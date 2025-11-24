using Microsoft.EntityFrameworkCore;
using ShowMeTheBet.Data;
using ShowMeTheBet.Models;
using BCrypt.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

namespace ShowMeTheBet.Services;

public class AuthService
{
    private readonly BettingDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AuthService>? _logger;
    private readonly IWebHostEnvironment? _environment;
    private User? _currentUser;
    private bool _userLoaded = false;

    public AuthService(BettingDbContext context, IHttpContextAccessor httpContextAccessor, ILogger<AuthService>? logger = null, IWebHostEnvironment? environment = null)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _environment = environment;
    }

    public User? CurrentUser
    {
        get
        {
            // 항상 쿠키에서 확인 (IIS 환경에서 가장 안정적)
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext != null && httpContext.Request.Cookies.TryGetValue("UserId", out var userIdCookie))
            {
                if (int.TryParse(userIdCookie, out var userId) && userId > 0)
                {
                    // _currentUser가 없거나 다른 사용자인 경우에만 로드
                    if (_currentUser == null || _currentUser.Id != userId)
                    {
                        try
                        {
                            _currentUser = _context.Users.Find(userId);
                            if (_currentUser != null)
                            {
                                _userLoaded = true;
                                _logger?.LogInformation("CurrentUser getter에서 쿠키로 사용자 로드: {UserId}, {Username}", userId, _currentUser.Username);
                            }
                            else
                            {
                                _logger?.LogWarning("CurrentUser getter: 사용자를 찾을 수 없습니다: UserId {UserId}", userId);
                                _currentUser = null;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "CurrentUser getter에서 사용자 로드 실패: {UserId}", userId);
                            _currentUser = null;
                        }
                    }
                }
                else
                {
                    // 쿠키에 유효하지 않은 값이 있으면 null로 설정
                    if (_currentUser != null)
                    {
                        _currentUser = null;
                        _userLoaded = false;
                    }
                }
            }
            else
            {
                // 쿠키가 없으면 null로 설정
                if (_currentUser != null)
                {
                    _currentUser = null;
                    _userLoaded = false;
                }
            }
            
            return _currentUser;
        }
    }

    public bool IsAuthenticated
    {
        get
        {
            // CurrentUser 속성을 통해 확인 (위의 로직 사용)
            var user = CurrentUser;
            var isAuth = user != null;
            _logger?.LogInformation("IsAuthenticated 호출: {IsAuth}, UserId: {UserId}", isAuth, user?.Id ?? 0);
            return isAuth;
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
        if (_userLoaded && _currentUser != null) return;

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            _userLoaded = true;
            return;
        }

        int? userId = null;

        try
        {
            // 1. 먼저 쿠키에서 시도 (IIS 환경에서 더 안정적)
            if (httpContext.Request.Cookies.TryGetValue("UserId", out var userIdCookie))
            {
                if (int.TryParse(userIdCookie, out var parsedUserId) && parsedUserId > 0)
                {
                    userId = parsedUserId;
                    _logger?.LogInformation("LoadUserFromSession: 쿠키에서 UserId 로드: {UserId}", userId);
                }
            }
            
            // 2. 쿠키에서 찾지 못한 경우 세션에서 시도
            if (!userId.HasValue || userId.Value <= 0)
            {
                if (httpContext.Session != null && httpContext.Session.IsAvailable)
                {
                    userId = httpContext.Session.GetInt32("UserId");
                    if (userId.HasValue)
                    {
                        _logger?.LogInformation("LoadUserFromSession: 세션에서 UserId 로드: {UserId}", userId);
                    }
                }
            }
            
            // 3. 사용자 로드
            if (userId.HasValue && userId.Value > 0)
            {
                _currentUser = _context.Users.Find(userId.Value);
                if (_currentUser != null)
                {
                    _logger?.LogInformation("LoadUserFromSession: 사용자 로드 완료: {UserId}, {Username}", userId, _currentUser.Username);
                }
                else
                {
                    _logger?.LogWarning("LoadUserFromSession: 사용자를 찾을 수 없습니다: UserId {UserId}", userId);
                }
            }
            else
            {
                _logger?.LogWarning("LoadUserFromSession: 유효한 UserId를 찾을 수 없습니다");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "LoadUserFromSession 중 오류 발생");
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
            // 1. 먼저 쿠키에서 시도 (IIS 환경에서 더 안정적)
            if (httpContext.Request.Cookies.TryGetValue("UserId", out var userIdCookie))
            {
                if (int.TryParse(userIdCookie, out var parsedUserId) && parsedUserId > 0)
                {
                    userId = parsedUserId;
                    _logger?.LogInformation("쿠키에서 UserId 로드: {UserId}", userId);
                }
            }
            
            // 2. 쿠키에서 찾지 못한 경우 세션에서 시도
            if (!userId.HasValue || userId.Value <= 0)
            {
                if (httpContext.Session != null)
                {
                    try
                    {
                        await httpContext.Session.LoadAsync();

                        int retryCount = 0;
                        while (!httpContext.Session.IsAvailable && retryCount < 3)
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
                    catch (Exception sessionEx)
                    {
                        _logger?.LogWarning(sessionEx, "세션 로드 중 오류 발생, 쿠키 사용");
                    }
                }
                else
                {
                    _logger?.LogWarning("세션이 null입니다");
                }
            }
            
            // 3. 사용자 로드
            if (userId.HasValue && userId.Value > 0)
            {
                _currentUser = await _context.Users.FindAsync(userId.Value);
                if (_currentUser != null)
                {
                    _logger?.LogInformation("사용자 로드 완료: {UserId}, Username: {Username}", 
                        userId, _currentUser.Username);
                }
                else
                {
                    _logger?.LogWarning("사용자를 찾을 수 없습니다: UserId {UserId}", userId);
                    _currentUser = null;
                }
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
                    
                    // HTTP 환경에서도 작동하도록 Secure를 false로 설정
                    // 실서버에서 HTTP를 사용하므로 항상 false로 설정
                    var isSecure = false; // HTTP 환경에서는 항상 false
                    
                    _logger?.LogInformation("쿠키 설정: IsHttps={IsHttps}, X-Forwarded-Proto={XForwardedProto}, Secure={Secure}", 
                        httpContext.Request.IsHttps, 
                        httpContext.Request.Headers["X-Forwarded-Proto"].ToString(), 
                        isSecure);
                    
                    var cookieOptions = new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = isSecure, // HTTPS인 경우에만 true
                        SameSite = SameSiteMode.Lax, // 크로스 사이트 요청 허용
                        Path = "/",
                        MaxAge = TimeSpan.FromMinutes(30),
                        IsEssential = true
                    };
                    
                    httpContext.Response.Cookies.Append("UserId", user.Id.ToString(), cookieOptions);
                    _logger?.LogInformation("쿠키에 UserId {UserId} 저장 완료 (Secure: {IsSecure})", user.Id, isSecure);
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
                    _logger?.LogInformation("세션 클리어 완료");
                }
                
                // 쿠키 삭제
                if (httpContext.Response != null && !httpContext.Response.HasStarted)
                {
                    // 쿠키 삭제 (만료 날짜를 과거로 설정)
                    var cookieOptions = new CookieOptions
                    {
                        Expires = DateTimeOffset.UtcNow.AddDays(-1),
                        HttpOnly = true,
                        Path = "/",
                        SameSite = SameSiteMode.Lax
                    };
                    
                    // HTTP 환경에서는 항상 Secure를 false로 설정
                    cookieOptions.Secure = false;
                    
                    httpContext.Response.Cookies.Append("UserId", "", cookieOptions);
                    _logger?.LogInformation("쿠키 삭제 완료");
                }
                else
                {
                    _logger?.LogWarning("Response가 null이거나 이미 시작되었습니다 - 쿠키 삭제 불가");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "로그아웃 중 세션/쿠키 삭제 오류");
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
            var userId = _currentUser.Id;
            
            // 실서버 환경에서 안정적으로 작동하도록 변경 추적 처리
            // 기존 추적된 엔티티가 있으면 분리
            var trackedEntity = _context.Users.Local.FirstOrDefault(u => u.Id == userId);
            if (trackedEntity != null)
            {
                _context.Entry(trackedEntity).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
            }
            
            // DB에서 최신 데이터 조회
            var freshUser = await _context.Users.FindAsync(userId);
            
            if (freshUser != null)
            {
                _currentUser = freshUser;
            }
        }
    }

    public void ResetUserLoad()
    {
        _userLoaded = false;
        _currentUser = null;
    }

    public async Task<bool> LoadUserByIdAsync(int userId)
    {
        try
        {
            // 기존 추적된 엔티티가 있으면 분리
            var trackedEntity = _context.Users.Local.FirstOrDefault(u => u.Id == userId);
            if (trackedEntity != null)
            {
                _context.Entry(trackedEntity).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
            }
            
            var user = await _context.Users.FindAsync(userId);
            if (user != null)
            {
                _currentUser = user;
                _userLoaded = true;
                _logger?.LogInformation("LoadUserByIdAsync: 사용자 로드 완료: {UserId}, {Username}, Balance: {Balance}", 
                    userId, user.Username, user.Balance);
                
                // CurrentUser getter가 쿠키를 확인하므로, 쿠키도 확인
                var httpContext = _httpContextAccessor.HttpContext;
                if (httpContext != null && httpContext.Request.Cookies.TryGetValue("UserId", out var userIdCookie))
                {
                    if (int.TryParse(userIdCookie, out var cookieUserId) && cookieUserId == userId)
                    {
                        _logger?.LogInformation("LoadUserByIdAsync: 쿠키와 일치 확인됨");
                    }
                    else
                    {
                        _logger?.LogWarning("LoadUserByIdAsync: 쿠키의 UserId({CookieUserId})와 로드한 UserId({UserId})가 일치하지 않음", 
                            cookieUserId, userId);
                    }
                }
                
                OnAuthStateChanged?.Invoke();
                return true;
            }
            else
            {
                _logger?.LogWarning("LoadUserByIdAsync: 사용자를 찾을 수 없습니다: UserId {UserId}", userId);
                _currentUser = null;
                _userLoaded = true;
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "LoadUserByIdAsync: 사용자 로드 실패: UserId {UserId}", userId);
            _currentUser = null;
            _userLoaded = true;
            return false;
        }
    }
}

