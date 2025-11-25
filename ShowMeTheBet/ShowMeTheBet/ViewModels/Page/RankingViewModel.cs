using ShowMeTheBet.Models;
using ShowMeTheBet.Services;
using ShowMeTheBet.ViewModels.Base;

namespace ShowMeTheBet.ViewModels.Page;

/// <summary>
/// 부자 랭킹 페이지 ViewModel.
/// </summary>
public class RankingViewModel : BaseViewModel
{
    private readonly RankingPageService _rankingService;

    private List<User>? _rankings;

    public RankingViewModel(RankingPageService rankingService)
    {
        _rankingService = rankingService;
    }

    public List<User>? Rankings
    {
        get => _rankings;
        private set => SetProperty(ref _rankings, value);
    }

    public async Task InitializeAsync()
    {
        await LoadAsync();
    }

    public async Task LoadAsync()
    {
        Rankings = await _rankingService.GetRankingsAsync();
    }

    public bool IsCurrentUser(User user) => _rankingService.IsCurrentUser(user);
}

