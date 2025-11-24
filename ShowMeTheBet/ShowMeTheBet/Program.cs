using Microsoft.EntityFrameworkCore;
using ShowMeTheBet.Components;
using ShowMeTheBet.Data;
using ShowMeTheBet.Services;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add controllers for API endpoints
builder.Services.AddControllers();

// Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<BettingDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// Session
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    // HTTP 환경에서도 작동하도록 SecurePolicy를 None으로 설정
    // HTTPS를 사용하는 경우 SameAsRequest로 변경 가능
    options.Cookie.SecurePolicy = CookieSecurePolicy.None; // HTTP 환경 지원
    options.Cookie.Path = "/";
    options.Cookie.Name = ".ShowMeTheBet.Session";
    // 쿠키가 항상 설정되도록
    options.Cookie.MaxAge = TimeSpan.FromMinutes(30);
    // 프로덕션 환경에서는 Domain 설정 가능 (필요한 경우)
    // options.Cookie.Domain = "yourdomain.com";
});

// SignalR을 위한 추가 설정 (IIS 환경에서 안정적인 연결을 위해)
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = !builder.Environment.IsProduction();
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
});

// ForwardedHeaders 서비스 추가 (IIS 리버스 프록시용)
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | 
                               ForwardedHeaders.XForwardedProto |
                               ForwardedHeaders.XForwardedHost;
    // 모든 프록시를 신뢰 (IIS 환경에서 필요)
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
    // IIS에서 모든 프록시를 신뢰하도록 설정
    options.RequireHeaderSymmetry = false;
});

// Services
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<BettingService>();
builder.Services.AddScoped<GameService>();
builder.Services.AddHttpContextAccessor();

// HttpClient 설정
builder.Services.AddHttpClient();

var app = builder.Build();

// Initialize database
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<BettingDbContext>();
        
        // Ensure all tables exist
        await context.Database.EnsureCreatedAsync();
        
        // Create GameBets table if it doesn't exist
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
            // Table might already exist or other error, log and continue
            var logger = services.GetRequiredService<ILogger<Program>>();
            logger.LogWarning(ex, "Could not create GameBets table, it might already exist.");
        }
        
        var bettingService = services.GetRequiredService<BettingService>();
        await bettingService.InitializeMatchesAsync();
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while initializing the database.");
    }
}

// Configure the HTTP request pipeline.
// IIS 리버스 프록시를 위한 ForwardedHeaders 설정 (가장 먼저 실행되어야 함)
app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

// 개발 환경이 아닐 때만 HTTPS 리다이렉션 (IIS에서 처리할 수도 있음)
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseSession();
app.UseAntiforgery();

// 정적 파일 제공
app.UseStaticFiles();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// SignalR 연결을 위한 엔드포인트 (Blazor Server가 자동으로 사용)
// 명시적으로 추가하여 IIS에서 연결이 안정적으로 작동하도록 함

// Map API controllers
app.MapControllers();

app.Run();
