
using System.Security.Claims;
using m1Chat.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace m1Chat.Controllers;

[Authorize]
[ApiController]
[Route("api/user/apikeys")]
public class UserApiKeysController : ControllerBase
{
    private readonly ApiKeyService _apiKeyService;

    public UserApiKeysController(ApiKeyService apiKeyService)
    {
        _apiKeyService = apiKeyService;
    }

    [HttpGet]
    public async Task<IActionResult> GetKeys()
    {
        var email = User.FindFirst(ClaimTypes.Email)?.Value;
        if (email == null) return Unauthorized();

        var keys = new Dictionary<string, string>
        {
            ["OpenRouter"] = await _apiKeyService.GetUserApiKey(email, "OpenRouter") ?? "",
            ["AIStudio"] = await _apiKeyService.GetUserApiKey(email, "AIStudio") ?? "",
            ["Chutes"] = await _apiKeyService.GetUserApiKey(email, "Chutes") ?? "",
            ["Mistral"] = await _apiKeyService.GetUserApiKey(email, "Mistral") ?? "",
            ["Groq"] = await _apiKeyService.GetUserApiKey(email, "Groq") ?? ""
        };

        return Ok(keys);
    }

    [HttpPost]
    public async Task<IActionResult> SaveKeys([FromBody] Dictionary<string, string> keys)
    {
        var email = User.FindFirst(ClaimTypes.Email)?.Value;
        if (email == null) return Unauthorized();

        foreach (var kvp in keys)
        {
            await _apiKeyService.SaveUserApiKey(email, kvp.Key, kvp.Value);
        }

        return Ok();
    }
}