using Microsoft.EntityFrameworkCore;
using ShowMeTheBet.Components;
using ShowMeTheBet.Data;
using ShowMeTheBet.Services;
using Microsoft.AspNetCore.HttpOverrides;
using ShowMeTheBet.ViewModels;
using ShowMeTheBet.ViewModels.Game;
using ShowMeTheBet.ViewModels.Page;

// ============================================================================
// ShowMeTheBet 애플리케이션 진입점
// ============================================================================
// 이 파일은 애플리케이션의 시작점으로, 서비스 구성, 미들웨어 설정,
// 데이터베이스 초기화 등을 담당합니다.
// ============================================================================

var builder = WebApplication.CreateBuilder(args);

// ============================================================================
// 서비스 구성 (Dependency Injection 설정)
// ============================================================================

// Razor Components 및 Interactive Server Components 추가
// Blazor Server를 사용하여 실시간 상호작용을 제공합니다.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// API 컨트롤러 추가
// RESTful API 엔드포인트를 제공하기 위해 사용됩니다.
builder.Services.AddControllers();

// ============================================================================
// 데이터베이스 설정
// ============================================================================

// 연결 문자열 가져오기 (appsettings.json에서)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Entity Framework Core를 MySQL과 함께 사용하도록 설정
// ServerVersion.AutoDetect를 사용하여 MySQL 서버 버전을 자동으로 감지합니다.
builder.Services.AddDbContext<BettingDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// ============================================================================
// 세션 설정
// ============================================================================

// 분산 메모리 캐시 추가 (세션 저장소로 사용)
builder.Services.AddDistributedMemoryCache();

// 세션 서비스 구성
builder.Services.AddSession(options =>
{
    // 세션 타임아웃: 30분
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    
    // 쿠키 설정
    options.Cookie.HttpOnly = true;        // JavaScript에서 접근 불가 (XSS 방지)
    options.Cookie.IsEssential = true;     // GDPR 규정에 따라 필수 쿠키로 설정
    options.Cookie.SameSite = SameSiteMode.Lax; // 크로스 사이트 요청 허용
    
    // HTTP 환경에서도 작동하도록 SecurePolicy를 None으로 설정
    // HTTPS를 사용하는 경우 SameAsRequest로 변경 가능
    options.Cookie.SecurePolicy = CookieSecurePolicy.None; // HTTP 환경 지원
    
    // 쿠키 경로 및 이름 설정
    options.Cookie.Path = "/";
    options.Cookie.Name = ".ShowMeTheBet.Session";
    
    // 쿠키가 항상 설정되도록 MaxAge 설정
    options.Cookie.MaxAge = TimeSpan.FromMinutes(30);
    
    // 프로덕션 환경에서는 Domain 설정 가능 (필요한 경우)
    // options.Cookie.Domain = "yourdomain.com";
});

// ============================================================================
// SignalR 설정
// ============================================================================

// SignalR 서비스 추가 (Blazor Server의 실시간 통신을 위해 필요)
// IIS 환경에서 안정적인 연결을 위해 추가 설정을 구성합니다.
builder.Services.AddSignalR(options =>
{
    // 프로덕션 환경이 아닐 때만 상세한 오류 메시지 표시
    options.EnableDetailedErrors = !builder.Environment.IsProduction();
    
    // 클라이언트 타임아웃: 30초
    // 클라이언트가 30초 동안 응답하지 않으면 연결이 끊어집니다.
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
    
    // Keep-Alive 간격: 15초
    // 15초마다 연결 상태를 확인하는 메시지를 보냅니다.
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
});

// ============================================================================
// ForwardedHeaders 설정 (IIS 리버스 프록시용)
// ============================================================================

// IIS를 리버스 프록시로 사용할 때 클라이언트 정보를 올바르게 전달받기 위한 설정
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    // 전달할 헤더 설정
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor |    // 클라이언트 IP 주소
                               ForwardedHeaders.XForwardedProto |   // 프로토콜 (HTTP/HTTPS)
                               ForwardedHeaders.XForwardedHost;     // 호스트 이름
    
    // 모든 프록시를 신뢰 (IIS 환경에서 필요)
    // 프로덕션 환경에서는 KnownProxies에 특정 IP만 추가하는 것이 더 안전합니다.
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
    
    // IIS에서 모든 프록시를 신뢰하도록 설정
    options.RequireHeaderSymmetry = false;
});

// ============================================================================
// 애플리케이션 서비스 등록
// ============================================================================

// Scoped 서비스로 등록 (요청마다 새로운 인스턴스 생성)
builder.Services.AddScoped<AuthService>();        // 인증 서비스
builder.Services.AddScoped<BettingService>();     // 스포츠 베팅 서비스
builder.Services.AddScoped<GameService>();        // 게임 서비스 (홀짝, 그래프)
builder.Services.AddScoped<AuthHelperService>();  // 인증 헬퍼 서비스 (공통 인증 로직)
builder.Services.AddScoped<BettingViewModel>();   // MVVM ViewModel (게임 선택 페이지)
builder.Services.AddScoped<SportsViewModel>();    // 스포츠 토토 페이지 ViewModel
builder.Services.AddScoped<GraphViewModel>();     // 그래프 게임 페이지 ViewModel
builder.Services.AddScoped<OddEvenViewModel>();   // 홀짝 게임 페이지 ViewModel
builder.Services.AddScoped<RouletteViewModel>();  // 룰렛 게임 페이지 ViewModel
builder.Services.AddScoped<BetHistoryViewModel>(); // 베팅 내역 페이지 ViewModel
builder.Services.AddScoped<RankingViewModel>();   // 랭킹 페이지 ViewModel
builder.Services.AddScoped<AuthPageService>();    // 로그인/회원가입/홈 서비스
builder.Services.AddScoped<RankingPageService>(); // 랭킹 페이지 서비스
builder.Services.AddScoped<BetHistoryPageService>(); // 베팅 내역 서비스

// HTTP 컨텍스트 접근자 추가
// 서비스에서 HttpContext에 접근할 수 있도록 합니다.
builder.Services.AddHttpContextAccessor();

// HttpClient 설정 (외부 API 호출용)
builder.Services.AddHttpClient();

// ============================================================================
// 애플리케이션 빌드
// ============================================================================

var app = builder.Build();

// ============================================================================
// 데이터베이스 초기화
// ============================================================================

// 애플리케이션 시작 시 데이터베이스를 초기화합니다.
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        // 데이터베이스 컨텍스트 가져오기
        var context = services.GetRequiredService<BettingDbContext>();
        
        // 모든 테이블이 존재하는지 확인하고, 없으면 생성
        await context.Database.EnsureCreatedAsync();
        
        // GameBets 테이블이 없으면 생성
        // (일부 환경에서 EnsureCreatedAsync가 모든 테이블을 생성하지 않을 수 있음)
        try
        {
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS `GameBets` (
                    `Id` int NOT NULL AUTO_INCREMENT,
                    `UserId` int NOT NULL,
                    `GameType` int NOT NULL,
                    `BetChoice` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
                    `Amount` decimal(18,2) NOT NULL,
                    `Multiplier` decimal(5,2) NOT NULL DEFAULT 1.0,
                    `WinAmount` decimal(18,2) NOT NULL,
                    `BetTime` datetime(6) NOT NULL,
                    `Status` int NOT NULL,
                    `Result` varchar(100) CHARACTER SET utf8mb4 NULL,
                    CONSTRAINT `PK_GameBets` PRIMARY KEY (`Id`),
                    CONSTRAINT `FK_GameBets_Users_UserId` FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`) ON DELETE CASCADE
                ) CHARACTER SET=utf8mb4;");
        }
        catch (Exception ex)
        {
            // 테이블이 이미 존재하거나 다른 오류가 발생한 경우
            // 로그를 남기고 계속 진행 (치명적 오류가 아님)
            var logger = services.GetRequiredService<ILogger<Program>>();
            logger.LogWarning(ex, "Could not create GameBets table, it might already exist.");
        }
        
        // 초기 경기 데이터 생성 (경기가 없을 때만)
        var bettingService = services.GetRequiredService<BettingService>();
        await bettingService.InitializeMatchesAsync();
    }
    catch (Exception ex)
    {
        // 데이터베이스 초기화 중 오류 발생 시 로그 기록
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while initializing the database.");
    }
}

// ============================================================================
// HTTP 요청 파이프라인 구성
// ============================================================================

// IIS 리버스 프록시를 위한 ForwardedHeaders 설정
// 가장 먼저 실행되어야 하므로 UseForwardedHeaders를 맨 위에 배치
app.UseForwardedHeaders();

// 프로덕션 환경이 아닐 때만 예외 처리 및 HSTS 설정
if (!app.Environment.IsDevelopment())
{
    // 예외 발생 시 /Error 페이지로 리다이렉트
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    
    // HSTS (HTTP Strict Transport Security) 설정
    // 브라우저가 HTTPS를 강제하도록 합니다.
    // 기본값은 30일입니다. 프로덕션 시나리오에 따라 변경 가능합니다.
    // 자세한 내용: https://aka.ms/aspnetcore-hsts
    app.UseHsts();
}

// 404 등 상태 코드 페이지 처리
// 존재하지 않는 페이지 접근 시 /not-found 페이지로 리다이렉트
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

// 개발 환경에서만 HTTPS 리다이렉션 사용
// IIS에서 HTTPS를 처리할 수도 있으므로 개발 환경에서만 활성화
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// 세션 미들웨어 추가
// 세션을 사용하기 전에 반드시 추가해야 합니다.
app.UseSession();

// Anti-Forgery 토큰 미들웨어 추가
// CSRF (Cross-Site Request Forgery) 공격을 방지합니다.
app.UseAntiforgery();

// 정적 파일 제공 (CSS, JavaScript, 이미지 등)
app.UseStaticFiles();

// Blazor 정적 자산 매핑
app.MapStaticAssets();

// Razor Components 매핑
// App 컴포넌트를 루트로 설정하고 Interactive Server 렌더 모드 사용
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// SignalR 연결을 위한 엔드포인트
// Blazor Server가 자동으로 사용하지만, 명시적으로 추가하여
// IIS에서 연결이 안정적으로 작동하도록 합니다.

// API 컨트롤러 매핑
// RESTful API 엔드포인트를 제공합니다.
app.MapControllers();

// ============================================================================
// 애플리케이션 실행
// ============================================================================

app.Run();
