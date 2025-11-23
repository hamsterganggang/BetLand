using System.ComponentModel.DataAnnotations;

namespace ShowMeTheBet.Models;

public class User
{
    public int Id { get; set; }
    
    [Required]
    [StringLength(50)]
    public string Username { get; set; } = string.Empty;
    
    [Required]
    [StringLength(100)]
    public string Email { get; set; } = string.Empty;
    
    [Required]
    public string PasswordHash { get; set; } = string.Empty;
    
    public decimal Balance { get; set; } = 100000; // 초기 잔액 10만원
    
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    
    // Navigation properties
    public List<Bet> Bets { get; set; } = new();
    public List<GameBet> GameBets { get; set; } = new();
}

