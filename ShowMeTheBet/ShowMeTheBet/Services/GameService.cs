using Microsoft.EntityFrameworkCore;
using ShowMeTheBet.Data;
using ShowMeTheBet.Models;
using Microsoft.AspNetCore.Http;

namespace ShowMeTheBet.Services;

/// <summary>
/// 게임 관련 비즈니스 로직을 처리하는 서비스 클래스
/// 홀짝 게임, 그래프 게임 등의 게임 로직과 베팅 처리를 담당합니다.
/// 
/// 주요 기능:
/// - 홀짝 게임: 30초마다 새로운 라운드가 시작되는 게임
/// - 그래프 게임: 배수가 1.0에서 5.0까지 증가하는 게임
/// - 베팅 내역 조회 및 관리
/// </summary>
public class GameService
{
    #region Private Fields
    /// <summary>
    /// 데이터베이스 컨텍스트 - 게임 베팅 정보 저장 및 조회
    /// </summary>
    private readonly BettingDbContext _context;
    
    /// <summary>
    /// 인증 서비스 - 사용자 정보 관리
    /// </summary>
    private readonly AuthService _authService;
    
    /// <summary>
    /// HTTP 컨텍스트 접근자 - 쿠키에서 userId 가져오기용
    /// </summary>
    private readonly IHttpContextAccessor _httpContextAccessor;
    private static readonly RouletteSlot[] _rouletteSlots = new[]
    {
        new RouletteSlot("꽝", 0m),
        new RouletteSlot("꽝", 0m),
        new RouletteSlot("꽝", 0m),
        new RouletteSlot("꽝", 0m),
        new RouletteSlot("x2", 2m),
        new RouletteSlot("x3", 3m),
        new RouletteSlot("x4", 4m),
        new RouletteSlot("x5", 5m)
    };
    
    /// <summary>
    /// 마지막 홀짝 결과 생성 시간 (캐싱용)
    /// </summary>
    private DateTime _lastOddEvenResult = DateTime.MinValue;
    
    /// <summary>
    /// 현재 홀짝 결과 (캐싱용)
    /// </summary>
    private string _currentOddEvenResult = string.Empty;
    #endregion

    #region Constructor
    /// <summary>
    /// GameService 생성자
    /// </summary>
    /// <param name="context">데이터베이스 컨텍스트</param>
    /// <param name="authService">인증 서비스</param>
    /// <param name="httpContextAccessor">HTTP 컨텍스트 접근자</param>
    public GameService(BettingDbContext context, AuthService authService, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _authService = authService;
        _httpContextAccessor = httpContextAccessor;
    }
    #endregion

    #region Private Helper Methods
    /// <summary>
    /// 쿠키에서 사용자 ID를 가져오는 헬퍼 메서드
    /// IIS 환경에서 안정적으로 작동하도록 쿠키를 사용합니다.
    /// </summary>
    /// <returns>사용자 ID (없으면 null)</returns>
    private int? GetUserIdFromCookie()
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                System.Diagnostics.Debug.WriteLine("[GetUserIdFromCookie] HttpContext가 null");
                return null;
            }
            
            // 쿠키에서 UserId 읽기
            if (httpContext.Request.Cookies.TryGetValue("UserId", out var userIdCookie))
            {
                if (int.TryParse(userIdCookie, out var userId) && userId > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[GetUserIdFromCookie] 쿠키에서 userId 찾음: {userId}");
                    return userId;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[GetUserIdFromCookie] 쿠키 값 파싱 실패: {userIdCookie}");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[GetUserIdFromCookie] UserId 쿠키를 찾을 수 없음");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GetUserIdFromCookie] 예외 발생: {ex.Message}");
        }
        return null;
    }

    /// <summary>
    /// 사용자 ID를 가져오는 통합 메서드
    /// 여러 방법을 시도하여 userId를 찾습니다:
    /// 1. 매개변수로 제공된 userId 사용
    /// 2. AuthService.CurrentUser에서 가져오기
    /// 3. 쿠키에서 가져오기
    /// </summary>
    /// <param name="userId">제공된 사용자 ID (선택사항)</param>
    /// <returns>사용자 ID (없으면 null)</returns>
    private async Task<int?> GetUserIdAsync(int? userId = null)
    {
        // 1. 매개변수로 제공된 userId가 있으면 사용
        if (userId.HasValue)
        {
            return userId.Value;
        }

        // 2. CurrentUser가 null이면 다시 로드 시도
        if (_authService.CurrentUser == null)
        {
            await _authService.LoadUserFromSessionAsync();
        }
        
        // 3. CurrentUser에서 가져오기
        if (_authService.CurrentUser != null)
        {
            return _authService.CurrentUser.Id;
        }
        
        // 4. 쿠키에서 가져오기 (최후의 수단)
        return GetUserIdFromCookie();
    }
    #endregion

    #region 홀짝 게임 관련 메서드
    /// <summary>
    /// 현재 라운드의 홀짝 결과를 반환합니다.
    /// 30초마다 새로운 라운드가 시작되며, 라운드 번호를 시드로 사용하여 일관된 결과를 생성합니다.
    /// 
    /// 동작 원리:
    /// - Unix 타임스탬프를 30초로 나눈 값을 라운드 번호로 사용
    /// - 라운드 번호를 Random 시드로 사용하여 동일한 라운드에서는 항상 같은 결과 반환
    /// - 이를 통해 모든 사용자가 같은 라운드에서 같은 결과를 보게 됨
    /// </summary>
    /// <returns>"홀" 또는 "짝"</returns>
    public async Task<string> GetCurrentOddEvenResultAsync()
    {
        var now = DateTime.Now;
        // Unix 타임스탬프 계산 (1970년 1월 1일부터 경과한 초)
        var secondsSinceEpoch = (long)(now - new DateTime(1970, 1, 1)).TotalSeconds;
        // 30초마다 새로운 라운드
        var roundNumber = secondsSinceEpoch / 30;

        // 라운드 번호를 시드로 사용하여 일관된 결과 생성
        // 같은 라운드 번호는 항상 같은 결과를 생성함
        var random = new Random((int)roundNumber);
        _currentOddEvenResult = random.Next(0, 2) == 0 ? "홀" : "짝";
        _lastOddEvenResult = now;

        return _currentOddEvenResult;
    }

    /// <summary>
    /// 다음 라운드까지 남은 시간(초)을 반환합니다.
    /// 홀짝 게임은 30초마다 새로운 라운드가 시작됩니다.
    /// 
    /// 계산 방법:
    /// - 현재 시간을 Unix 타임스탬프로 변환
    /// - 30초 단위로 나누어 현재 라운드 계산
    /// - 다음 라운드 시간에서 현재 시간을 빼서 남은 시간 계산
    /// </summary>
    /// <returns>남은 시간(초) - 최소 1초, 최대 30초</returns>
    public async Task<int> GetTimeUntilNextRoundAsync()
    {
        var now = DateTime.Now;
        var secondsSinceEpoch = (long)(now - new DateTime(1970, 1, 1)).TotalSeconds;
        var currentRound = secondsSinceEpoch / 30;
        var nextRoundTime = (currentRound + 1) * 30;
        var nextRoundDateTime = new DateTime(1970, 1, 1).AddSeconds(nextRoundTime);
        var timeLeft = (int)(nextRoundDateTime - now).TotalSeconds;
        // 0 이하가 되면 30초로 설정 (안전장치)
        return timeLeft > 0 ? timeLeft : 30;
    }

    /// <summary>
    /// 홀짝 게임 베팅을 처리합니다.
    /// 베팅 금액을 차감하고 GameBet 레코드를 생성합니다.
    /// 결과는 다음 라운드가 시작될 때 확인됩니다.
    /// 
    /// 처리 과정:
    /// 1. 베팅 선택 유효성 검사 ("홀" 또는 "짝"만 허용)
    /// 2. 사용자 ID 확인 (여러 방법 시도)
    /// 3. 사용자 잔액 확인 및 차감
    /// 4. GameBet 레코드 생성 (상태: Pending)
    /// 5. 대기 중인 베팅 결과 확인
    /// </summary>
    /// <param name="choice">베팅 선택 ("홀" 또는 "짝")</param>
    /// <param name="amount">베팅 금액</param>
    /// <param name="userId">사용자 ID (없으면 CurrentUser 또는 쿠키에서 가져옴)</param>
    /// <returns>베팅 성공 여부</returns>
    public async Task<bool> PlaceOddEvenBetAsync(string choice, decimal amount, int? userId = null)
    {
        // 베팅 선택 유효성 검사
        if (choice != "홀" && choice != "짝") return false;

        // 사용자 ID 가져오기 (통합 메서드 사용)
        userId = await GetUserIdAsync(userId);
        if (!userId.HasValue) return false;

        // DB에서 직접 사용자 조회 (Entity Framework 변경 추적을 위해)
        var trackedUser = await _context.Users.FindAsync(userId.Value);
        if (trackedUser == null || trackedUser.Balance < amount) return false;

        // GameBet 레코드 생성
        var bet = new GameBet
        {
            UserId = trackedUser.Id,
            GameType = GameType.OddEven,
            BetChoice = choice,
            Amount = amount,
            Multiplier = 2.0m, // 홀짝 게임은 항상 x2.0 배당
            WinAmount = amount * 2.0m, // 승리 시 받을 금액
            BetTime = DateTime.Now,
            Status = GameBetStatus.Pending // 결과는 다음 라운드에서 확인
        };

        // 잔액 차감
        trackedUser.Balance -= amount;
        _context.GameBets.Add(bet);
        
        try
        {
            await _context.SaveChangesAsync();
        }
        catch
        {
            // 저장 실패 시 원래 상태로 복구
            await _context.Entry(trackedUser).ReloadAsync();
            return false;
        }

        // 변경 추적 초기화 (다음 조회 시 최신 데이터를 가져오기 위해)
        _context.Entry(trackedUser).State = Microsoft.EntityFrameworkCore.EntityState.Detached;

        // 대기 중인 베팅 결과 확인 (이전 라운드의 베팅이 결과를 받을 수 있는지 확인)
        await CheckOddEvenResultsAsync();

        // 사용자 정보 최신화
        await _authService.RefreshUserAsync();
        return true;
    }

    /// <summary>
    /// 대기 중인 홀짝 게임 베팅의 결과를 확인하고 처리합니다.
    /// 
    /// 처리 로직:
    /// - Pending 상태인 모든 홀짝 게임 베팅을 조회
    /// - 각 베팅의 라운드와 현재 라운드를 비교
    /// - 다음 라운드가 시작되었으면 결과 확인
    /// - 승리한 경우 잔액에 승리 금액 추가
    /// </summary>
    private async Task CheckOddEvenResultsAsync()
    {
        // Pending 상태인 모든 홀짝 게임 베팅 조회
        var pendingBets = await _context.GameBets
            .Where(b => b.GameType == GameType.OddEven && b.Status == GameBetStatus.Pending)
            .ToListAsync();

        foreach (var bet in pendingBets)
        {
            // 베팅한 라운드 번호 계산
            var betRound = (long)(bet.BetTime - new DateTime(1970, 1, 1)).TotalSeconds / 30;
            // 현재 라운드 번호 계산
            var currentRound = (long)(DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds / 30;

            // 다음 라운드가 시작되면 결과 확인
            // (베팅한 다음 라운드의 결과로 승패 결정)
            if (currentRound > betRound)
            {
                // 베팅한 다음 라운드의 결과 확인
                var result = await GetResultForRoundAsync(betRound + 1);
                bet.Result = result;
                
                // 베팅 선택과 결과가 일치하면 승리
                if (bet.BetChoice == result)
                {
                    bet.Status = GameBetStatus.Won;
                    var user = await _context.Users.FindAsync(bet.UserId);
                    if (user != null)
                    {
                        // 승리 금액 추가
                        user.Balance += bet.WinAmount;
                    }
                }
                else
                {
                    // 베팅 선택과 결과가 다르면 패배
                    bet.Status = GameBetStatus.Lost;
                }
            }
        }

        // 변경사항 저장
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// 특정 라운드의 홀짝 결과를 반환합니다.
    /// 라운드 번호를 시드로 사용하여 일관된 결과를 생성합니다.
    /// </summary>
    /// <param name="roundNumber">라운드 번호</param>
    /// <returns>"홀" 또는 "짝"</returns>
    private async Task<string> GetResultForRoundAsync(long roundNumber)
    {
        var random = new Random((int)roundNumber);
        return random.Next(0, 2) == 0 ? "홀" : "짝";
    }

    /// <summary>
    /// 홀짝 게임 베팅 내역을 조회합니다.
    /// </summary>
    /// <param name="userId">사용자 ID (없으면 CurrentUser 또는 쿠키에서 가져옴)</param>
    /// <returns>홀짝 게임 베팅 내역 목록 (최신순 정렬)</returns>
    public async Task<List<GameBet>> GetOddEvenBetsAsync(int? userId = null)
    {
        // 사용자 ID 가져오기 (통합 메서드 사용)
        userId = await GetUserIdAsync(userId);
        if (!userId.HasValue) return new List<GameBet>();

        // 대기 중인 베팅 결과 확인
        await CheckOddEvenResultsAsync();

        // 해당 사용자의 홀짝 게임 베팅 내역 조회 (최신순)
        return await _context.GameBets
            .Where(b => b.UserId == userId.Value && b.GameType == GameType.OddEven)
            .OrderByDescending(b => b.BetTime)
            .ToListAsync();
    }

    #endregion

    #region 그래프 게임 관련 메서드
    /// <summary>
    /// 그래프 게임을 시작합니다.
    /// 베팅 금액을 차감하고 게임을 시작합니다. 게임은 클라이언트 측에서 실행되며,
    /// 사용자가 STOP 버튼을 누르거나 게임이 실패할 때까지 배수가 증가합니다.
    /// 
    /// 처리 과정:
    /// 1. 사용자 ID 확인
    /// 2. 사용자 잔액 확인 및 차감
    /// 3. 잔액 차감 후 DB 저장
    /// 4. Entity Framework 변경 추적 초기화
    /// 
    /// 주의사항:
    /// - 게임 시작 시 베팅 금액이 즉시 차감됨
    /// - 게임이 실패하면 베팅 금액은 환불되지 않음
    /// - 게임이 성공하면 StopGraphGameAsync에서 승리 금액 지급
    /// </summary>
    /// <param name="amount">베팅 금액</param>
    /// <param name="userId">사용자 ID (없으면 CurrentUser 또는 쿠키에서 가져옴)</param>
    /// <returns>게임 시작 성공 여부</returns>
    public async Task<bool> StartGraphGameAsync(decimal amount, int? userId = null)
    {
        try
        {
            // 사용자 ID 가져오기 (통합 메서드 사용)
            userId = await GetUserIdAsync(userId);
            if (!userId.HasValue)
            {
                System.Diagnostics.Debug.WriteLine($"[StartGraphGameAsync] userId를 찾을 수 없음");
                return false;
            }

            System.Diagnostics.Debug.WriteLine($"[StartGraphGameAsync] userId: {userId.Value}, amount: {amount}");

            // DB에서 직접 사용자 조회
            var trackedUser = await _context.Users.FindAsync(userId.Value);
            if (trackedUser == null)
            {
                System.Diagnostics.Debug.WriteLine($"[StartGraphGameAsync] 사용자를 찾을 수 없음: userId {userId.Value}");
                return false;
            }
            
            // 잔액 확인
            if (trackedUser.Balance < amount)
            {
                System.Diagnostics.Debug.WriteLine($"[StartGraphGameAsync] 잔액 부족: {trackedUser.Balance} < {amount}");
                return false;
            }
            
            System.Diagnostics.Debug.WriteLine($"[StartGraphGameAsync] 사용자 확인 완료: {trackedUser.Username}, 잔액: {trackedUser.Balance}");
        
            // 잔액 차감
            trackedUser.Balance -= amount;
            
            System.Diagnostics.Debug.WriteLine($"[StartGraphGameAsync] 잔액 차감 후: {trackedUser.Balance}");
            
            try
            {
                // 변경사항 저장
                await _context.SaveChangesAsync();
                System.Diagnostics.Debug.WriteLine($"[StartGraphGameAsync] DB 저장 성공");
            }
            catch (Exception ex)
            {
                // 저장 실패 시 원래 상태로 복구
                System.Diagnostics.Debug.WriteLine($"[StartGraphGameAsync] DB 저장 실패: {ex.Message}");
                await _context.Entry(trackedUser).ReloadAsync();
                return false;
            }

            // 변경 추적 초기화 (다음 조회 시 최신 데이터를 가져오기 위해)
            _context.Entry(trackedUser).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
            
            // 사용자 정보 최신화
            await _authService.RefreshUserAsync();
            System.Diagnostics.Debug.WriteLine($"[StartGraphGameAsync] 성공");
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[StartGraphGameAsync] 예외 발생: {ex.Message}, StackTrace: {ex.StackTrace}");
            return false;
        }
    }

    /// <summary>
    /// 그래프 게임을 중지하고 승리 금액을 지급합니다.
    /// 사용자가 STOP 버튼을 눌렀을 때 호출됩니다.
    /// 
    /// 처리 과정:
    /// 1. 배수 유효성 검사 (1.0 ~ 5.0)
    /// 2. 사용자 ID 확인
    /// 3. 승리 금액 계산 (베팅 금액 × 배수)
    /// 4. GameBet 레코드 생성 (상태: Won)
    /// 5. 사용자 잔액에 승리 금액 추가
    /// 
    /// 주의사항:
    /// - 베팅 금액은 이미 StartGraphGameAsync에서 차감되었음
    /// - 승리 금액만 추가하면 됨
    /// </summary>
    /// <param name="amount">베팅 금액</param>
    /// <param name="multiplier">현재 배수 (1.0 ~ 5.0)</param>
    /// <param name="userId">사용자 ID (없으면 CurrentUser 또는 쿠키에서 가져옴)</param>
    /// <returns>게임 중지 및 승리 금액 지급 성공 여부</returns>
    public async Task<bool> StopGraphGameAsync(decimal amount, decimal multiplier, int? userId = null)
    {
        // 배수 유효성 검사
        if (multiplier < 1.0m || multiplier > 5.0m) return false;

        // 사용자 ID 가져오기 (통합 메서드 사용)
        userId = await GetUserIdAsync(userId);
        if (!userId.HasValue) return false;

        // DB에서 직접 사용자 조회
        var user = await _context.Users.FindAsync(userId.Value);
        if (user == null) return false;

        // 승리 금액 계산
        var winAmount = amount * multiplier;

        // GameBet 레코드 생성
        var bet = new GameBet
        {
            UserId = user.Id,
            GameType = GameType.Graph,
            BetChoice = multiplier.ToString("F2"), // 배수를 문자열로 저장
            Amount = amount,
            Multiplier = multiplier,
            WinAmount = winAmount,
            BetTime = DateTime.Now,
            Status = GameBetStatus.Won, // STOP 버튼을 눌렀으므로 승리
            Result = $"x{multiplier:F2}" // 결과 표시용
        };

        // 승리 금액 추가 (베팅 금액은 이미 차감되었음)
        user.Balance += winAmount;

        _context.GameBets.Add(bet);
        
        try
        {
            await _context.SaveChangesAsync();
            System.Diagnostics.Debug.WriteLine($"[StopGraphGameAsync] 성공: userId={userId.Value}, winAmount={winAmount}, newBalance={user.Balance}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[StopGraphGameAsync] DB 저장 실패: {ex.Message}");
            return false;
        }

        // 사용자 정보 최신화
        await _authService.RefreshUserAsync();
        return true;
    }

    /// <summary>
    /// 그래프 게임 실패를 처리합니다.
    /// 게임이 실패하면 베팅 금액은 이미 차감되었으므로 추가 차감 없이 GameBet 레코드만 생성합니다.
    /// 
    /// 처리 과정:
    /// 1. 사용자 ID 확인
    /// 2. GameBet 레코드 생성 (상태: Lost)
    /// 3. 베팅 금액은 이미 차감되었으므로 추가 처리 없음
    /// </summary>
    /// <param name="amount">베팅 금액</param>
    /// <returns>실패 처리 성공 여부</returns>
    public async Task<bool> FailGraphGameAsync(decimal amount)
    {
        // 사용자 ID 가져오기 (통합 메서드 사용)
        var userId = await GetUserIdAsync();
        if (!userId.HasValue) return false;

        // DB에서 직접 사용자 조회
        var user = await _context.Users.FindAsync(userId.Value);
        if (user == null) return false;

        // GameBet 레코드 생성 (실패)
        var bet = new GameBet
        {
            UserId = user.Id,
            GameType = GameType.Graph,
            BetChoice = "실패",
            Amount = amount,
            Multiplier = 0m, // 실패 시 배수는 0
            WinAmount = 0m, // 실패 시 승리 금액은 0
            BetTime = DateTime.Now,
            Status = GameBetStatus.Lost, // 게임 실패
            Result = "실패"
        };

        // 베팅 금액은 이미 StartGraphGameAsync에서 차감되었으므로 추가 차감 없음
        _context.GameBets.Add(bet);
        await _context.SaveChangesAsync();

        // 사용자 정보 최신화
        await _authService.RefreshUserAsync();
        return true;
    }

    /// <summary>
    /// 그래프 게임 베팅 내역을 조회합니다.
    /// </summary>
    /// <param name="userId">사용자 ID (없으면 CurrentUser 또는 쿠키에서 가져옴)</param>
    /// <returns>그래프 게임 베팅 내역 목록 (최신순 정렬)</returns>
    public async Task<List<GameBet>> GetGraphBetsAsync(int? userId = null)
    {
        // 사용자 ID 가져오기 (통합 메서드 사용)
        userId = await GetUserIdAsync(userId);
        if (!userId.HasValue) return new List<GameBet>();

        // 해당 사용자의 그래프 게임 베팅 내역 조회 (최신순)
        return await _context.GameBets
            .Where(b => b.UserId == userId.Value && b.GameType == GameType.Graph)
            .OrderByDescending(b => b.BetTime)
            .ToListAsync();
    }

    /// <summary>
    /// 룰렛 게임을 실행합니다.
    /// </summary>
    public async Task<RouletteSpinResult> PlayRouletteAsync(decimal amount, int? userId = null)
    {
        if (amount < 1000m)
        {
            return RouletteSpinResult.Fail("최소 베팅 금액은 1,000원입니다.");
        }

        userId = await GetUserIdAsync(userId);
        if (!userId.HasValue)
        {
            return RouletteSpinResult.Fail("사용자 정보를 찾을 수 없습니다.");
        }

        var user = await _context.Users.FindAsync(userId.Value);
        if (user == null)
        {
            return RouletteSpinResult.Fail("사용자를 찾을 수 없습니다.");
        }

        if (user.Balance < amount)
        {
            return RouletteSpinResult.Fail("잔액이 부족합니다.", user.Balance);
        }

        user.Balance -= amount;

        var slot = _rouletteSlots[Random.Shared.Next(_rouletteSlots.Length)];
        var winAmount = slot.Multiplier > 0 ? amount * slot.Multiplier : 0m;
        var status = slot.Multiplier > 0 ? GameBetStatus.Won : GameBetStatus.Lost;

        if (status == GameBetStatus.Won)
        {
            user.Balance += winAmount;
        }

        var bet = new GameBet
        {
            UserId = user.Id,
            GameType = GameType.Roulette,
            BetChoice = slot.Label,
            Amount = amount,
            Multiplier = slot.Multiplier,
            WinAmount = winAmount,
            BetTime = DateTime.Now,
            Status = status,
            Result = slot.Label
        };

        _context.GameBets.Add(bet);
        await _context.SaveChangesAsync();
        await _authService.RefreshUserAsync();

        var message = status == GameBetStatus.Won
            ? $"축하합니다! {slot.Label}에 당첨되었습니다."
            : "아쉽지만 꽝입니다.";

        return new RouletteSpinResult(
            true,
            status == GameBetStatus.Won,
            slot.Label,
            slot.Multiplier,
            winAmount,
            user.Balance,
            message);
    }

    /// <summary>
    /// 룰렛 베팅 내역을 조회합니다.
    /// </summary>
    public async Task<List<GameBet>> GetRouletteBetsAsync(int? userId = null)
    {
        userId = await GetUserIdAsync(userId);
        if (!userId.HasValue) return new List<GameBet>();

        return await _context.GameBets
            .Where(b => b.UserId == userId.Value && b.GameType == GameType.Roulette)
            .OrderByDescending(b => b.BetTime)
            .ToListAsync();
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
    #endregion

    private record RouletteSlot(string Label, decimal Multiplier);
}
