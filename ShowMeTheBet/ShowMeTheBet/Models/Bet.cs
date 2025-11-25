namespace ShowMeTheBet.Models;

/// <summary>
/// 스포츠 베팅 정보를 나타내는 엔티티 클래스
/// 
/// 주요 속성:
/// - Id: 베팅 고유 ID (기본 키)
/// - UserId: 베팅한 사용자 ID (외래 키)
/// - MatchId: 베팅한 경기 ID (외래 키)
/// - Type: 베팅 타입 (홈/무/원정)
/// - Odds: 배당률
/// - Amount: 베팅 금액
/// - PotentialWin: 예상 승리 금액 (베팅 금액 × 배당률)
/// - Status: 베팅 상태 (대기 중/승리/패배)
/// 
/// 관계:
/// - User: 베팅한 사용자
/// - Match: 베팅한 경기
/// </summary>
public class Bet
{
    /// <summary>
    /// 베팅 고유 ID (기본 키, 자동 증가)
    /// </summary>
    public int Id { get; set; }
    
    /// <summary>
    /// 베팅한 사용자 ID (외래 키, User 테이블 참조)
    /// </summary>
    public int UserId { get; set; }
    
    /// <summary>
    /// 베팅한 경기 ID (외래 키, Match 테이블 참조)
    /// </summary>
    public int MatchId { get; set; }
    
    /// <summary>
    /// 경기 정보 문자열 (예: "FC서울 vs 수원삼성")
    /// UI 표시용으로 사용됩니다.
    /// </summary>
    public string MatchInfo { get; set; } = string.Empty;
    
    /// <summary>
    /// 베팅 타입 (홈팀 승리/무승부/원정팀 승리)
    /// </summary>
    public BetType Type { get; set; }
    
    /// <summary>
    /// 배당률 (소수점 2자리까지)
    /// 베팅 타입에 따라 경기의 HomeOdds, DrawOdds, AwayOdds 중 하나가 저장됩니다.
    /// </summary>
    public decimal Odds { get; set; }
    
    /// <summary>
    /// 베팅 금액 (소수점 2자리까지)
    /// </summary>
    public decimal Amount { get; set; }
    
    /// <summary>
    /// 예상 승리 금액 (베팅 금액 × 배당률)
    /// 베팅 시점의 배당률로 계산된 값입니다.
    /// </summary>
    public decimal PotentialWin { get; set; }
    
    /// <summary>
    /// 베팅 일시
    /// </summary>
    public DateTime BetTime { get; set; } = DateTime.Now;
    
    /// <summary>
    /// 베팅 상태
    /// - Pending: 경기 결과 대기 중
    /// - Won: 승리
    /// - Lost: 패배
    /// </summary>
    public BetStatus Status { get; set; } = BetStatus.Pending;
    
    // ========== Navigation Properties (관계 속성) ==========
    
    /// <summary>
    /// 베팅한 사용자
    /// Bet과 User는 N:1 관계입니다.
    /// </summary>
    public User User { get; set; } = null!;
    
    /// <summary>
    /// 베팅한 경기
    /// Bet과 Match는 N:1 관계입니다.
    /// </summary>
    public Match Match { get; set; } = null!;
}

/// <summary>
/// 베팅 타입 열거형
/// 스포츠 베팅에서 선택할 수 있는 옵션입니다.
/// </summary>
public enum BetType
{
    /// <summary>
    /// 홈팀 승리
    /// </summary>
    Home,
    
    /// <summary>
    /// 무승부
    /// </summary>
    Draw,
    
    /// <summary>
    /// 원정팀 승리
    /// </summary>
    Away
}

/// <summary>
/// 베팅 상태 열거형
/// 베팅의 현재 상태를 나타냅니다.
/// </summary>
public enum BetStatus
{
    /// <summary>
    /// 경기 결과 대기 중
    /// </summary>
    Pending,
    
    /// <summary>
    /// 승리 (베팅한 결과와 경기 결과가 일치)
    /// </summary>
    Won,
    
    /// <summary>
    /// 패배 (베팅한 결과와 경기 결과가 불일치)
    /// </summary>
    Lost
}
