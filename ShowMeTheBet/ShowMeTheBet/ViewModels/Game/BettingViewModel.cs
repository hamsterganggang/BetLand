using Microsoft.AspNetCore.Components;
using ShowMeTheBet.Services;
using ShowMeTheBet.ViewModels.Base;

namespace ShowMeTheBet.ViewModels.Game;

/// <summary>
/// 게임 선택 페이지(View: Betting.razor)의 ViewModel
/// </summary>
public class BettingViewModel : GamePageViewModel
{
    private readonly NavigationManager _navigationManager;

    public BettingViewModel(
        AuthService authService,
        AuthHelperService authHelper,
        NavigationManager navigationManager) : base(authService, authHelper)
    {
        _navigationManager = navigationManager;
    }

    public Task InitializeAsync() => base.InitializeAsync();

    public void NavigateToSports() => Navigate("/sports");
    public void NavigateToOddEven() => Navigate("/oddeven");
    public void NavigateToGraph() => Navigate("/graph");
    public void NavigateToRoulette() => Navigate("/roulette");

    private void Navigate(string uri)
    {
        try
        {
            _navigationManager.NavigateTo(uri, forceLoad: false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Navigation error to '{uri}': {ex.Message}");
        }
    }
}

