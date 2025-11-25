using Microsoft.JSInterop;
using ShowMeTheBet.Models.DTOs;

namespace ShowMeTheBet.Services;

/// <summary>
/// 인증 관련 공통 로직을 처리하는 헬퍼 서비스
/// 모든 Razor 페이지에서 공통으로 사용되는 인증 확인, userId 가져오기 등의 로직을 캡슐화
/// </summary>
public class AuthHelperService
{
    private readonly AuthService _authService;
    private readonly IJSRuntime _jsRuntime;

    /// <summary>
    /// AuthHelperService 생성자
    /// </summary>
    /// <param name="authService">인증 서비스 (사용자 정보 관리)</param>
    /// <param name="jsRuntime">JavaScript Interop 런타임 (API 호출용)</param>
    public AuthHelperService(AuthService authService, IJSRuntime jsRuntime)
    {
        _authService = authService;
        _jsRuntime = jsRuntime;
    }

    /// <summary>
    /// JavaScript Interop을 통해 API를 호출하여 인증 상태를 확인합니다.
    /// 실서버(IIS) 환경에서 SignalR 연결의 HttpContext 접근 문제를 해결하기 위해 사용됩니다.
    /// </summary>
    /// <returns>인증 확인 결과 (AuthCheckResult 객체)</returns>
    public async Task<AuthCheckResult?> CheckAuthAsync()
    {
        try
        {
            // JavaScript의 checkAuth 함수를 호출하여 /api/auth/check 엔드포인트에 요청
            // 이 방식은 SignalR 연결에서 HttpContext가 제대로 설정되지 않는 문제를 우회합니다
            var authResult = await _jsRuntime.InvokeAsync<AuthCheckResult>("checkAuth");
            return authResult;
        }
        catch
        {
            // API 호출 실패 시 null 반환 (로컬 환경이나 네트워크 오류 시)
            return null;
        }
    }

    /// <summary>
    /// 현재 사용자의 ID를 가져옵니다.
    /// 여러 방법을 시도하여 userId를 찾습니다:
    /// 1. AuthService.CurrentUser에서 가져오기
    /// 2. 없으면 세션에서 로드 시도
    /// 3. 없으면 API를 통해 확인
    /// </summary>
    /// <returns>사용자 ID (없으면 null)</returns>
    public async Task<int?> GetUserIdAsync()
    {
        // 1. 먼저 CurrentUser에서 확인 (가장 빠른 방법)
        if (_authService.CurrentUser != null)
        {
            return _authService.CurrentUser.Id;
        }

        // 2. CurrentUser가 null이면 세션에서 로드 시도
        await _authService.LoadUserFromSessionAsync();
        if (_authService.CurrentUser != null)
        {
            return _authService.CurrentUser.Id;
        }

        // 3. 여전히 null이면 API를 통해 확인 (최후의 수단)
        try
        {
            var authResult = await CheckAuthAsync();
            if (authResult != null && authResult.authenticated && authResult.userId.HasValue)
            {
                // API에서 userId를 받았으면 AuthService에도 로드
                await _authService.LoadUserByIdAsync(authResult.userId.Value);
                return authResult.userId.Value;
            }
        }
        catch
        {
            // API 호출 실패 시 무시
        }

        return null;
    }

    /// <summary>
    /// 인증 상태를 확인하고 사용자 정보를 로드합니다.
    /// 페이지 초기화 시 사용되는 공통 로직입니다.
    /// </summary>
    /// <returns>
    /// 인증 확인 결과:
    /// - IsAuthenticated: 인증 여부
    /// - Balance: 사용자 잔액 (인증된 경우)
    /// - UserId: 사용자 ID (인증된 경우)
    /// </returns>
    public async Task<(bool IsAuthenticated, decimal Balance, int? UserId)> InitializeAuthAsync()
    {
        // API를 통해 인증 상태 확인
        var authResult = await CheckAuthAsync();
        
        if (authResult != null && authResult.authenticated && authResult.userId.HasValue)
        {
            // API 응답이 성공적이고 인증된 경우
            // 사용자 정보를 AuthService에 로드
            await _authService.LoadUserByIdAsync(authResult.userId.Value);
            
            // 잔액 결정: API 응답의 balance가 있으면 사용, 없으면 RefreshUserAsync 후 가져오기
            decimal balance = 0;
            if (authResult.balance.HasValue)
            {
                balance = authResult.balance.Value;
            }
            else
            {
                // API 응답에 balance가 없으면 DB에서 최신 정보 가져오기
                await _authService.RefreshUserAsync();
                if (_authService.CurrentUser != null)
                {
                    balance = _authService.CurrentUser.Balance;
                }
            }

            return (true, balance, authResult.userId.Value);
        }
        else
        {
            // API 호출 실패 또는 인증되지 않은 경우
            // 직접 세션에서 확인 (로컬 환경 대비)
            _authService.ResetUserLoad();
            await _authService.LoadUserFromSessionAsync();
            var user = _authService.CurrentUser;
            
            if (user != null)
            {
                // 세션에서 사용자를 찾은 경우
                await _authService.RefreshUserAsync();
                return (true, user.Balance, user.Id);
            }
            else
            {
                // 인증되지 않은 경우
                return (false, 0, null);
            }
        }
    }

    /// <summary>
    /// 잔액을 업데이트하고 최신 정보를 가져옵니다.
    /// 베팅 후나 게임 종료 후 잔액을 갱신할 때 사용됩니다.
    /// </summary>
    /// <returns>최신 잔액</returns>
    public async Task<decimal> RefreshBalanceAsync()
    {
        // 사용자 정보를 DB에서 최신화
        await _authService.RefreshUserAsync();
        
        // 최신 잔액 반환
        if (_authService.CurrentUser != null)
        {
            return _authService.CurrentUser.Balance;
        }
        
        return 0;
    }
}

