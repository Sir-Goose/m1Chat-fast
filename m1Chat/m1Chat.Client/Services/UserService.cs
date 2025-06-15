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

        public async Task<UserStatsResponse?> GetUserStatsAsync()
        {
            try
            {
                return await _http.GetFromJsonAsync<UserStatsResponse>("api/user/stats");
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Error fetching user stats: {ex.Message}");
                return null;
            }
        }
        
        public async Task<Dictionary<string, string>> GetUserApiKeysAsync()
        {
            return await _http.GetFromJsonAsync<Dictionary<string, string>>("api/user/apikeys") 
                   ?? new Dictionary<string, string>();
        }

        public async Task SaveUserApiKeysAsync(Dictionary<string, string> keys)
        {
            await _http.PostAsJsonAsync("api/user/apikeys", keys);
        }


        private class UserMeResponse
        {
            public string? email { get; set; }
        }

        public class UserStatsResponse
        {
            public int TotalChats { get; set; }
            public int TotalMessages { get; set; }
        }
    }
}