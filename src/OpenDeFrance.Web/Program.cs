using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using OpenDeFrance.Core.Services;
using OpenDeFrance.Infrastructure.Data;
using OpenDeFrance.Infrastructure.Services;
using OpenDeFrance.Web.Components;
using OpenDeFrance.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Authentication & Authorization Services (Cookie Scheme for HTTP & Cascading State)
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
{
    options.LoginPath = "/login";
    options.AccessDeniedPath = "/login";
    options.ExpireTimeSpan = TimeSpan.FromHours(12);
    options.SlidingExpiration = true;
});

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddSingleton<IAuthService, AuthService>();
builder.Services.AddScoped<ILanguageService, LanguageService>();

// Base de données : provider déterminé par la clé "DatabaseProvider" (SqlServer ou Sqlite)
// Si absent, SqlServer est utilisé dès qu'une connection string est définie.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var providerKey = builder.Configuration["DatabaseProvider"]; // "SqlServer" | "Sqlite" | null

bool useSqlServer = providerKey?.Equals("Sqlite", StringComparison.OrdinalIgnoreCase) != true
    && !string.IsNullOrWhiteSpace(connectionString)
    && !connectionString.TrimStart().StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase);

// Fallback : si la connection string contient un serveur SQL Azure connu → SQL Server
if (!useSqlServer && !string.IsNullOrWhiteSpace(connectionString))
{
    useSqlServer = connectionString.Contains(".database.windows.net", StringComparison.OrdinalIgnoreCase)
                || connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase)
                || connectionString.Contains("Initial Catalog=", StringComparison.OrdinalIgnoreCase);
}

var effectiveConnectionString = connectionString
    ?? (useSqlServer ? throw new InvalidOperationException("ConnectionStrings:DefaultConnection manquante.")
                     : "Data Source=tournament.db");

Console.WriteLine($"[DB] Provider : {(useSqlServer ? "SQL Server" : "SQLite")}");
Console.WriteLine($"[DB] Connection : {effectiveConnectionString[..Math.Min(80, effectiveConnectionString.Length)]}...");

builder.Services.AddDbContext<TournamentDbContext>(options =>
{
    if (useSqlServer)
        options.UseSqlServer(effectiveConnectionString);
    else
        options.UseSqlite(effectiveConnectionString);
});

// Domain & Infrastructure Services
builder.Services.AddSingleton<IKumiteBracketGenerator, KumiteBracketGenerator>();
builder.Services.AddSingleton<IKataTournamentManager, KataTournamentManager>();
builder.Services.AddSingleton<IExcelTournamentService, ExcelTournamentService>();

// Centralized Tournament Service with background timer clock
builder.Services.AddSingleton<TournamentService>();
builder.Services.AddSingleton<ITournamentService>(sp => sp.GetRequiredService<TournamentService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<TournamentService>());

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

// Minimal API for Excel Template download
app.MapGet("/api/tournament/excel-template", (IExcelTournamentService excelService) =>
{
    var bytes = excelService.GenerateSampleExcelTemplate();
    return Results.File(
        bytes,
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "Modele_Competition_Kyokushin.xlsx");
});

// Authentication endpoints for Cookie Sign-in and Sign-out
app.MapMethods("/api/auth/login", ["GET", "POST"], async (HttpContext httpContext, IAuthService authService) =>
{
    string username = string.Empty;
    string password = string.Empty;
    string returnUrl = httpContext.Request.Query["returnUrl"].ToString();

    if (httpContext.Request.HasFormContentType)
    {
        var form = await httpContext.Request.ReadFormAsync();
        username = form["username"].ToString().Trim();
        password = form["password"].ToString();
        if (form.ContainsKey("returnUrl"))
        {
            returnUrl = form["returnUrl"].ToString();
        }
    }

    // Support connexions rapides par paramètre query (ex: ?quick=admin)
    string quick = httpContext.Request.Query["quick"].ToString();
    if (!string.IsNullOrWhiteSpace(quick))
    {
        username = quick switch
        {
            "admin" => "admin",
            "table" => "table",
            "tv" => "tv",
            "streamer" => "streamer",
            "collector" => "collector",
            _ => username
        };
        password = quick switch
        {
            "admin" => "admin123",
            "table" => "table123",
            "tv" => "tv123",
            "streamer" => "streamer123",
            "collector" => "collector123",
            _ => password
        };
    }

    var session = authService.Authenticate(username, password);
    if (session == null)
    {
        var errUrl = $"/login?error=invalid&returnUrl={Uri.EscapeDataString(returnUrl)}";
        return Results.Redirect(errUrl);
    }

    var claims = new List<Claim>
    {
        new(ClaimTypes.Name, session.Username),
        new(ClaimTypes.Role, session.Role),
        new("DisplayName", session.DisplayName)
    };
    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

    var target = !string.IsNullOrWhiteSpace(returnUrl) && !returnUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase) && !returnUrl.StartsWith("//")
        ? (returnUrl.StartsWith("/") ? returnUrl : $"/{returnUrl}")
        : (session.Role == "Admin" ? "/admin" : (session.Role == "Table" ? "/tatami/1/controller" : (session.Role == "Streamer" ? "/tatami/1/streamer" : (session.Role == "Collector" ? "/collector" : "/tatami/1/tv"))));

    return Results.Redirect(target);
}).DisableAntiforgery();

app.MapMethods("/api/auth/logout", ["GET", "POST"], async (HttpContext httpContext) =>
{
    await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/");
}).DisableAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
