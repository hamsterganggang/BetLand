namespace ShowMeTheBet.Models;

/// <summary>
/// 게임 베팅 정보를 나타내는 엔티티 클래스
/// 홀짝 게임과 그래프 게임의 베팅 정보를 저장합니다.
/// 
/// 주요 속성:
/// - Id: 베팅 고유 ID (기본 키)
/// - UserId: 베팅한 사용자 ID (외래 키)
/// - GameType: 게임 타입 (홀짝/그래프)
/// - BetChoice: 베팅 선택 ("홀"/"짝" 또는 배수 문자열)
/// - Amount: 베팅 금액
/// - Multiplier: 배수 (홀짝: 2.0, 그래프: 1.0~5.0)
/// - WinAmount: 승리 금액 (베팅 금액 × 배수)
/// - Status: 베팅 상태 (대기 중/승리/패배)
/// 
/// 관계:
/// - User: 베팅한 사용자
/// </summary>
public class GameBet
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
    /// 게임 타입 (홀짝 게임 또는 그래프 게임)
    /// </summary>
    public GameType GameType { get; set; }
    
    /// <summary>
    /// 베팅 선택
    /// - 홀짝 게임: "홀" 또는 "짝"
    /// - 그래프 게임: 배수 문자열 (예: "3.45") 또는 "실패"
    /// </summary>
    public string BetChoice { get; set; } = string.Empty;
    
    /// <summary>
    /// 베팅 금액 (소수점 2자리까지)
    /// </summary>
    public decimal Amount { get; set; }
    
    /// <summary>
    /// 배수 (소수점 2자리까지)
    /// - 홀짝 게임: 항상 2.0
    /// - 그래프 게임: 1.0 ~ 5.0 (게임 중 증가)
    /// - 실패: 0.0
    /// </summary>
    public decimal Multiplier { get; set; } = 1.0m;
    
    /// <summary>
    /// 승리 금액 (베팅 금액 × 배수)
    /// 승리 시 받을 수 있는 금액입니다.
    /// </summary>
    public decimal WinAmount { get; set; }
    
    /// <summary>
    /// 베팅 일시
    /// </summary>
    public DateTime BetTime { get; set; } = DateTime.Now;
    
    /// <summary>
    /// 베팅 상태
    /// - Pending: 결과 대기 중 (홀짝 게임만 해당)
    /// - Won: 승리
    /// - Lost: 패배
    /// </summary>
    public GameBetStatus Status { get; set; } = GameBetStatus.Pending;
    
    /// <summary>
    /// 결과 문자열 (UI 표시용)
    /// - 홀짝 게임: "홀" 또는 "짝"
    /// - 그래프 게임: "x3.45" (배수) 또는 "실패"
    /// </summary>
    public string Result { get; set; } = string.Empty;
    
    // ========== Navigation Properties (관계 속성) ==========
    
    /// <summary>
    /// 베팅한 사용자
    /// GameBet과 User는 N:1 관계입니다.
    /// </summary>
    public User User { get; set; } = null!;
}

/// <summary>
/// 게임 타입 열거형
/// 게임 베팅의 종류를 나타냅니다.
/// </summary>
public enum GameType
{
    /// <summary>
    /// 홀짝 게임
    /// 30초마다 새로운 라운드가 시작되며, 홀 또는 짝을 선택하여 베팅합니다.
    /// 배당률은 항상 x2.0입니다.
    /// </summary>
    OddEven,
    
    /// <summary>
    /// 그래프 게임
    /// 배수가 1.0에서 5.0까지 증가하는 게임입니다.
    /// 사용자가 STOP 버튼을 누르면 승리, 게임이 실패하면 패배입니다.
    /// </summary>
    Graph,
    
    /// <summary>
    /// 룰렛 게임
    /// 8개의 슬롯(꽝 4개, 배수 4개) 중 하나를 랜덤으로 선택하여 배당을 결정합니다.
    /// </summary>
    Roulette
}

/// <summary>
/// 게임 베팅 상태 열거형
/// 게임 베팅의 현재 상태를 나타냅니다.
/// </summary>
public enum GameBetStatus
{
    /// <summary>
    /// 결과 대기 중
    /// 홀짝 게임에서만 사용되며, 다음 라운드 결과를 기다리는 상태입니다.
    /// </summary>
    Pending,
    
    /// <summary>
    /// 승리
    /// 베팅한 결과와 게임 결과가 일치하거나, 그래프 게임에서 STOP 버튼을 눌러 승리한 경우입니다.
    /// </summary>
    Won,
    
    /// <summary>
    /// 패배
    /// 베팅한 결과와 게임 결과가 불일치하거나, 그래프 게임에서 실패한 경우입니다.
    /// </summary>
    Lost
}
