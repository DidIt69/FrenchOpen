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

// ─── Configuration de la base de données ───────────────────────────────────
// Priorité de configuration (du plus fort au plus faible) :
//   1. Variables d'environnement Azure App Service
//   2. appsettings.{Environment}.json
//   3. appsettings.json
//
// Dans Azure App Service, définir :
//   - Application Setting  : DatabaseProvider = SqlServer
//   - Connection String    : DefaultConnection = Server=tcp:... (type: Custom)
//     OU Application Setting : ConnectionStrings__DefaultConnection = Server=tcp:...

var dbProvider   = builder.Configuration["DatabaseProvider"] ?? "Sqlite";
var connStr      = builder.Configuration.GetConnectionString("DefaultConnection")
                ?? builder.Configuration["ConnectionStrings__DefaultConnection"]
                ?? builder.Configuration["DefaultConnection"];  // fallback si mis comme App Setting simple

// ─── Diagnostics de démarrage ─────────────────────────────────────────────
var env = builder.Environment.EnvironmentName;
Console.Error.WriteLine("╔══════════════════════════════════════════════════════╗");
Console.Error.WriteLine($"║  ENVIRONMENT  : {env,-36}║");
Console.Error.WriteLine($"║  DatabaseProvider : {dbProvider,-33}║");
Console.Error.WriteLine($"║  ConnStr trouvée  : {(connStr != null ? "OUI" : "NON"),-33}║");
if (connStr != null)
{
    var preview = connStr.Length > 60 ? connStr[..60] + "..." : connStr;
    Console.Error.WriteLine($"║  ConnStr début    : {preview,-33}║");
}
Console.Error.WriteLine("╚══════════════════════════════════════════════════════╝");

bool useSqlServer = dbProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase);

if (useSqlServer && string.IsNullOrWhiteSpace(connStr))
{
    Console.Error.WriteLine("[ERREUR] DatabaseProvider=SqlServer mais aucune connection string trouvée !");
    throw new InvalidOperationException(
        "DatabaseProvider=SqlServer mais ConnectionStrings:DefaultConnection est vide. " +
        "Vérifiez la configuration Azure App Service.");
}

var effectiveConnStr = connStr ?? "Data Source=tournament.db";

builder.Services.AddDbContext<TournamentDbContext>(options =>
{
    if (useSqlServer)
    {
        options.UseSqlServer(effectiveConnStr);
        Console.Error.WriteLine("[DB] ✅ Utilisation de SQL SERVER");
    }
    else
    {
        options.UseSqlite(effectiveConnStr);
        Console.Error.WriteLine("[DB] ⚠️  Utilisation de SQLITE");
    }
});
// ─────────────────────────────────────────────────────────────────────────

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
