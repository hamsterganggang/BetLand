using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using ShowMeTheBet.Services;

namespace ShowMeTheBet.Components.Pages.Base;

/// <summary>
/// 게임 페이지들의 공통 기능을 제공하는 베이스 클래스
/// 인증 확인, 잔액 관리, 공통 상태 관리 등의 로직을 캡슐화하여 중복 코드를 제거합니다.
/// </summary>
public abstract class GamePageBase : ComponentBase
{
    #region Injected Services
    /// <summary>
    /// 인증 서비스 - 사용자 인증 및 세션 관리
    /// </summary>
    [Inject] protected AuthService AuthService { get; set; } = null!;
    
    /// <summary>
    /// 인증 헬퍼 서비스 - 공통 인증 로직 처리
    /// </summary>
    [Inject] protected AuthHelperService AuthHelper { get; set; } = null!;
    
    /// <summary>
    /// JavaScript Interop 런타임 - 브라우저 API 호출용
    /// </summary>
    [Inject] protected IJSRuntime JSRuntime { get; set; } = null!;
    
    /// <summary>
    /// 네비게이션 매니저 - 페이지 이동용
    /// </summary>
    [Inject] protected NavigationManager Navigation { get; set; } = null!;
    #endregion

    #region Protected Properties
    /// <summary>
    /// 현재 사용자 잔액
    /// </summary>
    protected decimal balance = 0;
    
    /// <summary>
    /// 페이지 로딩 상태 (초기 데이터 로드 중인지 여부)
    /// </summary>
    protected bool isLoading = true;
    
    /// <summary>
    /// 사용자 인증 상태 (로그인 여부)
    /// </summary>
    protected bool isAuthenticated = false;
    #endregion

    #region Lifecycle Methods
    /// <summary>
    /// 페이지 초기화 시 호출되는 메서드
    /// 인증 상태를 확인하고 초기 데이터를 로드합니다.
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        isLoading = true;
        StateHasChanged();
        
        try
        {
            // 공통 인증 초기화 로직 실행
            var (isAuth, userBalance, userId) = await AuthHelper.InitializeAuthAsync();
            isAuthenticated = isAuth;
            balance = userBalance;
            
            // 하위 클래스에서 추가 초기화 로직 실행
            await OnPageInitializedAsync(userId);
        }
        catch
        {
            // 초기화 실패 시 인증되지 않은 상태로 설정
            isAuthenticated = false;
            balance = 0;
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    /// <summary>
    /// 첫 렌더링 후 호출되는 메서드
    /// SignalR 연결 후 HttpContext가 설정될 수 있으므로 인증 상태를 다시 확인합니다.
    /// </summary>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && !isAuthenticated)
        {
            // 첫 렌더링 시 인증되지 않았으면 다시 확인
            await CheckAuthAndLoadDataAsync();
        }
    }
    #endregion

    #region Protected Virtual Methods
    /// <summary>
    /// 페이지별 초기화 로직을 구현하는 가상 메서드
    /// 각 게임 페이지에서 필요한 초기 데이터를 로드할 때 오버라이드합니다.
    /// </summary>
    /// <param name="userId">인증된 사용자의 ID (없으면 null)</param>
    protected virtual async Task OnPageInitializedAsync(int? userId)
    {
        // 기본 구현은 비어있음 - 하위 클래스에서 오버라이드
        await Task.CompletedTask;
    }

    /// <summary>
    /// 인증 상태를 다시 확인하고 데이터를 로드하는 메서드
    /// SignalR 연결 후 HttpContext가 설정될 수 있을 때 호출됩니다.
    /// </summary>
    protected virtual async Task CheckAuthAndLoadDataAsync()
    {
        try
        {
            var authResult = await AuthHelper.CheckAuthAsync();
            if (authResult != null && authResult.authenticated && authResult.userId.HasValue)
            {
                var wasAuthenticated = isAuthenticated;
                await AuthService.LoadUserByIdAsync(authResult.userId.Value);
                isAuthenticated = true;
                
                if (!wasAuthenticated)
                {
                    // 인증 상태가 변경된 경우 잔액 업데이트
                    balance = await AuthHelper.RefreshBalanceAsync();
                    StateHasChanged();
                }
            }
        }
        catch
        {
            // 인증 확인 실패 시 무시
        }
    }
    #endregion

    #region Protected Helper Methods
    /// <summary>
    /// 잔액을 최신화하고 UI를 업데이트합니다.
    /// 베팅 후나 게임 종료 후 호출됩니다.
    /// </summary>
    protected async Task UpdateBalanceAsync()
    {
        balance = await AuthHelper.RefreshBalanceAsync();
        StateHasChanged();
    }

    /// <summary>
    /// 로그인 페이지로 이동합니다.
    /// </summary>
    protected void NavigateToLogin()
    {
        Navigation.NavigateTo("/login");
    }
    #endregion
}

