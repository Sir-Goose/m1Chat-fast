using m1Chat.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Threading.Tasks;

namespace m1Chat.Services
{
    public class ApiKeyService
    {
        private readonly ChatDbContext _db;

        public ApiKeyService(ChatDbContext db)
        {
            _db = db;
        }

        public async Task<string> GetUserApiKey(string email, string provider)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null) return null;

            var key = await _db.UserApiKeys
                .FirstOrDefaultAsync(k => k.UserId == user.Id && k.Provider == provider);

            return key?.ApiKey;
        }

        public async Task SaveUserApiKey(string email, string provider, string apiKey)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null) return;

            var existingKey = await _db.UserApiKeys
                .FirstOrDefaultAsync(k => k.UserId == user.Id && k.Provider == provider);

            if (existingKey != null)
            {
                existingKey.ApiKey = apiKey;
            }
            else
            {
                _db.UserApiKeys.Add(new UserApiKey
                {
                    UserId = user.Id,
                    Provider = provider,
                    ApiKey = apiKey
                });
            }

            await _db.SaveChangesAsync();
        }
    }
}