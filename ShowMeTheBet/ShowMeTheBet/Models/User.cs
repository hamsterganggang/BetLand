using System.ComponentModel.DataAnnotations;

namespace ShowMeTheBet.Models;

/// <summary>
/// 사용자 정보를 나타내는 엔티티 클래스
/// 
/// 주요 속성:
/// - Id: 사용자 고유 ID (기본 키)
/// - Username: 사용자명 (고유, 최대 50자)
/// - Email: 이메일 주소 (고유, 최대 100자)
/// - PasswordHash: 비밀번호 해시 (BCrypt로 암호화)
/// - Balance: 사용자 잔액 (초기값: 100,000원)
/// - CreatedAt: 계정 생성 일시
/// 
/// 관계:
/// - Bets: 사용자가 한 스포츠 베팅 목록
/// - GameBets: 사용자가 한 게임 베팅 목록 (홀짝, 그래프 게임)
/// </summary>
public class User
{
    /// <summary>
    /// 사용자 고유 ID (기본 키, 자동 증가)
    /// </summary>
    public int Id { get; set; }
    
    /// <summary>
    /// 사용자명 (고유, 필수, 최대 50자)
    /// </summary>
    [Required]
    [StringLength(50)]
    public string Username { get; set; } = string.Empty;
    
    /// <summary>
    /// 이메일 주소 (고유, 필수, 최대 100자)
    /// </summary>
    [Required]
    [StringLength(100)]
    public string Email { get; set; } = string.Empty;
    
    /// <summary>
    /// 비밀번호 해시 (BCrypt로 암호화된 비밀번호)
    /// 원본 비밀번호는 저장하지 않으며, 해시만 저장합니다.
    /// </summary>
    [Required]
    public string PasswordHash { get; set; } = string.Empty;
    
    /// <summary>
    /// 사용자 잔액 (소수점 2자리까지, 초기값: 100,000원)
    /// 베팅, 승리, 패배에 따라 변경됩니다.
    /// </summary>
    public decimal Balance { get; set; } = 100000; // 초기 잔액 10만원
    
    /// <summary>
    /// 계정 생성 일시
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    
    // ========== Navigation Properties (관계 속성) ==========
    
    /// <summary>
    /// 사용자가 한 스포츠 베팅 목록
    /// User와 Bet은 1:N 관계입니다.
    /// </summary>
    public List<Bet> Bets { get; set; } = new();
    
    /// <summary>
    /// 사용자가 한 게임 베팅 목록 (홀짝, 그래프 게임)
    /// User와 GameBet은 1:N 관계입니다.
    /// </summary>
    public List<GameBet> GameBets { get; set; } = new();
}
