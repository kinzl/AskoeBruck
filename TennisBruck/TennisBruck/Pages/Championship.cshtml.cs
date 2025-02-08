using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TennisBruck.Pages;

public class Championship : PageModel
{
    public List<Group> Groups { get; set; }
    public List<Match> KnockoutMatches { get; set; }

    public void OnGet()
    {
        Groups = new List<Group>
        {
            new Group
            {
                Name = "Group A",
                Teams = new List<Team> { new Team { Id = 1, Name = "Team 1" }, new Team { Id = 2, Name = "Team 2" } }
            },
            new Group
            {
                Name = "Group B",
                Teams = new List<Team> { new Team { Id = 3, Name = "Team 3" }, new Team { Id = 4, Name = "Team 4" } }
            }
        };

        KnockoutMatches = new List<Match>
        {
            new Match { Team1 = Groups[0].Teams[0], Team2 = Groups[1].Teams[0] },
            new Match { Team1 = Groups[0].Teams[1], Team2 = Groups[1].Teams[1] }
        };
    }

    public class Team
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public class Group
    {
        public string Name { get; set; }
        public List<Team> Teams { get; set; }
    }

    public class Match
    {
        public Team Team1 { get; set; }
        public Team Team2 { get; set; }
    }
}