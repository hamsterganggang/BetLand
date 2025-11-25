using Microsoft.EntityFrameworkCore;
using ShowMeTheBet.Data;
using ShowMeTheBet.Models;
using BCrypt.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

namespace ShowMeTheBet.Services;

/// <summary>
/// 사용자 인증 및 세션 관리를 담당하는 서비스 클래스
/// 로그인, 로그아웃, 사용자 정보 관리, 세션/쿠키 관리 등의 기능을 제공합니다.
/// IIS 환경에서 안정적으로 작동하도록 쿠키와 세션을 모두 사용합니다.
/// </summary>
public class AuthService
{
    #region Private Fields
    /// <summary>
    /// 데이터베이스 컨텍스트 - 사용자 정보 조회 및 저장
    /// </summary>
    private readonly BettingDbContext _context;
    
    /// <summary>
    /// HTTP 컨텍스트 접근자 - 세션 및 쿠키 접근용
    /// </summary>
    private readonly IHttpContextAccessor _httpContextAccessor;
    
    /// <summary>
    /// 로거 - 디버깅 및 오류 추적용
    /// </summary>
    private readonly ILogger<AuthService>? _logger;
    
    /// <summary>
    /// 웹 호스트 환경 정보
    /// </summary>
    private readonly IWebHostEnvironment? _environment;
    
    /// <summary>
    /// 현재 로그인한 사용자 정보 (캐시)
    /// </summary>
    private User? _currentUser;
    
    /// <summary>
    /// 사용자 정보가 로드되었는지 여부 (중복 로드 방지)
    /// </summary>
    private bool _userLoaded = false;
    #endregion

    #region Constructor
    /// <summary>
    /// AuthService 생성자
    /// </summary>
    /// <param name="context">데이터베이스 컨텍스트</param>
    /// <param name="httpContextAccessor">HTTP 컨텍스트 접근자</param>
    /// <param name="logger">로거 (선택사항)</param>
    /// <param name="environment">웹 호스트 환경 (선택사항)</param>
    public AuthService(BettingDbContext context, IHttpContextAccessor httpContextAccessor, ILogger<AuthService>? logger = null, IWebHostEnvironment? environment = null)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _environment = environment;
    }
    #endregion

    #region Public Properties
    /// <summary>
    /// 현재 로그인한 사용자 정보를 반환하는 속성
    /// 쿠키에서 UserId를 읽어서 사용자 정보를 로드합니다.
    /// IIS 환경에서 안정적으로 작동하도록 쿠키를 우선적으로 사용합니다.
    /// </summary>
    public User? CurrentUser
    {
        get
        {
            // 이미 메모리에 사용자 정보가 있으면 그대로 반환
            if (_currentUser != null && _userLoaded)
            {
                return _currentUser;
            }

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
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "CurrentUser getter에서 사용자 로드 실패: {UserId}", userId);
                        }
                    }
                }
            }

            return _currentUser;
        }
    }

    /// <summary>
    /// 현재 사용자가 인증되었는지 여부를 반환하는 속성
    /// CurrentUser가 null이 아니면 인증된 것으로 간주합니다.
    /// </summary>
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

    /// <summary>
    /// 인증 상태가 변경될 때 발생하는 이벤트
    /// 로그인/로그아웃 시 UI 업데이트를 위해 사용됩니다.
    /// </summary>
    public event Action? OnAuthStateChanged;
    #endregion

    #region Public Methods

    /// <summary>
    /// 새 사용자를 등록합니다.
    /// 사용자명과 이메일이 중복되지 않아야 합니다.
    /// </summary>
    /// <param name="username">사용자명</param>
    /// <param name="email">이메일</param>
    /// <param name="password">비밀번호 (BCrypt로 해시화되어 저장됨)</param>
    /// <returns>등록 성공 여부 (중복된 사용자명/이메일이 있으면 false)</returns>
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

    #region Private Methods
    /// <summary>
    /// 세션/쿠키에서 사용자 정보를 로드하는 내부 메서드 (동기 버전)
    /// 쿠키를 먼저 확인하고, 없으면 세션에서 확인합니다.
    /// </summary>
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

    /// <summary>
    /// 세션/쿠키에서 사용자 정보를 로드하는 비동기 메서드
    /// 쿠키를 먼저 확인하고, 없으면 세션에서 확인합니다.
    /// IIS 환경에서 안정적으로 작동하도록 여러 번 재시도합니다.
    /// </summary>
    public async Task LoadUserFromSessionAsync()
    {
        // ResetUserLoad가 호출된 경우 강제로 다시 로드
        // _userLoaded를 체크하지 않고 항상 세션을 확인

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            _logger?.LogWarning("LoadUserFromSessionAsync: HttpContext가 null입니다");
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
                }
            }
            else
            {
                _logger?.LogWarning("유효한 UserId를 찾을 수 없습니다");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "LoadUserFromSessionAsync 중 오류 발생");
        }

        _userLoaded = true;
    }

    /// <summary>
    /// 사용자 로그인을 처리합니다.
    /// 비밀번호는 BCrypt로 검증하고, 로그인 성공 시 세션과 쿠키에 UserId를 저장합니다.
    /// </summary>
    /// <param name="username">사용자명</param>
    /// <param name="password">비밀번호</param>
    /// <returns>로그인 성공 여부</returns>
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

    /// <summary>
    /// 사용자 로그아웃을 처리합니다.
    /// 세션과 쿠키를 모두 삭제하고 CurrentUser를 null로 설정합니다.
    /// </summary>
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

    /// <summary>
    /// 사용자 ID로 사용자 정보를 조회합니다.
    /// </summary>
    /// <param name="userId">사용자 ID</param>
    /// <returns>사용자 정보 (없으면 null)</returns>
    public async Task<User?> GetUserAsync(int userId)
    {
        return await _context.Users.FindAsync(userId);
    }

    /// <summary>
    /// 현재 사용자 정보를 데이터베이스에서 최신화합니다.
    /// 잔액 등 변경된 정보를 반영하기 위해 사용됩니다.
    /// Entity Framework의 변경 추적을 초기화하여 최신 데이터를 가져옵니다.
    /// </summary>
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

    /// <summary>
    /// 사용자 로드 상태를 초기화합니다.
    /// 다음에 CurrentUser를 접근할 때 다시 로드하도록 합니다.
    /// </summary>
    public void ResetUserLoad()
    {
        _userLoaded = false;
        _currentUser = null;
    }

    /// <summary>
    /// 사용자 ID로 사용자 정보를 로드하고 CurrentUser에 설정합니다.
    /// Entity Framework의 변경 추적을 초기화하여 최신 데이터를 가져옵니다.
    /// </summary>
    /// <param name="userId">사용자 ID</param>
    /// <returns>로드 성공 여부</returns>
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
    #endregion
    #endregion
}

