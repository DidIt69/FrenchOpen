using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using OpenDeFrance.Core.Models;

namespace OpenDeFrance.Web.Services;

public class CustomAuthStateProvider : AuthenticationStateProvider
{
    private readonly ProtectedLocalStorage _localStorage;
    private readonly ClaimsPrincipal _anonymous = new(new ClaimsIdentity());

    public CustomAuthStateProvider(ProtectedLocalStorage localStorage)
    {
        _localStorage = localStorage;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var result = await _localStorage.GetAsync<UserSession>("TournamentUserSession");
            if (result.Success && result.Value != null)
            {
                var session = result.Value;
                var claims = new List<Claim>
                {
                    new(ClaimTypes.Name, session.Username),
                    new(ClaimTypes.Role, session.Role),
                    new("DisplayName", session.DisplayName)
                };
                var identity = new ClaimsIdentity(claims, "CustomAuth");
                return new AuthenticationState(new ClaimsPrincipal(identity));
            }
        }
        catch
        {
            // Erreur ignorée lors du pré-rendu ou si le stockage n'est pas encore accessible
        }

        return new AuthenticationState(_anonymous);
    }

    public async Task LoginAsync(UserSession session)
    {
        await _localStorage.SetAsync("TournamentUserSession", session);

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, session.Username),
            new(ClaimTypes.Role, session.Role),
            new("DisplayName", session.DisplayName)
        };
        var identity = new ClaimsIdentity(claims, "CustomAuth");
        var user = new ClaimsPrincipal(identity);

        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(user)));
    }

    public async Task LogoutAsync()
    {
        try
        {
            await _localStorage.DeleteAsync("TournamentUserSession");
        }
        catch
        {
        }

        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_anonymous)));
    }
}
