using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace ShowMeTheBet.Services;

/// <summary>
/// 로그인/회원가입/홈 리다이렉트 등 인증 관련 단순 워크플로우를 처리하는 서비스.
/// 페이지 단에서는 UI 처리에만 집중할 수 있도록 보조 로직을 제공합니다.
/// </summary>
public class AuthPageService
{
    private readonly AuthService _authService;
    private readonly NavigationManager _navigationManager;
    private readonly IJSRuntime _jsRuntime;

    public AuthPageService(
        AuthService authService,
        NavigationManager navigationManager,
        IJSRuntime jsRuntime)
    {
        _authService = authService;
        _navigationManager = navigationManager;
        _jsRuntime = jsRuntime;
    }

    public async Task<bool> RedirectIfAuthenticatedAsync(string redirectUrl = "/game", bool reloadSession = false)
    {
        if (reloadSession)
        {
            _authService.ResetUserLoad();
            await _authService.LoadUserFromSessionAsync();
        }

        if (_authService.CurrentUser != null)
        {
            _navigationManager.NavigateTo(redirectUrl, forceLoad: true);
            return true;
        }

        return false;
    }

    public async Task NavigateFromHomeAsync()
    {
        _authService.ResetUserLoad();
        await _authService.LoadUserFromSessionAsync();

        var destination = _authService.IsAuthenticated ? "/game" : "/login";
        _navigationManager.NavigateTo(destination, forceLoad: true);
    }

    public async Task<AuthOperationResult> LoginAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return AuthOperationResult.CreateFailure("사용자명과 비밀번호를 입력해주세요.");
        }

        try
        {
            var payload = JsonSerializer.Serialize(new { Username = username, Password = password });
            var response = await _jsRuntime.InvokeAsync<LoginResponse?>("loginWithCredentials", payload);

            if (response != null && response.Success)
            {
                return AuthOperationResult.CreateSuccess();
            }

            return AuthOperationResult.CreateFailure(response?.Message ?? "로그인에 실패했습니다.");
        }
        catch (Exception ex)
        {
            return AuthOperationResult.CreateFailure($"로그인 중 오류가 발생했습니다: {ex.Message}");
        }
    }

    public async Task<AuthOperationResult> RegisterAsync(string username, string email, string password, string passwordConfirm)
    {
        var validationError = ValidateRegistrationInput(username, email, password, passwordConfirm);
        if (validationError != null)
        {
            return AuthOperationResult.CreateFailure(validationError);
        }

        try
        {
            var registered = await _authService.RegisterAsync(username, email, password);
            if (!registered)
            {
                return AuthOperationResult.CreateFailure("이미 존재하는 사용자명 또는 이메일입니다.");
            }

            var loginSuccess = await _authService.LoginAsync(username, password);
            if (loginSuccess)
            {
                await Task.Delay(500);
                await _jsRuntime.InvokeVoidAsync("navigateToGame");
                return AuthOperationResult.CreateSuccess("회원가입이 완료되었습니다! 게임 페이지로 이동합니다...");
            }

            _navigationManager.NavigateTo("/login");
            return AuthOperationResult.CreateSuccess("회원가입이 완료되었습니다! 로그인 페이지로 이동합니다.");
        }
        catch (Exception ex)
        {
            return AuthOperationResult.CreateFailure($"회원가입 중 오류가 발생했습니다: {ex.Message}");
        }
    }

    private static string? ValidateRegistrationInput(string username, string email, string password, string passwordConfirm)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return "사용자명을 입력해주세요.";
        }

        if (username.Length < 3 || username.Length > 20)
        {
            return "사용자명은 3-20자 사이여야 합니다.";
        }

        if (string.IsNullOrWhiteSpace(email) || !email.Contains("@") || !email.Contains("."))
        {
            return "올바른 이메일을 입력해주세요.";
        }

        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
        {
            return "비밀번호는 최소 6자 이상이어야 합니다.";
        }

        if (password != passwordConfirm)
        {
            return "비밀번호가 일치하지 않습니다.";
        }

        return null;
    }

    private sealed class LoginResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? RedirectUrl { get; set; }
    }
}

public record AuthOperationResult(bool Success, string? ErrorMessage = null, string? SuccessMessage = null)
{
    public static AuthOperationResult CreateSuccess(string? message = null) => new(true, null, message);
    public static AuthOperationResult CreateFailure(string message) => new(false, message, null);
}

