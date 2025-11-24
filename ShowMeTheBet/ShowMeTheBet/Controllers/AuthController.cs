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
            return Ok(new { success = true, redirectUrl = "/game" });
        }
        else
        {
            return Unauthorized(new { success = false, message = "사용자명 또는 비밀번호가 올바르지 않습니다." });
        }
    }

    public class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}

