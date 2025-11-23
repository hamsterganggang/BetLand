namespace ShowMeTheBet.Models;

public class Bet
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int MatchId { get; set; }
    public string MatchInfo { get; set; } = string.Empty;
    public BetType Type { get; set; }
    public decimal Odds { get; set; }
    public decimal Amount { get; set; }
    public decimal PotentialWin { get; set; }
    public DateTime BetTime { get; set; } = DateTime.Now;
    public BetStatus Status { get; set; } = BetStatus.Pending;
    
    // Navigation properties
    public User User { get; set; } = null!;
    public Match Match { get; set; } = null!;
}

public enum BetType
{
    Home,
    Draw,
    Away
}

public enum BetStatus
{
    Pending,
    Won,
    Lost
}

