using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

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
}
