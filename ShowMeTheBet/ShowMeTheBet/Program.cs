using Microsoft.EntityFrameworkCore;
using ShowMeTheBet.Components;
using ShowMeTheBet.Data;
using ShowMeTheBet.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<BettingDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// Services
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<BettingService>();
builder.Services.AddScoped<GameService>();

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
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
