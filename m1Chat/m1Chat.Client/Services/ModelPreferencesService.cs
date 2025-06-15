using System.Text.Json;
using Microsoft.JSInterop;

namespace m1Chat.Client.Services;

public class ModelPreferencesService
{
    private readonly IJSRuntime _jsRuntime;
    private const string StorageKey = "modelPreferences";
    
    public List<string> EnabledModels { get; private set; } = new();

    public ModelPreferencesService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task InitializeAsync()
    {
        if (OperatingSystem.IsBrowser())
        {
            var json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", StorageKey);
            EnabledModels = string.IsNullOrEmpty(json) 
                ? GetAllAvailableModels() 
                : JsonSerializer.Deserialize<List<string>>(json) ?? GetAllAvailableModels();
        }
        
    }

    public async Task SetEnabledModels(List<string> models)
    {
        EnabledModels = models;
        var json = JsonSerializer.Serialize(models);
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
    }

    public List<string> GetAllAvailableModels() => new()
    {
        "Deepseek v3 (Chutes)",
        "Deepseek R1 0528 (Chutes)",
        "Gemini 2.5 Flash (AI Studio)",
        "Gemini 2.0 Flash (AI Studio)",
        "Devstral Small (Mistral AI)",
        "Mistral Medium (Mistral AI)",
        "Gemini 2.5 Flash (OpenRouter)",
        "Gemma 3 27B (OpenRouter",
        "Llama 4 Maverick (OpenRouter)",
        "Llama 3.1 8B (Groq)",
        "Llama 4 Scout (Groq)"
    };

    public Dictionary<string, string> GetModelProviders() => new()
    {
        ["Deepseek v3 (Chutes)"] = "Chutes",
        ["Deepseek R1 0528 (Chutes)"] = "Chutes",
        ["Gemini 2.5 Flash (AI Studio)"] = "AI Studio",
        ["Gemini 2.0 Flash (AI Studio)"] = "AI Studio",
        ["Devstral Small (Mistral AI)"] = "Mistral AI",
        ["Mistral Medium (Mistral AI)"] = "Mistral AI",
        ["Gemini 2.5 Flash (OpenRouter)"] = "Openrouter",
        ["Gemma 3 27B (OpenRouter)"] = "Openrouter",
        ["Llama 4 Maverick (OpenRouter)"] = "Openrouter",
        ["Llama 3.1 8B (Groq)"] = "Groq",
        ["Llama 4 Scout (Groq)"] = "Groq"
    };
}