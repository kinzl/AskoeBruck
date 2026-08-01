using System;
using System.Collections.Generic;
using TennisDb;
using TennisBruck.Services;

namespace TennisBruck.Pages.Partials;

public class DoublePairsConfigViewModel
{
    public int CompetitionId { get; set; }
    public DateTime RegistrationUntil { get; set; }
    public bool IsAdmin { get; set; }
    public bool IsRegistrationExpired => RegistrationUntil < CityTime.GetViennaTimeZone();
    public List<Player> RegisteredPlayers { get; set; } = new();
    public List<Team> RegisteredTeams { get; set; } = new();
}
