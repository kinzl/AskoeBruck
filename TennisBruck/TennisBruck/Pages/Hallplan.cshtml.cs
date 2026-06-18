namespace TennisBruck.Pages;

[Authorize]
public class Hallplan(
    CurrentPlayerService currentPlayerService,
    TennisContext db,
    ILogger<Hallplan> logger)
    : PageModel
{
    public Player LoggedInPlayer { get; set; } = null!;

    [BindProperty] public List<HallPlanDay> HallPlanDays { get; set; } = [];

    public IEnumerable<Player> NotRegisteredPlayers { get; set; } = [];
    public IEnumerable<Player> RegisteredPlayers { get; set; } = [];
    public required IEnumerable<HallPlanEntity> HallPlanEntity { get; set; }
    [BindProperty] public int HallPlanId { get; set; }
    public bool IsLoggedInPlayerPlaying { get; set; }
    public string? Message { get; set; }
    public bool IsError { get; set; }
    public HallPlanEntity? SelectedHallPlanEntity { get; set; }

    public IActionResult OnPost(string? message = null, bool isError = false)
    {
        Message = message;
        IsError = isError;
        return InitValues();
    }

    public IActionResult OnGet(string? message = null, bool isError = false)
    {
        Message = message;
        IsError = isError;
        return InitValues();
    }

    private IActionResult InitValues()
    {
        HallPlanId = HttpContext.Session.GetInt32("selectedHallPlanId") ?? 0;

        LoggedInPlayer = currentPlayerService.GetCurrentUser()!;

        HallPlanEntity = db.HallPlanEntities.ToList();
        SelectedHallPlanEntity = db.HallPlanEntities.SingleOrDefault(x => x.Id == HallPlanId);

        HallPlanDays = db.HallPlanDays
            .Include(x => x.Players)
            .Where(x => x.HallPlanId == HallPlanId)
            .OrderBy(x => x.PlayDate)
            .ToList();

        NotRegisteredPlayers = db.Players
            .Where(p => !db.HallPlanRegistrations
                .Any(r => r.PlayerId == p.Id && r.HallPlanId == HallPlanId))
            .ToList();

        RegisteredPlayers = db.Players
            .Where(p => db.HallPlanRegistrations
                .Any(r => r.PlayerId == p.Id && r.HallPlanId == HallPlanId))
            .ToList();

        foreach (var court in HallPlanDays)
        {
            court.Players = court.Players.OrderBy(p => p.Player.ToString()).ToList();
        }

        if (SelectedHallPlanEntity != null)
        {
            IsLoggedInPlayerPlaying = db.HallPlanEntities
                .Include(hallPlanEntity => hallPlanEntity.Registrations)
                .Single(x => x.Id == SelectedHallPlanEntity.Id)
                .Registrations.Any(x => x.Player.Id == LoggedInPlayer.Id);
        }

        return Page();
    }

    public async Task<IActionResult> OnPostSwapPlayersAsync([FromBody] SwapRequestData data)
    {
        var currentUser = currentPlayerService.GetCurrentUser();
        if (currentUser == null) return Unauthorized();

        // Determine the HallPlanId from the courts involved so we can check registration
        var courtId = data.Court1Id ?? data.Court2Id;
        int? relevantHallPlanId = courtId.HasValue
            ? (await db.HallPlanDays.FindAsync(courtId.Value))?.HallPlanId
            : null;

        bool isInvolvedPlayer = currentUser.Id == data.Player1Id || currentUser.Id == data.Player2Id;
        bool isRegisteredInPlan = relevantHallPlanId.HasValue &&
            await db.HallPlanRegistrations.AnyAsync(r =>
                r.PlayerId == currentUser.Id && r.HallPlanId == relevantHallPlanId.Value);

        // Fall 1: Normaler Tausch (Beide Spieler sind schon im Plan auf einem Platz)
        if (data.Court1Id.HasValue && data.Court2Id.HasValue)
        {
            // db.Set<T>() greift auf die Tabelle zu, auch ohne DbSet-Eigenschaft im Context!
            var playerCourt1 = await db.HallPlanDayPlayers
                .Include(pc => pc.Player)
                .FirstOrDefaultAsync(pc => pc.Player.Id == data.Player1Id && pc.HallPlanDay.Id == data.Court1Id);

            var playerCourt2 = await db.HallPlanDayPlayers
                .Include(pc => pc.Player)
                .FirstOrDefaultAsync(pc => pc.Player.Id == data.Player2Id && pc.HallPlanDay.Id == data.Court2Id);

            if (playerCourt1 != null && playerCourt2 != null)
            {
                // Wir holen uns die echten Spieler-Objekte aus der Datenbank
                var tempPlayer = await db.Players.FindAsync(data.Player1Id);
                var player2 = await db.Players.FindAsync(data.Player2Id);

                // Und weisen sie über Kreuz neu zu
                playerCourt1.Player = player2!;
                playerCourt2.Player = tempPlayer!;

                await db.SaveChangesAsync();
            }
        }
        // Fall 2: Spieler 1 kommt von der Bank, Spieler 2 ist auf dem Platz
        else if (!data.Court1Id.HasValue && data.Court2Id.HasValue)
        {
            var playerCourt2 = await db.HallPlanDayPlayers
                .FirstOrDefaultAsync(pc => pc.Player.Id == data.Player2Id && pc.HallPlanDay.Id == data.Court2Id);

            if (playerCourt2 != null)
            {
                var benchPlayer = await db.Players.FindAsync(data.Player1Id);
                if (benchPlayer != null)
                {
                    playerCourt2.Player = benchPlayer;
                    await db.SaveChangesAsync();
                }
            }
        }
        // Fall 3: Spieler 2 kommt von der Bank (falls man den Platz-Spieler auf die Bank zieht)
        else if (data.Court1Id.HasValue && !data.Court2Id.HasValue)
        {
            var playerCourt1 = await db.HallPlanDayPlayers
                .FirstOrDefaultAsync(pc => pc.Player.Id == data.Player1Id && pc.HallPlanDay.Id == data.Court1Id);

            if (playerCourt1 != null)
            {
                var benchPlayer = await db.Players.FindAsync(data.Player2Id);
                if (benchPlayer != null)
                {
                    playerCourt1.Player = benchPlayer;
                    await db.SaveChangesAsync();
                }
            }
        }

        return new JsonResult(new { success = true });
    }

    public IActionResult OnPostCreateCompetition(string? competitionName)
    {
        if (!User.IsInRole("Admin")) return Forbid();
        if (string.IsNullOrWhiteSpace(competitionName))
            return RedirectToPage(nameof(Hallplan));

        var plan = new HallPlanEntity
        {
            Name = competitionName
        };

        db.HallPlanEntities.Add(plan);
        db.SaveChanges();

        return RedirectToPage(nameof(Hallplan), new { message = $"Bewerb '{competitionName}' erfolgreich angelegt." });
    }

    public IActionResult OnPostGeneratePlan(DateTime startDate, DateTime endDate, int frequencyDays)
    {
        if (!User.IsInRole("Admin")) return Forbid();
        logger.LogInformation("Starting plan generation");
        if (frequencyDays < 1) frequencyDays = 7;

        InitValues();

        var existingDays = db.HallPlanDays
            .Include(d => d.Players)
            .Where(d => d.HallPlanId == HallPlanId)
            .ToList();

        db.HallPlanDays.RemoveRange(existingDays);
        db.SaveChanges();

        // 2. Spieler laden
        var players = db.HallPlanEntities
            .Include(hallPlanEntity => hallPlanEntity.Registrations)
            .ThenInclude(hallPlanRegistration => hallPlanRegistration.Player)
            .Single(x => x.Id == SelectedHallPlanEntity!.Id)
            .Registrations
            .Select(x => x.Player)
            .ToList();

        if (players.Count < 4)
        {
            Console.WriteLine("Nicht genug Spieler für den Plan.");
            return Page();
        }

        var matchDays = new List<DateTime>();

        // Wir starten genau am Startdatum und springen immer 'frequencyDays' weiter
        for (var date = startDate; date <= endDate; date = date.AddDays(frequencyDays))
        {
            matchDays.Add(date);
        }

        if (!matchDays.Any())
        {
            Console.WriteLine("Keine Spieltage im gewählten Zeitraum gefunden.");
            return Page();
        }

        // 3. Spieler fair verteilen
        var playerCounts = players.ToDictionary(p => p.Id, _ => 0);
        var random = new Random();

        foreach (var day in matchDays)
        {
            var shuffled = players.OrderBy(_ => random.Next()).ToList();

            // Holt die 4 Spieler, die bisher am wenigsten gespielt haben
            var selectedPlayers = shuffled
                .OrderBy(p => playerCounts[p.Id])
                .Take(4)
                .ToList();

            // HallPlanDay erstellen
            var hallDay = new HallPlanDay
            {
                HallPlanId = HallPlanId,
                PlayDate = day,
                Players = new List<HallPlanDayPlayer>(),
                HallPlanEntity = SelectedHallPlanEntity!
            };

            foreach (var player in selectedPlayers)
            {
                hallDay.Players.Add(new HallPlanDayPlayer
                {
                    Player = player,
                    HallPlanDay = hallDay
                });

                // Spielanzahl erhöhen
                playerCounts[player.Id]++;
            }

            db.HallPlanDays.Add(hallDay);
        }

        db.SaveChanges();
        Console.WriteLine($"Balanced HallPlan erfolgreich generiert (Rhythmus: {frequencyDays} Tage).");
        logger.LogInformation("Plan generation complete");
        return RedirectToPage(nameof(Hallplan), new { message = $"Plan erfolgreich generiert (Rhythmus: {frequencyDays} Tage)." });
    }

    public IActionResult OnPostChangePlayingState()
    {
        InitValues();
        var user = currentPlayerService.GetCurrentUser()!;

        var player = db.Players
            .Include(x => x.IdentityUser)
            .Single(x => x.IdentityUserId == user.IdentityUserId);
        var hallPlanEntity = db.HallPlanEntities
            .Include(x => x.Registrations)
            .ThenInclude(x => x.Player)
            .Single(x => x.Id == SelectedHallPlanEntity!.Id);
        var isRegistered =
            hallPlanEntity.Registrations.SingleOrDefault(x => x.Player.IdentityUserId == user.IdentityUserId);

        if (isRegistered == null)
        {
            db.HallPlanRegistrations.Add(new HallPlanRegistration()
            {
                PlayerId = player.Id,
                Player = player,
                HallPlanEntity = hallPlanEntity,
                HallPlanId = hallPlanEntity.Id,
                RegisteredAt = DateTime.Now
            });
        }
        else
        {
            var hallplanRegistration =
                hallPlanEntity.Registrations.Single(x => x.Player.IdentityUserId == user.IdentityUserId);
            db.HallPlanRegistrations.Remove(hallplanRegistration);
        }

        db.SaveChanges();

        var msg = isRegistered == null ? "Du wurdest in den aktiven Hallenplan eingeteilt." : "Du wurdest auf der Ersatzbank (pausiert) platziert.";
        return RedirectToPage(nameof(Hallplan), new { message = msg });
    }

    public IActionResult OnPostAddPlayerToHallplan(int playerId)
    {
        var currentUser = currentPlayerService.GetCurrentUser();
        if (currentUser == null) return Unauthorized();
        if (currentUser.Id != playerId && !User.IsInRole("Admin")) return Forbid();

        InitValues();
        if (playerId == 0) return RedirectToPage(nameof(Hallplan), new { HallPlanId });

        var exists = db.HallPlanRegistrations.Any(x => x.PlayerId == playerId && x.HallPlanId == HallPlanId);

        if (exists) return RedirectToPage(nameof(Hallplan), new { message = "Spieler ist bereits fest im Abo.", isError = true });

        var player = db.Players.Single(x => x.Id == playerId);
        var hallPlanEntity = db.HallPlanEntities.Single(x => x.Id == HallPlanId);
        db.HallPlanRegistrations.Add(new HallPlanRegistration
        {
            PlayerId = playerId,
            HallPlanId = HallPlanId,
            Player = player,
            HallPlanEntity = hallPlanEntity,
            RegisteredAt = DateTime.Now
        });

        db.SaveChanges();

        return RedirectToPage(nameof(Hallplan), new { message = $"{player} wurde dauerhaft ins Abo aufgenommen." });
    }

    public IActionResult OnPostDeleteSelectedHallplan()
    {
        if (!User.IsInRole("Admin")) return Forbid();
        InitValues();
        var hallPlanEntity =
            db.HallPlanEntities.Single(x => SelectedHallPlanEntity != null && x.Id == SelectedHallPlanEntity.Id);
        db.HallPlanEntities.Remove(hallPlanEntity);
        db.SaveChanges();
        return RedirectToPage(nameof(Hallplan), new { message = "Hallenplan wurde erfolgreich gelöscht." });
    }

    public IActionResult OnPostBack()
    {
        return RedirectToPage(nameof(Index));
    }

    public IActionResult OnPostSelectHallPlan()
    {
        HttpContext.Session.SetInt32("selectedHallPlanId", HallPlanId);
        return RedirectToPage(nameof(Hallplan));
    }

    public async Task<IActionResult> OnPostRemovePlayerFromAboAsync(int playerIdToRemove)
    {
        var currentUser = currentPlayerService.GetCurrentUser();
        if (currentUser == null) return Unauthorized();
        if (currentUser.Id != playerIdToRemove && !User.IsInRole("Admin")) return Forbid();

        InitValues();
        var player = await db.Players.FirstOrDefaultAsync(p => p.Id == playerIdToRemove);

        if (player == null) return RedirectToPage();
        var hallPlanEntity = db.HallPlanEntities
            .Include(x => x.Registrations)
            .ThenInclude(x => x.Player)
            .Single(x => x.Id == SelectedHallPlanEntity!.Id);
        var registration = hallPlanEntity.Registrations.Single(x => x.Player.IdentityUserId == player.IdentityUserId);
        db.HallPlanRegistrations.Remove(registration);
        await db.SaveChangesAsync();
        return RedirectToPage(new { message = "Der Spieler wurde aus dem festen Abo entfernt." });
    }
}