using Microsoft.EntityFrameworkCore;
using ShowMeTheBet.Data;
using ShowMeTheBet.Models;
using Microsoft.AspNetCore.Http;

namespace ShowMeTheBet.Services;

public class GameService
{
    private readonly BettingDbContext _context;
    private readonly AuthService _authService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private DateTime _lastOddEvenResult = DateTime.MinValue;
    private string _currentOddEvenResult = string.Empty;

    public GameService(BettingDbContext context, AuthService authService, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _authService = authService;
        _httpContextAccessor = httpContextAccessor;
    }
    
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

    // 홀짝 게임
    public async Task<string> GetCurrentOddEvenResultAsync()
    {
        var now = DateTime.Now;
        var secondsSinceEpoch = (long)(now - new DateTime(1970, 1, 1)).TotalSeconds;
        var roundNumber = secondsSinceEpoch / 30; // 30초마다 새로운 라운드

        // 라운드 번호를 시드로 사용하여 일관된 결과 생성
        var random = new Random((int)roundNumber);
        _currentOddEvenResult = random.Next(0, 2) == 0 ? "홀" : "짝";
        _lastOddEvenResult = now;

        return _currentOddEvenResult;
    }

    public async Task<int> GetTimeUntilNextRoundAsync()
    {
        var now = DateTime.Now;
        var secondsSinceEpoch = (long)(now - new DateTime(1970, 1, 1)).TotalSeconds;
        var currentRound = secondsSinceEpoch / 30;
        var nextRoundTime = (currentRound + 1) * 30;
        var nextRoundDateTime = new DateTime(1970, 1, 1).AddSeconds(nextRoundTime);
        var timeLeft = (int)(nextRoundDateTime - now).TotalSeconds;
        return timeLeft > 0 ? timeLeft : 30;
    }

    public async Task<bool> PlaceOddEvenBetAsync(string choice, decimal amount, int? userId = null)
    {
        if (choice != "홀" && choice != "짝") return false;

        // userId가 제공되지 않으면 CurrentUser 또는 쿠키에서 가져오기
        if (!userId.HasValue)
        {
            // CurrentUser가 null이면 다시 로드 시도
            if (_authService.CurrentUser == null)
            {
                await _authService.LoadUserFromSessionAsync();
            }
            
            // CurrentUser가 여전히 null이면 쿠키에서 userId 가져오기
            if (_authService.CurrentUser != null)
            {
                userId = _authService.CurrentUser.Id;
            }
            else
            {
                userId = GetUserIdFromCookie();
            }
        }
        
        if (!userId.HasValue) return false;

        // DB에서 직접 사용자 조회
        var trackedUser = await _context.Users.FindAsync(userId.Value);
        if (trackedUser == null || trackedUser.Balance < amount) return false;

        var bet = new GameBet
        {
            UserId = trackedUser.Id,
            GameType = GameType.OddEven,
            BetChoice = choice,
            Amount = amount,
            Multiplier = 2.0m,
            WinAmount = amount * 2.0m,
            BetTime = DateTime.Now,
            Status = GameBetStatus.Pending
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

        // 변경 추적 초기화 후 다시 로드
        _context.Entry(trackedUser).State = Microsoft.EntityFrameworkCore.EntityState.Detached;

        // 결과 확인 (30초 라운드 기준)
        await CheckOddEvenResultsAsync();

        await _authService.RefreshUserAsync();
        return true;
    }

    private async Task CheckOddEvenResultsAsync()
    {
        var pendingBets = await _context.GameBets
            .Where(b => b.GameType == GameType.OddEven && b.Status == GameBetStatus.Pending)
            .ToListAsync();

        foreach (var bet in pendingBets)
        {
            var betRound = (long)(bet.BetTime - new DateTime(1970, 1, 1)).TotalSeconds / 30;
            var currentRound = (long)(DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds / 30;

            // 다음 라운드가 시작되면 결과 확인
            if (currentRound > betRound)
            {
                // 결과 확인 (베팅한 다음 라운드의 결과)
                var result = await GetResultForRoundAsync(betRound + 1); // 베팅한 다음 라운드 결과
                bet.Result = result;
                
                if (bet.BetChoice == result)
                {
                    bet.Status = GameBetStatus.Won;
                    var user = await _context.Users.FindAsync(bet.UserId);
                    if (user != null)
                    {
                        user.Balance += bet.WinAmount;
                    }
                }
                else
                {
                    bet.Status = GameBetStatus.Lost;
                }
            }
        }

        await _context.SaveChangesAsync();
    }

    private async Task<string> GetResultForRoundAsync(long roundNumber)
    {
        var random = new Random((int)roundNumber);
        return random.Next(0, 2) == 0 ? "홀" : "짝";
    }

    public async Task<List<GameBet>> GetOddEvenBetsAsync(int? userId = null)
    {
        // userId가 제공되지 않으면 CurrentUser 또는 쿠키에서 가져오기
        if (!userId.HasValue)
        {
            if (_authService.CurrentUser == null)
            {
                await _authService.LoadUserFromSessionAsync();
            }
            
            if (_authService.CurrentUser != null)
            {
                userId = _authService.CurrentUser.Id;
            }
            else
            {
                userId = GetUserIdFromCookie();
            }
        }
        
        if (!userId.HasValue) return new List<GameBet>();

        await CheckOddEvenResultsAsync();

        return await _context.GameBets
            .Where(b => b.UserId == userId.Value && b.GameType == GameType.OddEven)
            .OrderByDescending(b => b.BetTime)
            .ToListAsync();
    }

    // 그래프 게임
    public async Task<bool> StartGraphGameAsync(decimal amount, int? userId = null)
    {
        try
        {
            // userId가 제공되지 않으면 CurrentUser 또는 쿠키에서 가져오기
            if (!userId.HasValue)
            {
                // CurrentUser가 null이면 다시 로드 시도
                if (_authService.CurrentUser == null)
                {
                    await _authService.LoadUserFromSessionAsync();
                }
                
                // CurrentUser가 여전히 null이면 쿠키에서 userId 가져오기
                if (_authService.CurrentUser != null)
                {
                    userId = _authService.CurrentUser.Id;
                }
                else
                {
                    userId = GetUserIdFromCookie();
                }
            }
            
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

            // 변경 추적 초기화 후 다시 로드
            _context.Entry(trackedUser).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
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

    public async Task<bool> StopGraphGameAsync(decimal amount, decimal multiplier, int? userId = null)
    {
        if (multiplier < 1.0m || multiplier > 5.0m) return false;

        // userId가 제공되지 않으면 CurrentUser 또는 쿠키에서 가져오기
        if (!userId.HasValue)
        {
            // CurrentUser가 null이면 다시 로드 시도
            if (_authService.CurrentUser == null)
            {
                await _authService.LoadUserFromSessionAsync();
            }
            
            // CurrentUser가 여전히 null이면 쿠키에서 userId 가져오기
            if (_authService.CurrentUser != null)
            {
                userId = _authService.CurrentUser.Id;
            }
            else
            {
                userId = GetUserIdFromCookie();
            }
        }
        
        if (!userId.HasValue) return false;

        // DB에서 직접 사용자 조회
        var user = await _context.Users.FindAsync(userId.Value);
        if (user == null) return false;

        var winAmount = amount * multiplier;

        var bet = new GameBet
        {
            UserId = user.Id,
            GameType = GameType.Graph,
            BetChoice = multiplier.ToString("F2"),
            Amount = amount,
            Multiplier = multiplier,
            WinAmount = winAmount,
            BetTime = DateTime.Now,
            Status = GameBetStatus.Won,
            Result = $"x{multiplier:F2}"
        };

        user.Balance += winAmount; // 승리 금액 추가

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

        await _authService.RefreshUserAsync();
        return true;
    }

    public async Task<bool> FailGraphGameAsync(decimal amount)
    {
        // CurrentUser가 null이면 다시 로드 시도
        if (_authService.CurrentUser == null)
        {
            await _authService.LoadUserFromSessionAsync();
        }
        
        // CurrentUser가 여전히 null이면 쿠키에서 userId 가져오기
        int? userId = null;
        if (_authService.CurrentUser != null)
        {
            userId = _authService.CurrentUser.Id;
        }
        else
        {
            userId = GetUserIdFromCookie();
        }
        
        if (!userId.HasValue) return false;

        // DB에서 직접 사용자 조회
        var user = await _context.Users.FindAsync(userId.Value);
        if (user == null) return false;

        var bet = new GameBet
        {
            UserId = user.Id,
            GameType = GameType.Graph,
            BetChoice = "실패",
            Amount = amount,
            Multiplier = 0m,
            WinAmount = 0m,
            BetTime = DateTime.Now,
            Status = GameBetStatus.Lost,
            Result = "실패"
        };

        // 베팅 금액은 이미 차감되었으므로 추가 차감 없음
        _context.GameBets.Add(bet);
        await _context.SaveChangesAsync();

        await _authService.RefreshUserAsync();
        return true;
    }

    public async Task<List<GameBet>> GetGraphBetsAsync(int? userId = null)
    {
        // userId가 제공되지 않으면 CurrentUser 또는 쿠키에서 가져오기
        if (!userId.HasValue)
        {
            if (_authService.CurrentUser == null)
            {
                await _authService.LoadUserFromSessionAsync();
            }
            
            if (_authService.CurrentUser != null)
            {
                userId = _authService.CurrentUser.Id;
            }
            else
            {
                userId = GetUserIdFromCookie();
            }
        }
        
        if (!userId.HasValue) return new List<GameBet>();

        return await _context.GameBets
            .Where(b => b.UserId == userId.Value && b.GameType == GameType.Graph)
            .OrderByDescending(b => b.BetTime)
            .ToListAsync();
    }

    public async Task<decimal> GetBalanceAsync()
    {
        if (_authService.CurrentUser == null) return 0;
        await _authService.RefreshUserAsync();
        return _authService.CurrentUser?.Balance ?? 0;
    }
}

