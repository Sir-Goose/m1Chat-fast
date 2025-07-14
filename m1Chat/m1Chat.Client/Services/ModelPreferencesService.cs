using System.Text.Json;
using Microsoft.JSInterop;

namespace m1Chat.Client.Services;

public class ModelPreferencesService
{
    private readonly IJSRuntime _jsRuntime;
    private const string StorageKey = "modelPreferences";
    private const string DefaultModelKey = "defaultModel";

    public List<string> EnabledModels { get; private set; } = new();
    public string DefaultModel { get; private set; } = "";

    public ModelPreferencesService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task InitializeAsync()
    {
        if (OperatingSystem.IsBrowser())
        {
            var json = await _jsRuntime.InvokeAsync<string>(
                "localStorage.getItem",
                StorageKey
            );

            // First ternary converted to if/else
            if (string.IsNullOrEmpty(json))
            {
                EnabledModels = GetAllAvailableModels();
            }
            else
            {
                var deserializedModels = JsonSerializer.Deserialize<List<string>>(json);
                if (deserializedModels == null)
                {
                    EnabledModels = GetAllAvailableModels();
                }
                else
                {
                    EnabledModels = deserializedModels;
                }
            }

            json = await _jsRuntime.InvokeAsync<string>(
                "localStorage.getItem",
                DefaultModelKey
            );

            // Second ternary converted to if/else
            if (string.IsNullOrEmpty(json))
            {
                DefaultModel = "Devstral Small (Free Tier)";
            }
            else
            {
                var deserializedModel = JsonSerializer.Deserialize<string>(json);
                if (deserializedModel == null)
                {
                    DefaultModel = "Devstral Small (Free Tier)";
                }
                else
                {
                    DefaultModel = deserializedModel;
                }
            }
        }
    }

    public async Task SetEnabledModels(List<string> models)
    {
        EnabledModels = models;
        var json = JsonSerializer.Serialize(models);
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
    }

    public async Task SetDefaultModel(string defaultModel)
    {
        DefaultModel = defaultModel;
        var json = JsonSerializer.Serialize(defaultModel);
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", DefaultModelKey, json);
    }

    public List<string> GetAllAvailableModels() => new()
    {
        "Devstral Small (Free Tier)",
        "Gemini 2.5 Flash (AI Studio)",
        "Gemini 2.0 Flash (AI Studio)",
        "Gemini 2.5 Pro (AI Studio)",
        "Deepseek v3 (Chutes)",
        "DeepSeek R1T2 Chimera (Chutes)",
        "Deepseek R1 0528 (Chutes)",
        "Kimi Dev (Chutes)",
        "Devstral Small (Mistral AI)",
        "Devstral Medium (Mistral AI)",
        "Mistral Medium (Mistral AI)",
        "Magistral Medium (Mistral AI)",
        "Moonshot Kimi K2 (OpenRouter)",
        "Gemini 2.5 Flash (OpenRouter)",
        "Gemma 3 27B (OpenRouter)",
        "Llama 4 Maverick (OpenRouter)",
        "Cypher Alpha (OpenRouter)",
        "Llama 3.1 8B (Groq)",
        "Llama 4 Scout (Groq)"
    };

    public Dictionary<string, string> GetModelProviders() => new()
    {
        ["Devstral Small (Free Tier)"] = "Free Tier",
        ["Deepseek v3 (Chutes)"] = "Chutes",
        ["Deepseek R1 0528 (Chutes)"] = "Chutes",
        ["DeepSeek R1T2 Chimera (Chutes)"] = "Chutes",
        ["Kimi Dev (Chutes)"] = "Chutes",
        ["Gemini 2.5 Pro (AI Studio)"] = "AI Studio",
        ["Gemini 2.5 Flash (AI Studio)"] = "AI Studio",
        ["Gemini 2.0 Flash (AI Studio)"] = "AI Studio",
        ["Devstral Small (Mistral AI)"] = "Mistral AI",
        ["Mistral Medium (Mistral AI)"] = "Mistral AI",
        ["Devstral Medium (Mistral AI)"] = "Mistral AI",
        ["Magistral Medium (Mistral AI)"] = "Mistral AI",
        ["Moonshot Kimi K2 (OpenRouter)"] = "Openrouter",
        ["Gemini 2.5 Flash (OpenRouter)"] = "Openrouter",
        ["Gemma 3 27B (OpenRouter)"] = "Openrouter",
        ["Llama 4 Maverick (OpenRouter)"] = "Openrouter",
        ["Cypher Alpha (OpenRouter)"] = "Openrouter",
        ["Llama 3.1 8B (Groq)"] = "Groq",
        ["Llama 4 Scout (Groq)"] = "Groq"
    };
}
