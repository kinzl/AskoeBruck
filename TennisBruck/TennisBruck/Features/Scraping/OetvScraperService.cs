using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TennisDb;

namespace TennisBruck.Features.Scraping;

public class OetvScraperService(HttpClient httpClient, ILogger<OetvScraperService> logger, IConfiguration configuration)
{
    private string GetApiKey() => Environment.GetEnvironmentVariable("OETV_API_KEY")!;

    /// <summary>
    /// Fetches the ITN for a specific player profile URL.
    /// URL should look something like: https://www.oetv.at/spieler/NU12345
    /// </summary>
    public async Task<decimal?> GetPlayerItnAsync(string? nuLigaPlayerUrl)
    {
        if (string.IsNullOrWhiteSpace(nuLigaPlayerUrl)) return null;

        try
        {
            var uri = new Uri(nuLigaPlayerUrl);
            var playerId = uri.Segments.Last().Trim('/');

            string apiKey = GetApiKey();
            string apiUrl = $"https://www.oetv.at/?oetvappapi=1&apikey={apiKey}&method=nu-player&playerId={playerId}";

            using var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
            request.Headers.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/123.0.0.0 Safari/537.36");
            request.Headers.Add("Accept", "application/json, text/javascript, */*; q=0.01");
            request.Headers.Add("X-Requested-With", "XMLHttpRequest");
            request.Headers.Add("Referer", nuLigaPlayerUrl);

            var response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var jsonContent = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(jsonContent);

            var root = document.RootElement;
            if (root.TryGetProperty("success", out var successElement) && successElement.GetBoolean() == true)
            {
                if (root.TryGetProperty("data", out var dataElement) &&
                    dataElement.TryGetProperty("player", out var playerObj))
                {
                    if (playerObj.TryGetProperty("fedRank", out var fedRankElement) &&
                        fedRankElement.ValueKind == JsonValueKind.Number)
                    {
                        return fedRankElement.GetDecimal();
                    }

                    if (fedRankElement.ValueKind == JsonValueKind.String)
                    {
                        var itnStr = fedRankElement.GetString()?.Replace(',', '.');
                        if (decimal.TryParse(itnStr, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal itn))
                            return itn;
                    }
                }
            }

            logger.LogWarning("Could not find ITN (fedRank) in JSON for {Url}", nuLigaPlayerUrl);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching ITN for URL: {Url}", nuLigaPlayerUrl);
            return null;
        }
    }

    /// <summary>
    /// Attempts to automatically find the player's profile URL on the ÖTV website
    /// by searching for their name via the ÖTV API and matching the exact club name (Verein).
    /// </summary>
    public async Task<string?> AutomaticallyFindPlayerUrlAsync(string firstName, string lastName, string targetClubName)
    {
        try
        {
            string apiKey = GetApiKey();
            string apiUrl =
                $"https://www.oetv.at/?oetvappapi=1&apikey={apiKey}&method=nu-players&firstname={Uri.EscapeDataString(firstName)}&lastname={Uri.EscapeDataString(lastName)}";

            using var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
            request.Headers.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/123.0.0.0 Safari/537.36");
            request.Headers.Add("Accept", "application/json, text/javascript, */*; q=0.01");
            request.Headers.Add("X-Requested-With", "XMLHttpRequest");
            request.Headers.Add("Referer", "https://www.oetv.at/spieler");

            var response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var jsonContent = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(jsonContent);

            var root = document.RootElement;
            if (root.TryGetProperty("success", out var successElement) && successElement.GetBoolean() == true)
            {
                if (root.TryGetProperty("data", out var dataElement) &&
                    dataElement.TryGetProperty("players", out var playersArray))
                {
                    var matchingPlayers = new List<JsonElement>();

                    foreach (var player in playersArray.EnumerateArray())
                    {
                        bool clubMatches = false;
                        bool nameMatches = false;

                        if (player.TryGetProperty("clubName", out var clubNameElement))
                        {
                            var clubName = clubNameElement.GetString() ?? "";

                            var tokens = targetClubName.Split(new[] { ' ', '-' },
                                StringSplitOptions.RemoveEmptyEntries);
                            clubMatches = true;
                            foreach (var token in tokens)
                            {
                                if (token.Contains("Ö", StringComparison.OrdinalIgnoreCase) ||
                                    token.Contains("ö", StringComparison.OrdinalIgnoreCase)) continue;

                                if (!clubName.Contains(token, StringComparison.OrdinalIgnoreCase))
                                {
                                    clubMatches = false;
                                    break;
                                }
                            }
                        }

                        if (player.TryGetProperty("firstname", out var fnElement) &&
                            player.TryGetProperty("lastname", out var lnElement))
                        {
                            var apiFirstname = fnElement.GetString() ?? "";
                            var apiLastname = lnElement.GetString() ?? "";

                            nameMatches = apiFirstname.Trim()
                                              .Equals(firstName.Trim(), StringComparison.OrdinalIgnoreCase) &&
                                          apiLastname.Trim().Equals(lastName.Trim(),
                                              StringComparison.OrdinalIgnoreCase);
                        }

                        if (clubMatches && nameMatches)
                        {
                            matchingPlayers.Add(player);
                        }
                    }

                    if (matchingPlayers.Count == 1)
                    {
                        var playerRecord = matchingPlayers[0];
                        if (playerRecord.TryGetProperty("playerId", out var playerIdElement))
                        {
                            var playerId = playerIdElement.GetString();
                            if (!string.IsNullOrEmpty(playerId))
                            {
                                return $"https://www.oetv.at/spieler/{playerId}";
                            }
                        }
                    }

                    logger.LogInformation(
                        "Automatic search ended. Found {Count} matching players for {First} {Last} in club {Club}.",
                        matchingPlayers.Count, firstName, lastName, targetClubName);
                    return null;
                }
            }

            logger.LogWarning("Failed to parse expected JSON structure from ÖTV API for {First} {Last}.", firstName,
                lastName);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to automatically find player URL for {First} {Last}.", firstName, lastName);
            return null;
        }
    }

    /// <summary>
    /// Fetches official ÖTV / OÖTV league team matches for the club.
    /// Supports direct nuLiga / ÖTV URL (e.g. from ooetv.at or oetv.at) or club number (e.g. 40291 for ASKÖ Bruck - Peuerbach).
    /// </summary>
    public async Task<List<OetvMatch>> FetchClubMatchesAsync(string? clubIdentifier = "40291")
    {
        var matches = new List<OetvMatch>();
        clubIdentifier = string.IsNullOrWhiteSpace(clubIdentifier) ? "40291" : clubIdentifier.Trim();

        // 1. If direct URL provided, scrape HTML from that specific page
        if (clubIdentifier.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            clubIdentifier.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                matches = await ScrapeMatchesFromHtmlUrlAsync(clubIdentifier);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not scrape matches from HTML URL {Url}", clubIdentifier);
            }

            return matches;
        }

        // 2. Try querying the official OÖTV API using the configured API key
        try
        {
            string? apiKey = Environment.GetEnvironmentVariable("OETV_API_KEY");
            if (!string.IsNullOrEmpty(apiKey))
            {
                var now = DateTime.UtcNow;
                var startDate = $"{now.Year - 1}-01-01";
                var endDate = $"{now.Year + 1}-12-31";
                string apiUrl = $"https://www.ooetv.at/?oetvappapi=1&apikey={apiKey}&method=nu-club-meetings&clubId={clubIdentifier}&startDate={startDate}&endDate={endDate}&fed={Uri.EscapeDataString("OÖTV")}";

                using var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
                request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/123.0.0.0 Safari/537.36");
                request.Headers.Add("Accept", "application/json, text/javascript, */*; q=0.01");
                request.Headers.Add("X-Requested-With", "XMLHttpRequest");
                request.Headers.Add("Referer", "https://www.ooetv.at/vereine");
                request.Headers.Add("Cookie", "cookie_optin=essential:1|statistiken:1; SgCookieOptin.lastPreferences=true;");

                var response = await httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("success", out var success) && success.GetBoolean())
                    {
                        if (doc.RootElement.TryGetProperty("data", out var data) &&
                            data.TryGetProperty("meetings", out var meetingsArray) &&
                            meetingsArray.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var item in meetingsArray.EnumerateArray())
                            {
                                var match = ParseMatchFromJson(item);
                                if (match != null) matches.Add(match);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not fetch matches via OÖTV API for {Club}", clubIdentifier);
        }

        // 3. If no matches yet and a club ID like 40291 was specified, query the official OÖTV club page
        if (!matches.Any())
        {
            var urlsToTry = new[]
            {
                $"https://www.ooetv.at/liga/vereine/verein/v/{clubIdentifier}.html",
                $"https://www.oetv.at/liga/vereine/verein/v/{clubIdentifier}.html"
            };

            foreach (var url in urlsToTry)
            {
                try
                {
                    matches = await ScrapeMatchesFromHtmlUrlAsync(url);
                    if (matches.Any()) break;
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Attempt to scrape {Url} yielded no results", url);
                }
            }
        }

        return matches;
    }

    private static OetvMatch? ParseMatchFromJson(JsonElement el)
    {
        try
        {
            // Support both OÖTV app API field names and standard names
            string comp = "";
            if (el.TryGetProperty("groupName", out var gn)) comp = gn.GetString() ?? "";
            else if (el.TryGetProperty("competition", out var c)) comp = c.GetString() ?? "";

            string home = "";
            if (el.TryGetProperty("home", out var h)) home = h.GetString() ?? "";
            else if (el.TryGetProperty("homeTeam", out var ht)) home = ht.GetString() ?? "";

            string away = "";
            if (el.TryGetProperty("guest", out var g)) away = g.GetString() ?? "";
            else if (el.TryGetProperty("awayTeam", out var at)) away = at.GetString() ?? "";

            if (string.IsNullOrWhiteSpace(home) || string.IsNullOrWhiteSpace(away)) return null;
            if (home.Contains("spielfrei", StringComparison.OrdinalIgnoreCase) ||
                away.Contains("spielfrei", StringComparison.OrdinalIgnoreCase)) return null;

            string dateStr = "";
            if (el.TryGetProperty("scheduled", out var s)) dateStr = s.GetString() ?? "";
            else if (el.TryGetProperty("date", out var d)) dateStr = d.GetString() ?? "";

            DateTime matchDate = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(dateStr))
            {
                if (DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var parsedUtc))
                {
                    matchDate = DateTime.SpecifyKind(parsedUtc, DateTimeKind.Utc);
                }
                else if (DateTime.TryParse(dateStr, CultureInfo.GetCultureInfo("de-AT"), DateTimeStyles.AssumeLocal, out var parsedAt))
                {
                    matchDate = parsedAt.ToUniversalTime();
                }
            }

            bool isHome = home.Contains("Bruck", StringComparison.OrdinalIgnoreCase);

            string venue = "";
            if (el.TryGetProperty("courtHallName", out var ch) && !string.IsNullOrWhiteSpace(ch.GetString()))
            {
                venue = ch.GetString()!;
            }
            else if (el.TryGetProperty("venue", out var v) && !string.IsNullOrWhiteSpace(v.GetString()))
            {
                venue = v.GetString()!;
            }
            else
            {
                venue = isHome ? "Tennisanlage ASKÖ Bruck" : $"Tennisanlage {away}";
            }

            // Results and score
            string? score = null;
            string status = "Geplant";

            if (el.TryGetProperty("matchesHome", out var mh) && el.TryGetProperty("matchesGuest", out var mg))
            {
                int hScore = mh.GetInt32();
                int gScore = mg.GetInt32();
                bool hasSets = el.TryGetProperty("setsHome", out var sh) && sh.GetInt32() > 0;
                if (hScore > 0 || gScore > 0 || hasSets)
                {
                    score = $"{hScore} : {gScore}";
                    status = "Beendet";
                }
            }
            else if (el.TryGetProperty("score", out var sc) && !string.IsNullOrWhiteSpace(sc.GetString()))
            {
                score = sc.GetString();
                status = "Beendet";
            }

            string? pdfUrl = null;
            if (el.TryGetProperty("pdfUrl", out var p) && !string.IsNullOrWhiteSpace(p.GetString()))
            {
                pdfUrl = p.GetString();
            }
            else if (el.TryGetProperty("url", out var u) && !string.IsNullOrWhiteSpace(u.GetString()))
            {
                pdfUrl = u.GetString();
            }

            string category = DetermineCategory(comp, home);
            if (el.TryGetProperty("contestCategory", out var cc))
            {
                var catStr = cc.GetString()?.ToLowerInvariant() ?? "";
                if (catStr.Contains("sen") || catStr.Contains("senior")) category = "Senioren";
                else if (catStr.Contains("jun") || catStr.Contains("kid") || catStr.Contains("jgd")) category = "Jugend / Kids";
            }

            return new OetvMatch
            {
                CompetitionName = string.IsNullOrWhiteSpace(comp) ? "OÖTV Mannschaftsmeisterschaft" : comp,
                Category = category,
                HomeTeam = home,
                AwayTeam = away,
                IsHomeMatch = isHome,
                MatchDateTime = matchDate,
                Venue = venue,
                Score = score,
                Status = status,
                OetvUrl = pdfUrl
            };
        }
        catch
        {
            return null;
        }
    }

    private async Task<List<OetvMatch>> ScrapeMatchesFromHtmlUrlAsync(string url)
    {
        var list = new List<OetvMatch>();
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/123.0.0.0 Safari/537.36");
        request.Headers.Add("Cookie", "cookie_optin=essential:1|statistiken:1; SgCookieOptin.lastPreferences=true;");
        request.Headers.Add("Referer", "https://www.ooetv.at/");

        var response = await httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode) return list;

        var html = await response.Content.ReadAsStringAsync();
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var rows = doc.DocumentNode.SelectNodes("//table[contains(@class, 'result-set')]//tr") ??
                   doc.DocumentNode.SelectNodes("//table//tr");

        if (rows == null) return list;

        foreach (var row in rows)
        {
            var cells = row.SelectNodes("td");
            if (cells == null || cells.Count < 3) continue;

            // In nuLiga schedules, typically:
            // Cell 0: Date/Time (e.g. "Sa. 16.05.2026 13:00")
            // Cell 1: Home Team or Liga
            // Cell 2: Guest Team
            // Cell 3: Result / Score
            string rawDate = cells[0].InnerText.Trim();
            string rawHome = "";
            string rawAway = "";
            string rawScore = "";
            string matchUrl = "";

            var link = row.SelectSingleNode(".//a[@href]");
            if (link != null)
            {
                var href = link.GetAttributeValue("href", "");
                if (!string.IsNullOrEmpty(href))
                {
                    matchUrl = href.StartsWith("http") ? href : new Uri(new Uri(url), href).ToString();
                }
            }

            if (cells.Count >= 5)
            {
                rawHome = cells[2].InnerText.Trim();
                rawAway = cells[3].InnerText.Trim();
                if (cells.Count > 4) rawScore = cells[4].InnerText.Trim();
            }
            else if (cells.Count >= 3)
            {
                rawHome = cells[1].InnerText.Trim();
                rawAway = cells[2].InnerText.Trim();
            }

            if (string.IsNullOrWhiteSpace(rawHome) || string.IsNullOrWhiteSpace(rawAway)) continue;

            // Filter out column header rows
            if (rawHome.Equals("Heimmannschaft", StringComparison.OrdinalIgnoreCase) ||
                rawHome.Equals("Begegnung", StringComparison.OrdinalIgnoreCase)) continue;

            bool isHome = rawHome.Contains("Bruck", StringComparison.OrdinalIgnoreCase);

            DateTime dt = DateTime.UtcNow;
            var cleanDateStr = Regex.Replace(rawDate, @"^[A-Za-zäöüÄÖÜ\.]+\s*", ""); // Remove "Sa. "
            if (DateTime.TryParse(cleanDateStr, CultureInfo.GetCultureInfo("de-AT"), out var parsed))
            {
                dt = parsed;
            }

            string compName = "OÖTV Mannschaftsmeisterschaft";
            var headerNode = row.SelectSingleNode("./preceding::h2[1] | ./preceding::h3[1] | ./preceding::caption[1]");
            if (headerNode != null && !string.IsNullOrWhiteSpace(headerNode.InnerText))
            {
                compName = headerNode.InnerText.Trim();
            }

            list.Add(new OetvMatch
            {
                CompetitionName = compName,
                Category = DetermineCategory(compName, rawHome),
                HomeTeam = rawHome,
                AwayTeam = rawAway,
                IsHomeMatch = isHome,
                MatchDateTime = dt,
                Venue = isHome ? "Tennisanlage ASKÖ Bruck" : $"Tennisanlage {rawAway}",
                Score = string.IsNullOrWhiteSpace(rawScore) || rawScore.Contains(":") == false ? null : rawScore,
                Status = string.IsNullOrWhiteSpace(rawScore) || rawScore.Contains(":") == false ? "Geplant" : "Beendet",
                OetvUrl = string.IsNullOrWhiteSpace(matchUrl) ? null : matchUrl
            });
        }

        return list;
    }

    private static string DetermineCategory(string comp, string team)
    {
        string text = $"{comp} {team}".ToLowerInvariant();
        if (text.Contains("damen") || text.Contains("frauen")) return "Damen";
        if (text.Contains("jugend") || text.Contains("kids") || text.Contains("junior") || text.Contains("green") || text.Contains("u10") || text.Contains("u12") || text.Contains("u14") || text.Contains("u16")) return "Jugend / Kids";
        if (text.Contains("senioren") || text.Contains("35") || text.Contains("45") || text.Contains("55") || text.Contains("60") || text.Contains("65")) return "Senioren";
        if (text.Contains("mixed")) return "Mixed";
        if (text.Contains("herren")) return "Herren";
        return "Herren";
    }
}



