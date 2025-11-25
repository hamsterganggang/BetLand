using Microsoft.EntityFrameworkCore;
using ShowMeTheBet.Data;
using ShowMeTheBet.Models;

namespace ShowMeTheBet.Services;

/// <summary>
/// 스포츠 베팅 관련 비즈니스 로직을 처리하는 서비스 클래스
/// 경기 정보 조회, 베팅 처리, 베팅 내역 관리 등의 기능을 제공합니다.
/// 
/// 주요 기능:
/// - 경기 목록 조회 (예정된 경기 및 진행 중인 경기)
/// - 스포츠 베팅 처리 (홈/무/원정)
/// - 베팅 내역 조회 및 관리
/// - 베팅 취소 및 환불
/// - 사용자 랭킹 조회
/// </summary>
public class BettingService
{
    #region Private Fields
    /// <summary>
    /// 데이터베이스 컨텍스트 - 경기 및 베팅 정보 저장 및 조회
    /// </summary>
    private readonly BettingDbContext _context;
    
    /// <summary>
    /// 인증 서비스 - 사용자 정보 관리
    /// </summary>
    private readonly AuthService _authService;
    #endregion

    #region Constructor
    /// <summary>
    /// BettingService 생성자
    /// </summary>
    /// <param name="context">데이터베이스 컨텍스트</param>
    /// <param name="authService">인증 서비스</param>
    public BettingService(BettingDbContext context, AuthService authService)
    {
        _context = context;
        _authService = authService;
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// 예정된 경기 및 진행 중인 경기 목록을 조회합니다.
    /// 
    /// 조회 조건:
    /// - MatchStatus가 Upcoming(예정) 또는 Live(진행 중)인 경기만 조회
    /// - 경기 시간순으로 정렬 (가장 가까운 경기부터)
    /// </summary>
    /// <returns>경기 목록 (경기 시간순 정렬)</returns>
    public async Task<List<Match>> GetMatchesAsync()
    {
        return await _context.Matches
            .Where(m => m.Status == MatchStatus.Upcoming || m.Status == MatchStatus.Live)
            .OrderBy(m => m.MatchTime)
            .ToListAsync();
    }
    
    /// <summary>
    /// 경기 ID로 경기 정보를 조회합니다.
    /// </summary>
    /// <param name="id">경기 ID</param>
    /// <returns>경기 정보 (없으면 null)</returns>
    public async Task<Match?> GetMatchAsync(int id)
    {
        return await _context.Matches.FindAsync(id);
    }

    /// <summary>
    /// 현재 사용자의 잔액을 반환합니다.
    /// </summary>
    /// <returns>사용자 잔액 (인증되지 않았으면 0)</returns>
    public async Task<decimal> GetBalanceAsync()
    {
        if (_authService.CurrentUser == null) return 0;
        await _authService.RefreshUserAsync();
        return _authService.CurrentUser?.Balance ?? 0;
    }

    /// <summary>
    /// 스포츠 베팅을 처리합니다.
    /// 베팅 금액을 차감하고 Bet 레코드를 생성합니다.
    /// 
    /// 처리 과정:
    /// 1. 사용자 인증 확인
    /// 2. 경기 정보 조회 및 유효성 검사
    /// 3. 사용자 잔액 확인
    /// 4. 베팅 타입에 따른 배당률 결정
    /// 5. Bet 레코드 생성 (상태: Pending)
    /// 6. 사용자 잔액 차감
    /// 
    /// 주의사항:
    /// - 베팅은 경기 시작 전에만 가능 (경기 상태가 Upcoming인 경우)
    /// - 베팅 금액은 즉시 차감됨
    /// - 경기 결과에 따라 나중에 승리 금액이 지급됨
    /// </summary>
    /// <param name="matchId">경기 ID</param>
    /// <param name="type">베팅 타입 (홈/무/원정)</param>
    /// <param name="amount">베팅 금액</param>
    /// <returns>베팅 성공 여부</returns>
    public async Task<bool> PlaceBetAsync(int matchId, BetType type, decimal amount)
    {
        // CurrentUser가 null이면 다시 로드 시도
        if (_authService.CurrentUser == null)
        {
            await _authService.LoadUserFromSessionAsync();
        }
        
        // 사용자 인증 확인
        if (_authService.CurrentUser == null) return false;
        
        // 경기 정보 조회
        var match = await GetMatchAsync(matchId);
        if (match == null) return false;

        // 사용자 정보 최신화 및 잔액 확인
        await _authService.RefreshUserAsync();
        var user = _authService.CurrentUser;
        if (user == null || user.Balance < amount) return false;

        // 베팅 타입에 따른 배당률 결정
        var odds = type switch
        {
            BetType.Home => match.HomeOdds,      // 홈팀 승리 배당률
            BetType.Draw => match.DrawOdds,      // 무승부 배당률
            BetType.Away => match.AwayOdds,      // 원정팀 승리 배당률
            _ => 1m                               // 기본값 (발생하지 않아야 함)
        };

        // Bet 레코드 생성
        var bet = new Bet
        {
            UserId = user.Id,
            MatchId = matchId,
            MatchInfo = $"{match.HomeTeam} vs {match.AwayTeam}", // 경기 정보 문자열
            Type = type,
            Odds = odds,
            Amount = amount,
            PotentialWin = amount * odds, // 승리 시 받을 수 있는 금액
            BetTime = DateTime.Now,
            Status = BetStatus.Pending // 경기 결과 대기 중
        };

        // 잔액 차감
        user.Balance -= amount;
        
        // DB에 저장
        _context.Bets.Add(bet);
        await _context.SaveChangesAsync();
        
        // 사용자 정보 최신화
        await _authService.RefreshUserAsync();
        return true;
    }

    /// <summary>
    /// 현재 사용자의 베팅 내역을 조회합니다.
    /// 
    /// 조회 내용:
    /// - 현재 사용자의 모든 베팅 내역
    /// - 경기 정보 포함 (Include를 사용하여 Join)
    /// - 최신순으로 정렬
    /// </summary>
    /// <returns>베팅 내역 목록 (최신순, 경기 정보 포함)</returns>
    public async Task<List<Bet>> GetBetsAsync()
    {
        // 사용자 인증 확인
        if (_authService.CurrentUser == null) return new List<Bet>();
        
        // 현재 사용자의 베팅 내역 조회 (경기 정보 포함, 최신순)
        return await _context.Bets
            .Where(b => b.UserId == _authService.CurrentUser.Id)
            .Include(b => b.Match) // 경기 정보도 함께 조회 (Join)
            .OrderByDescending(b => b.BetTime)
            .ToListAsync();
    }

    /// <summary>
    /// 대기 중인 베팅을 취소하고 베팅 금액을 환불합니다.
    /// 
    /// 취소 조건:
    /// - 베팅 상태가 Pending(대기 중)인 경우만 취소 가능
    /// - 경기가 시작되면 취소 불가
    /// 
    /// 처리 과정:
    /// 1. 베팅 정보 조회 및 유효성 검사
    /// 2. 베팅 상태 확인 (Pending인지 확인)
    /// 3. 사용자 잔액에 베팅 금액 환불
    /// 4. 베팅 레코드 삭제
    /// </summary>
    /// <param name="betId">베팅 ID</param>
    /// <returns>베팅 취소 성공 여부</returns>
    public async Task<bool> RemoveBetAsync(int betId)
    {
        // 사용자 인증 확인
        if (_authService.CurrentUser == null) return false;
        
        // 현재 사용자의 베팅 조회
        var bet = await _context.Bets
            .FirstOrDefaultAsync(b => b.Id == betId && b.UserId == _authService.CurrentUser.Id);
            
        // 베팅이 없거나 이미 처리된 경우 취소 불가
        if (bet == null || bet.Status != BetStatus.Pending) return false;

        // 사용자 정보 최신화
        await _authService.RefreshUserAsync();
        var user = _authService.CurrentUser;
        if (user == null) return false;

        // 베팅 금액 환불
        user.Balance += bet.Amount;
        
        // 베팅 레코드 삭제
        _context.Bets.Remove(bet);
        await _context.SaveChangesAsync();
        
        // 사용자 정보 최신화
        await _authService.RefreshUserAsync();
        return true;
    }

    /// <summary>
    /// 초기 경기 데이터를 생성합니다.
    /// 데이터베이스에 경기가 없을 때만 실행됩니다.
    /// 
    /// 생성되는 경기:
    /// - K리그1 경기 2개
    /// - 프리미어리그 경기 2개
    /// - 라리가 경기 1개
    /// - 분데스리가 경기 1개
    /// 
    /// 각 경기는 예정된 시간과 배당률을 가집니다.
    /// </summary>
    public async Task InitializeMatchesAsync()
    {
        // 이미 경기가 있으면 초기화하지 않음
        if (await _context.Matches.AnyAsync()) return;

        // 초기 경기 데이터 생성
        var matches = new List<Match>
        {
            // K리그1 경기 1
            new Match
            {
                League = "K리그1",
                HomeTeam = "FC서울",
                AwayTeam = "수원삼성",
                MatchTime = DateTime.Now.AddHours(2),
                HomeOdds = 2.10m,  // 홈팀 승리 배당률
                DrawOdds = 3.20m,  // 무승부 배당률
                AwayOdds = 3.50m,  // 원정팀 승리 배당률
                Status = MatchStatus.Upcoming
            },
            // K리그1 경기 2
            new Match
            {
                League = "K리그1",
                HomeTeam = "울산현대",
                AwayTeam = "전북현대",
                MatchTime = DateTime.Now.AddHours(4),
                HomeOdds = 1.85m,
                DrawOdds = 3.40m,
                AwayOdds = 4.20m,
                Status = MatchStatus.Upcoming
            },
            // 프리미어리그 경기 1
            new Match
            {
                League = "프리미어리그",
                HomeTeam = "맨체스터 유나이티드",
                AwayTeam = "리버풀",
                MatchTime = DateTime.Now.AddDays(1),
                HomeOdds = 2.50m,
                DrawOdds = 3.10m,
                AwayOdds = 2.80m,
                Status = MatchStatus.Upcoming
            },
            // 프리미어리그 경기 2
            new Match
            {
                League = "프리미어리그",
                HomeTeam = "아스날",
                AwayTeam = "첼시",
                MatchTime = DateTime.Now.AddDays(1).AddHours(3),
                HomeOdds = 1.95m,
                DrawOdds = 3.30m,
                AwayOdds = 3.90m,
                Status = MatchStatus.Upcoming
            },
            // 라리가 경기
            new Match
            {
                League = "라리가",
                HomeTeam = "레알 마드리드",
                AwayTeam = "바르셀로나",
                MatchTime = DateTime.Now.AddDays(2),
                HomeOdds = 2.20m,
                DrawOdds = 3.50m,
                AwayOdds = 3.00m,
                Status = MatchStatus.Upcoming
            },
            // 분데스리가 경기
            new Match
            {
                League = "분데스리가",
                HomeTeam = "바이에른 뮌헨",
                AwayTeam = "도르트문트",
                MatchTime = DateTime.Now.AddDays(2).AddHours(5),
                HomeOdds = 1.75m,
                DrawOdds = 3.80m,
                AwayOdds = 4.50m,
                Status = MatchStatus.Upcoming
            }
        };

        // DB에 저장
        _context.Matches.AddRange(matches);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// 사용자 랭킹을 조회합니다.
    /// 잔액이 높은 순서대로 정렬됩니다.
    /// 
    /// 정렬 기준:
    /// - 잔액 내림차순 (가장 많은 잔액을 가진 사용자가 1위)
    /// </summary>
    /// <returns>사용자 목록 (잔액 내림차순)</returns>
    public async Task<List<User>> GetUserRankingsAsync()
    {
        return await _context.Users
            .OrderByDescending(u => u.Balance)
            .ToListAsync();
    }
    #endregion
}
