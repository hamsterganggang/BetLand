using Microsoft.EntityFrameworkCore;
using ShowMeTheBet.Data;
using ShowMeTheBet.Models;
using BCrypt.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ShowMeTheBet.Services;

public class AuthService
{
    private readonly BettingDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AuthService>? _logger;
    private User? _currentUser;
    private bool _userLoaded = false;

    public AuthService(BettingDbContext context, IHttpContextAccessor httpContextAccessor, ILogger<AuthService>? logger = null)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public User? CurrentUser
    {
        get
        {
            if (!_userLoaded)
            {
                LoadUserFromSession();
            }
            return _currentUser;
        }
    }

    public bool IsAuthenticated
    {
        get
        {
            if (!_userLoaded)
            {
                LoadUserFromSession();
            }
            return _currentUser != null;
        }
    }

    public event Action? OnAuthStateChanged;

    public async Task<bool> RegisterAsync(string username, string email, string password)
    {
        if (await _context.Users.AnyAsync(u => u.Username == username || u.Email == email))
        {
            return false;
        }

        var user = new User
        {
            Username = username,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Balance = 100000,
            CreatedAt = DateTime.Now
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return true;
    }

    private void LoadUserFromSession()
    {
        if (_userLoaded) return;

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            _userLoaded = true;
            return;
        }

        int? userId = null;

        try
        {
            // 1. 먼저 세션에서 로드 시도
            if (httpContext.Session != null && httpContext.Session.IsAvailable)
            {
                userId = httpContext.Session.GetInt32("UserId");
            }
            
            // 2. 세션에서 찾지 못한 경우 쿠키에서 시도
            if (!userId.HasValue || userId.Value <= 0)
            {
                if (httpContext.Request.Cookies.TryGetValue("UserId", out var userIdCookie))
                {
                    if (int.TryParse(userIdCookie, out var parsedUserId))
                    {
                        userId = parsedUserId;
                    }
                }
            }
            
            // 3. 사용자 로드
            if (userId.HasValue && userId.Value > 0)
            {
                _currentUser = _context.Users.Find(userId.Value);
            }
        }
        catch
        {
            // Session or cookie might not be available, ignore
        }

        _userLoaded = true;
    }

    public async Task LoadUserFromSessionAsync()
    {
        // ResetUserLoad가 호출된 경우 강제로 다시 로드
        // _userLoaded를 체크하지 않고 항상 세션을 확인

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            _logger?.LogWarning("LoadUserFromSessionAsync: HttpContext가 null입니다");
            _userLoaded = true;
            _currentUser = null;
            return;
        }

        int? userId = null;

        try
        {
            // 1. 먼저 세션에서 로드 시도
            if (httpContext.Session != null)
            {
                await httpContext.Session.LoadAsync();

                int retryCount = 0;
                while (!httpContext.Session.IsAvailable && retryCount < 5)
                {
                    await Task.Delay(50);
                    await httpContext.Session.LoadAsync();
                    retryCount++;
                }

                if (httpContext.Session.IsAvailable)
                {
                    userId = httpContext.Session.GetInt32("UserId");
                    _logger?.LogInformation("세션에서 UserId 로드: {UserId}", userId);
                }
                else
                {
                    _logger?.LogWarning("세션이 사용 불가능합니다");
                }
            }
            else
            {
                _logger?.LogWarning("세션이 null입니다");
            }
            
            // 2. 세션에서 찾지 못한 경우 쿠키에서 시도
            if (!userId.HasValue || userId.Value <= 0)
            {
                if (httpContext.Request.Cookies.TryGetValue("UserId", out var userIdCookie))
                {
                    if (int.TryParse(userIdCookie, out var parsedUserId))
                    {
                        userId = parsedUserId;
                        _logger?.LogInformation("쿠키에서 UserId 로드: {UserId}", userId);
                    }
                }
                else
                {
                    _logger?.LogWarning("쿠키에서 UserId를 찾을 수 없습니다");
                }
            }
            
            // 3. 사용자 로드
            if (userId.HasValue && userId.Value > 0)
            {
                _currentUser = await _context.Users.FindAsync(userId.Value);
                _logger?.LogInformation("사용자 로드 완료: {UserId}, Username: {Username}", 
                    userId, _currentUser?.Username ?? "null");
            }
            else
            {
                _logger?.LogWarning("유효한 UserId를 찾을 수 없습니다");
                _currentUser = null;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "LoadUserFromSessionAsync 중 오류 발생");
            _currentUser = null;
        }

        _userLoaded = true;
    }

    public async Task<bool> LoginAsync(string username, string password)
    {
        _logger?.LogInformation("로그인 시도: {Username}", username);
        
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
        
        if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            _logger?.LogWarning("로그인 실패: 사용자를 찾을 수 없거나 비밀번호가 일치하지 않습니다");
            return false;
        }

        _logger?.LogInformation("로그인 성공: UserId {UserId}, Username {Username}", user.Id, user.Username);

        _currentUser = user;
        _userLoaded = true;

        // Save to session and cookie - 세션과 쿠키 모두에 저장
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            _logger?.LogError("HttpContext가 null입니다 - 세션/쿠키 저장 불가");
            OnAuthStateChanged?.Invoke();
            return true; // 사용자는 로그인되었지만 세션 저장 실패
        }

        _logger?.LogInformation("HttpContext 확인: Session={HasSession}, Response={HasResponse}, ResponseHasStarted={HasStarted}",
            httpContext.Session != null, httpContext.Response != null, httpContext.Response?.HasStarted ?? false);

        try
        {
            // 1. 세션에 저장
            if (httpContext.Session != null)
            {
                _logger?.LogInformation("세션 저장 시작");
                if (!httpContext.Session.IsAvailable)
                {
                    await httpContext.Session.LoadAsync();
                    _logger?.LogInformation("세션 로드 완료");
                }
                
                httpContext.Session.SetInt32("UserId", user.Id);
                await httpContext.Session.CommitAsync();
                _logger?.LogInformation("세션에 UserId {UserId} 저장 완료", user.Id);
                
                // 저장 확인
                await httpContext.Session.LoadAsync();
                var savedUserId = httpContext.Session.GetInt32("UserId");
                _logger?.LogInformation("세션 저장 확인: {SavedUserId}", savedUserId);
            }
            else
            {
                _logger?.LogWarning("세션이 null입니다");
            }
            
            // 2. 직접 쿠키에도 저장 (세션이 작동하지 않을 경우를 대비)
            if (httpContext.Response != null)
            {
                if (httpContext.Response.HasStarted)
                {
                    _logger?.LogWarning("Response가 이미 시작되었습니다 - 쿠키 저장 불가");
                }
                else
                {
                    _logger?.LogInformation("쿠키 저장 시작");
                    var cookieOptions = new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = false, // 로컬 개발 환경에서는 false
                        SameSite = SameSiteMode.Lax,
                        Path = "/",
                        MaxAge = TimeSpan.FromMinutes(30),
                        IsEssential = true
                    };
                    
                    httpContext.Response.Cookies.Append("UserId", user.Id.ToString(), cookieOptions);
                    _logger?.LogInformation("쿠키에 UserId {UserId} 저장 완료", user.Id);
                }
            }
            else
            {
                _logger?.LogWarning("Response가 null입니다");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "세션/쿠키 저장 중 오류 발생");
        }

        OnAuthStateChanged?.Invoke();
        return true;
    }

    public void Logout()
    {
        _currentUser = null;
        _userLoaded = false;

        // Clear session and cookie
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext != null)
        {
            try
            {
                // 세션 클리어
                if (httpContext.Session != null)
                {
                    httpContext.Session.Clear();
                }
                
                // 쿠키 삭제
                if (httpContext.Response != null && !httpContext.Response.HasStarted)
                {
                    httpContext.Response.Cookies.Delete("UserId");
                }
            }
            catch
            {
                // Session or cookie might not be available, ignore
            }
        }

        OnAuthStateChanged?.Invoke();
    }

    public async Task<User?> GetUserAsync(int userId)
    {
        return await _context.Users.FindAsync(userId);
    }

    public async Task RefreshUserAsync()
    {
        if (!_userLoaded)
        {
            LoadUserFromSession();
        }

        if (_currentUser != null)
        {
            _currentUser = await _context.Users.FindAsync(_currentUser.Id);
        }
    }

    public void ResetUserLoad()
    {
        _userLoaded = false;
    }
}

