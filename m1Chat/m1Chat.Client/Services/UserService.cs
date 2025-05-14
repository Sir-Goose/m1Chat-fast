using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace m1Chat.Client.Services
{
    public class UserService
    {
        private readonly HttpClient _http;

        public UserService(HttpClient http)
        {
            _http = http;
        }

        public async Task<string?> GetUserEmailAsync()
        {
            try
            {
                var result = await _http.GetFromJsonAsync<UserMeResponse>("api/user/me");
                return result?.email;
            }
            catch
            {
                return null;
            }
        }

        private class UserMeResponse
        {
            public string? email { get; set; }
        }
    }
}