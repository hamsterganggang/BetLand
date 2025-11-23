namespace ShowMeTheBet.Models;

public class Match
{
    public int Id { get; set; }
    public string League { get; set; } = string.Empty;
    public string HomeTeam { get; set; } = string.Empty;
    public string AwayTeam { get; set; } = string.Empty;
    public DateTime MatchTime { get; set; }
    public decimal HomeOdds { get; set; }
    public decimal DrawOdds { get; set; }
    public decimal AwayOdds { get; set; }
    public MatchStatus Status { get; set; } = MatchStatus.Upcoming;
    public int? HomeScore { get; set; }
    public int? AwayScore { get; set; }
    
    // Navigation properties
    public List<Bet> Bets { get; set; } = new();
}

public enum MatchStatus
{
    Upcoming,
    Live,
    Finished
}

