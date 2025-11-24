using Microsoft.AspNetCore.Mvc;
using ShowMeTheBet.Services;

namespace ShowMeTheBet.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;

    public AuthController(AuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { success = false, message = "사용자명과 비밀번호를 입력해주세요." });
        }

        var success = await _authService.LoginAsync(request.Username, request.Password);
        
        if (success)
        {
            // 세션/쿠키가 확실히 저장되도록 약간의 지연
            await Task.Delay(100);
            return Ok(new { success = true, redirectUrl = "/game" });
        }
        else
        {
            return Unauthorized(new { success = false, message = "사용자명 또는 비밀번호가 올바르지 않습니다." });
        }
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        _authService.Logout();
        return Ok(new { success = true });
    }

    [HttpGet("check")]
    public IActionResult CheckAuth()
    {
        var user = _authService.CurrentUser;
        if (user != null)
        {
            return Ok(new { 
                success = true, 
                authenticated = true,
                userId = user.Id,
                username = user.Username,
                balance = user.Balance
            });
        }
        else
        {
            return Ok(new { 
                success = true, 
                authenticated = false 
            });
        }
    }

    public class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}

