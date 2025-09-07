using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace Audiarr.Services.Auth;

public class BlazorAuthStateProvider : AuthenticationStateProvider
{
    private readonly ProtectedLocalStorage _localStorage;
    private readonly ClaimsPrincipal _anonymous = new(new ClaimsIdentity());

    public BlazorAuthStateProvider(ProtectedLocalStorage localStorage)
    {
        _localStorage = localStorage;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var tokenResult = await _localStorage.GetAsync<string>("accessToken");
            var usernameResult = await _localStorage.GetAsync<string>("username");
            var roleResult = await _localStorage.GetAsync<string>("userRole");

            if (!tokenResult.Success || string.IsNullOrEmpty(tokenResult.Value))
            {
                return new AuthenticationState(_anonymous);
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, usernameResult.Value ?? ""),
                new Claim(ClaimTypes.Role, roleResult.Value ?? "user")
            };

            var identity = new ClaimsIdentity(claims, "jwt");
            var user = new ClaimsPrincipal(identity);

            return new AuthenticationState(user);
        }
        catch
        {
            return new AuthenticationState(_anonymous);
        }
    }

    public void MarkUserAsAuthenticated(string username, string role)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Role, role)
        };

        var identity = new ClaimsIdentity(claims, "jwt");
        var user = new ClaimsPrincipal(identity);

        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(user)));
    }

    public async Task MarkUserAsLoggedOut()
    {
        await _localStorage.DeleteAsync("accessToken");
        await _localStorage.DeleteAsync("refreshToken");
        await _localStorage.DeleteAsync("username");
        await _localStorage.DeleteAsync("userRole");

        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_anonymous)));
    }
}