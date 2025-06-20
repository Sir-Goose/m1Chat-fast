using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace m1Chat.Client
{
    public class CustomAuthStateProvider : AuthenticationStateProvider
    {
        private readonly HttpClient _http;
        
        public CustomAuthStateProvider(HttpClient http)
        {
            _http = http;
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            try
            {
                // Check auth status
                var authResponse = await _http.GetFromJsonAsync<AuthResponse>("auth/check");
                
                if (authResponse?.IsAuthenticated == true)
                {
                    // Get user email
                    var userResponse = await _http.GetFromJsonAsync<UserResponse>("api/user/me");
                    var email = userResponse?.Email ?? "unknown";
                    
                    // Create claims identity
                    var identity = new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.Email, email)
                    }, "auth");
                    
                    return new AuthenticationState(new ClaimsPrincipal(identity));
                }
                return new AuthenticationState(new ClaimsPrincipal());
            }
            catch
            {
                return new AuthenticationState(new ClaimsPrincipal());
            }
        }
    }

    public class AuthResponse
    {
        public bool IsAuthenticated { get; set; }
    }

    public class UserResponse
    {
        public string? Email { get; set; }
    }
}