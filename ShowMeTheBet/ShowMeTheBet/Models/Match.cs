namespace ShowMeTheBet.Models;

/// <summary>
/// 경기 정보를 나타내는 엔티티 클래스
/// 스포츠 토토에서 베팅할 수 있는 경기 정보를 저장합니다.
/// 
/// 주요 속성:
/// - Id: 경기 고유 ID (기본 키)
/// - League: 리그명 (예: "K리그1", "프리미어리그")
/// - HomeTeam: 홈팀 이름
/// - AwayTeam: 원정팀 이름
/// - MatchTime: 경기 일시
/// - HomeOdds: 홈팀 승리 배당률
/// - DrawOdds: 무승부 배당률
/// - AwayOdds: 원정팀 승리 배당률
/// - Status: 경기 상태 (예정/진행 중/종료)
/// 
/// 관계:
/// - Bets: 이 경기에 대한 베팅 목록
/// </summary>
public class Match
{
    /// <summary>
    /// 경기 고유 ID (기본 키, 자동 증가)
    /// </summary>
    public int Id { get; set; }
    
    /// <summary>
    /// 리그명 (예: "K리그1", "프리미어리그", "라리가", "분데스리가")
    /// </summary>
    public string League { get; set; } = string.Empty;
    
    /// <summary>
    /// 홈팀 이름
    /// </summary>
    public string HomeTeam { get; set; } = string.Empty;
    
    /// <summary>
    /// 원정팀 이름
    /// </summary>
    public string AwayTeam { get; set; } = string.Empty;
    
    /// <summary>
    /// 경기 일시
    /// </summary>
    public DateTime MatchTime { get; set; }
    
    /// <summary>
    /// 홈팀 승리 배당률 (소수점 2자리까지)
    /// </summary>
    public decimal HomeOdds { get; set; }
    
    /// <summary>
    /// 무승부 배당률 (소수점 2자리까지)
    /// </summary>
    public decimal DrawOdds { get; set; }
    
    /// <summary>
    /// 원정팀 승리 배당률 (소수점 2자리까지)
    /// </summary>
    public decimal AwayOdds { get; set; }
    
    /// <summary>
    /// 경기 상태
    /// - Upcoming: 예정된 경기 (베팅 가능)
    /// - Live: 진행 중인 경기 (베팅 불가)
    /// - Finished: 종료된 경기 (베팅 불가)
    /// </summary>
    public MatchStatus Status { get; set; } = MatchStatus.Upcoming;
    
    /// <summary>
    /// 홈팀 점수 (경기 종료 후 설정)
    /// </summary>
    public int? HomeScore { get; set; }
    
    /// <summary>
    /// 원정팀 점수 (경기 종료 후 설정)
    /// </summary>
    public int? AwayScore { get; set; }
    
    // ========== Navigation Properties (관계 속성) ==========
    
    /// <summary>
    /// 이 경기에 대한 베팅 목록
    /// Match와 Bet은 1:N 관계입니다.
    /// </summary>
    public List<Bet> Bets { get; set; } = new();
}

/// <summary>
/// 경기 상태 열거형
/// 경기의 현재 상태를 나타냅니다.
/// </summary>
public enum MatchStatus
{
    /// <summary>
    /// 예정된 경기
    /// 아직 시작하지 않았으며, 베팅이 가능한 상태입니다.
    /// </summary>
    Upcoming,
    
    /// <summary>
    /// 진행 중인 경기
    /// 경기가 진행 중이며, 베팅이 불가능한 상태입니다.
    /// </summary>
    Live,
    
    /// <summary>
    /// 종료된 경기
    /// 경기가 종료되었으며, 베팅 결과가 확정된 상태입니다.
    /// </summary>
    Finished
}
