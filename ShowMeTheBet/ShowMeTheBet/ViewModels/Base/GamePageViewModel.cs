using ShowMeTheBet.Services;

namespace ShowMeTheBet.ViewModels.Base;

/// <summary>
/// 게임 관련 페이지(View)에서 공통으로 사용하는 ViewModel
/// - 인증/잔액 상태 관리
/// - 초기화 로직 캡슐화
/// </summary>
public abstract class GamePageViewModel : BaseViewModel
{
    protected AuthService AuthService => _authService;
    protected AuthHelperService AuthHelper => _authHelper;

    private readonly AuthService _authService;
    private readonly AuthHelperService _authHelper;

    private decimal _balance;
    private bool _isLoading = true;
    private bool _isAuthenticated;

    protected GamePageViewModel(AuthService authService, AuthHelperService authHelper)
    {
        _authService = authService;
        _authHelper = authHelper;
    }

    /// <summary>
    /// 현재 사용자 잔액
    /// </summary>
    public decimal Balance
    {
        get => _balance;
        protected set => SetProperty(ref _balance, value);
    }

    /// <summary>
    /// 페이지 로딩 상태
    /// </summary>
    public bool IsLoading
    {
        get => _isLoading;
        protected set => SetProperty(ref _isLoading, value);
    }

    /// <summary>
    /// 사용자 인증 여부
    /// </summary>
    public bool IsAuthenticated
    {
        get => _isAuthenticated;
        protected set => SetProperty(ref _isAuthenticated, value);
    }

    /// <summary>
    /// 페이지 초기화 로직 (공통)
    /// </summary>
    /// <param name="onPageInitializedAsync">페이지별 추가 초기화 델리게이트</param>
    public async Task InitializeAsync(Func<int?, Task>? onPageInitializedAsync = null)
    {
        IsLoading = true;
        try
        {
            var (isAuth, userBalance, userId) = await _authHelper.InitializeAuthAsync();
            IsAuthenticated = isAuth;
            Balance = userBalance;

            if (onPageInitializedAsync != null)
            {
                await onPageInitializedAsync(userId);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// 잔액 최신화
    /// </summary>
    public async Task RefreshBalanceAsync()
    {
        Balance = await _authHelper.RefreshBalanceAsync();
    }
}

