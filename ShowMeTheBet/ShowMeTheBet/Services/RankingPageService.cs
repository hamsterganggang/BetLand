using ShowMeTheBet.Models;

namespace ShowMeTheBet.Services;

/// <summary>
/// 부자 랭킹 페이지용 서비스.
/// </summary>
public class RankingPageService
{
    private readonly BettingService _bettingService;
    private readonly AuthService _authService;

    public RankingPageService(BettingService bettingService, AuthService authService)
    {
        _bettingService = bettingService;
        _authService = authService;
    }

    public Task<List<User>> GetRankingsAsync() => _bettingService.GetUserRankingsAsync();

    public bool IsCurrentUser(User rankingUser) =>
        _authService.IsAuthenticated && _authService.CurrentUser?.Id == rankingUser.Id;
}

