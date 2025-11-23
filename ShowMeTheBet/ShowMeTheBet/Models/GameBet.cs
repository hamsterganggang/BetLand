namespace ShowMeTheBet.Models;

public class GameBet
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public GameType GameType { get; set; }
    public string BetChoice { get; set; } = string.Empty; // "홀" or "짝" for OddEven, multiplier for Graph
    public decimal Amount { get; set; }
    public decimal Multiplier { get; set; } = 1.0m;
    public decimal WinAmount { get; set; }
    public DateTime BetTime { get; set; } = DateTime.Now;
    public GameBetStatus Status { get; set; } = GameBetStatus.Pending;
    public string Result { get; set; } = string.Empty;
    
    // Navigation properties
    public User User { get; set; } = null!;
}

public enum GameType
{
    OddEven,  // 홀짝 게임
    Graph     // 그래프 게임
}

public enum GameBetStatus
{
    Pending,
    Won,
    Lost
}

