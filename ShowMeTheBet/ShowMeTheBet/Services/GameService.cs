using Microsoft.EntityFrameworkCore;
using ShowMeTheBet.Data;
using ShowMeTheBet.Models;

namespace ShowMeTheBet.Services;

public class GameService
{
    private readonly BettingDbContext _context;
    private readonly AuthService _authService;
    private DateTime _lastOddEvenResult = DateTime.MinValue;
    private string _currentOddEvenResult = string.Empty;

    public GameService(BettingDbContext context, AuthService authService)
    {
        _context = context;
        _authService = authService;
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

    public async Task<bool> PlaceOddEvenBetAsync(string choice, decimal amount)
    {
        if (_authService.CurrentUser == null) return false;
        if (choice != "홀" && choice != "짝") return false;

        await _authService.RefreshUserAsync();
        var user = _authService.CurrentUser;
        if (user == null || user.Balance < amount) return false;

        var bet = new GameBet
        {
            UserId = user.Id,
            GameType = GameType.OddEven,
            BetChoice = choice,
            Amount = amount,
            Multiplier = 2.0m,
            WinAmount = amount * 2.0m,
            BetTime = DateTime.Now,
            Status = GameBetStatus.Pending
        };

        user.Balance -= amount;
        _context.GameBets.Add(bet);
        await _context.SaveChangesAsync();

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

    public async Task<List<GameBet>> GetOddEvenBetsAsync()
    {
        if (_authService.CurrentUser == null) return new List<GameBet>();

        await CheckOddEvenResultsAsync();

        return await _context.GameBets
            .Where(b => b.UserId == _authService.CurrentUser.Id && b.GameType == GameType.OddEven)
            .OrderByDescending(b => b.BetTime)
            .ToListAsync();
    }

    // 그래프 게임
    public async Task<bool> StartGraphGameAsync(decimal amount)
    {
        if (_authService.CurrentUser == null) return false;

        await _authService.RefreshUserAsync();
        var user = _authService.CurrentUser;
        if (user == null || user.Balance < amount) return false;

        // 베팅 금액 차감
        user.Balance -= amount;
        await _context.SaveChangesAsync();

        await _authService.RefreshUserAsync();
        return true;
    }

    public async Task<bool> StopGraphGameAsync(decimal amount, decimal multiplier)
    {
        if (_authService.CurrentUser == null) return false;
        if (multiplier < 1.0m || multiplier > 5.0m) return false;

        await _authService.RefreshUserAsync();
        var user = _authService.CurrentUser;
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
        await _context.SaveChangesAsync();

        await _authService.RefreshUserAsync();
        return true;
    }

    public async Task<bool> FailGraphGameAsync(decimal amount)
    {
        if (_authService.CurrentUser == null) return false;

        await _authService.RefreshUserAsync();
        var user = _authService.CurrentUser;
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

    public async Task<List<GameBet>> GetGraphBetsAsync()
    {
        if (_authService.CurrentUser == null) return new List<GameBet>();

        return await _context.GameBets
            .Where(b => b.UserId == _authService.CurrentUser.Id && b.GameType == GameType.Graph)
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

