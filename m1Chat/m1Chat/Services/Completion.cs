using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;

using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using m1Chat.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace m1Chat.Services
{
    // Reuse the same DTO as your controller
    public class ChatMessageDto
    {
        public string Role { get; set; }
        public string Content { get; set; }
        public List<Guid>? FileIds { get; set; }
    }

    public class Completion
    {
        private readonly HttpClient _httpClient;
        private readonly string _openRouterApiKey;
        private readonly string _groqApiKey;
        private readonly string _aiStudioApiKey;
        private readonly string _chutesApiKey;
        private readonly string _mistralApiKey;
        private readonly string _freeTierApiKey;
        private readonly string _openRouterUri;
        private readonly string _groqUri;
        private readonly string _aiStudioUri;
        private readonly string _chutesUri;
        private readonly string _mistralUri;
        private readonly string _freeTierUri;
        private readonly string _publicUrl;
        private readonly string _appTitle;
        private Provider _provider;
        private readonly string _systemPrompt;
        private readonly string _magistralSystemPrompt;
        private DateTime _dateTime;
        private readonly ApiKeyService _apiKeyService;

        // Provider constants
        private const string ProviderOpenRouter = "OpenRouter";
        private const string ProviderGroq = "Groq";
        private const string ProviderAiStudio = "AIStudio";
        private const string ProviderChutes = "Chutes";
        private const string ProviderMistral = "Mistral";
        private const string ProviderFreeTier = "Free Tier";

        private enum Provider
        {
            Chutes,
            ChutesKimi,
            OpenRouter,
            AiStudio,
            Groq,
            Mistral,
            FreeTier
        }

        public Completion(ApiKeyService apiKeyService, IConfiguration config)
        {
            _httpClient = new HttpClient();

            // API keys are optional at startup; enforce per-provider when used.
            _openRouterApiKey = GetConfiguredValueOrEmpty(
                config,
                "Providers:OpenRouter:ApiKey",
                "OPENROUTER_API_KEY"
            );
            _groqApiKey = GetConfiguredValueOrEmpty(
                config,
                "Providers:Groq:ApiKey",
                "GROQ_API_KEY"
            );
            _aiStudioApiKey = GetConfiguredValueOrEmpty(
                config,
                "Providers:AiStudio:ApiKey",
                "AISTUDIO_API_KEY"
            );
            _chutesApiKey = GetConfiguredValueOrEmpty(
                config,
                "Providers:Chutes:ApiKey",
                "CHUTES_API_KEY"
            );
            _mistralApiKey = GetConfiguredValueOrEmpty(
                config,
                "Providers:Mistral:ApiKey",
                "MISTRAL_API_KEY"
            );
            _freeTierApiKey = GetConfiguredValueOrEmpty(
                config,
                "Providers:FreeTier:ApiKey",
                "FREE_TIER_API_KEY"
            );

            // --- Retrieve URIs from Environment Variables or use defaults ---
            _openRouterUri = GetConfiguredValueOrDefault(
                config,
                "Providers:OpenRouter:Uri",
                "OPENROUTER_URI",
                "https://openrouter.ai/api/v1/chat/completions"
            );
            _groqUri = GetConfiguredValueOrDefault(
                config,
                "Providers:Groq:Uri",
                "GROQ_URI",
                "https://api.groq.com/openai/v1/chat/completions"
            );
            _aiStudioUri = GetConfiguredValueOrDefault(
                config,
                "Providers:AiStudio:Uri",
                "AISTUDIO_URI",
                "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions"
            );
            _chutesUri = GetConfiguredValueOrDefault(
                config,
                "Providers:Chutes:Uri",
                "CHUTES_URI",
                "https://llm.chutes.ai/v1/chat/completions"
            );
            _mistralUri = GetConfiguredValueOrDefault(
                config,
                "Providers:Mistral:Uri",
                "MISTRAL_URI",
                "https://api.mistral.ai/v1/chat/completions"
            );
            _freeTierUri = GetConfiguredValueOrDefault(
                config,
                "Providers:FreeTier:Uri",
                "FREE_TIER_URI",
                _mistralUri
            );
            _publicUrl = config["App:PublicUrl"] ?? "https://localhost:5001";
            _appTitle = config["App:Title"] ?? "m1Chat";

            _provider = Provider.FreeTier; // Default provider
            _dateTime = DateTime.Now;
            _systemPrompt =
                $"You are M1 Chat, an AI assistant. Your role is to assist and engage in conversation while being helpful, respectful, and engaging.\n- The current date and time including timezone is {_dateTime}.\n- Always use LaTeX for mathematical expressions:\n    - Inline math must be wrapped in single dollar signs: $ content $ \n    - Display math must be wrapped in double dollar signs: $$ content $$\n-   \n- When generating code:\n    - Ensure it is properly formatted using Prettier with a print width of 80 characters\n    - Present it in Markdown code blocks with the correct language extension indicated";
            _magistralSystemPrompt = $"You are M1 Chat, an AI assistant. Your role is to assist and engage in conversation while being helpful, respectful, and engaging.\n- The current date and time including timezone is {_dateTime}.\n- Always use LaTeX for mathematical expressions:\n    - Inline math must be wrapped in single dollar signs: $ content $ \n    - Display math must be wrapped in double dollar signs: $$ content $$\n-   \n- When generating code:\n    - Ensure it is properly formatted using Prettier with a print width of 80 characters\n    - Present it in Markdown code blocks with the correct language extension indicated\n A user will ask you to solve a task. You should first draft your thinking process (inner monologue) until you have derived the final answer. Afterwards, write a self-contained summary of your thoughts (i.e. your summary should be succinct but contain all the critical steps you needed to reach the conclusion). You should use Markdown to format your response. Write both your thoughts and summary in the same language as the task posed by the user. NEVER use \\boxed{{}} in your response.\n\nYour thinking process must follow the template below:\n<think>\nYour thoughts or/and draft, like working through an exercise on scratch paper. Be as casual and as long as you want until you are confident to generate a correct answer.\n</think>\n\nHere, provide a concise summary that reflects your reasoning and presents a clear final answer to the user. Don't mention that this is a summary.\n\nProblem:\n\n";
            _apiKeyService = apiKeyService;
        }

        private static string GetConfiguredValueOrEmpty(
            IConfiguration config,
            string configKey,
            string envVarName
        ) => config[configKey] ?? Environment.GetEnvironmentVariable(envVarName) ?? string.Empty;

        private static string GetConfiguredValueOrDefault(
            IConfiguration config,
            string configKey,
            string envVarName,
            string defaultValue
        ) => config[configKey] ?? Environment.GetEnvironmentVariable(envVarName) ?? defaultValue;

        private static string ResolveApiKeyOrThrow(
            string? userApiKey,
            string? fallbackApiKey,
            string providerLabel,
            string envVarName
        )
        {
            if (!string.IsNullOrWhiteSpace(userApiKey))
            {
                return userApiKey;
            }

            if (!string.IsNullOrWhiteSpace(fallbackApiKey))
            {
                return fallbackApiKey;
            }

            throw new InvalidOperationException(
                $"{providerLabel} API key is missing. Add one in Profile or set '{envVarName}' on the server."
            );
        }

        public async IAsyncEnumerable<string> CompleteAsync(
            List<ChatMessageDto> messages,
            string model,
            string reasoningEffort,
            ChatDbContext db,
            string userEmail
        )
        {
            // Process messages and include file content if database context is provided
            var processedMessages = new List<ChatMessageDto>();

            // Add system prompt if provided
            if (model != "Magistral Medium (Mistral AI)")
            {
                processedMessages.Add(
                    new ChatMessageDto { Role = "system", Content = _systemPrompt }
                );
            }
            else
            {
                processedMessages.Add(
                    new ChatMessageDto { Role = "system", Content = _magistralSystemPrompt }
                );
            }


            foreach (var message in messages)
            {
                var content = message.Content;
                Console.WriteLine(message.FileIds);
                // If message has files, prepend their content
                if (message.FileIds != null && message.FileIds.Any())
                {
                    Console.WriteLine("File detected");
                    var fileContents = new List<string>();
                    foreach (var fileId in message.FileIds)
                    {
                        try
                        {
                            var file = await db.UploadedFiles.FindAsync(fileId);
                            if (file != null && File.Exists(file.FilePath))
                            {
                                var fileContent = await File.ReadAllTextAsync(
                                    file.FilePath
                                );
                                fileContents.Add(
                                    $"--- File: {file.OriginalFileName} ---\n{fileContent}\n--- End of {file.OriginalFileName} ---\n"
                                );
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(
                                $"Error reading file {fileId}: {ex.Message}"
                            );
                        }
                    }

                    if (fileContents.Count != 0)
                    {
                        content = string.Join("\n", fileContents) + "\n" + content;
                    }
                }
                processedMessages.Add(
                    new ChatMessageDto { Role = message.Role, Content = content }
                );
            }

            reasoningEffort = reasoningEffort.ToLower();
            switch (model)
            {
                case "Deepseek v3 (Chutes)":
                    model = "deepseek-ai/DeepSeek-V3-0324";
                    _provider = Provider.Chutes;
                    break;
                case "Kimi Dev (Chutes)":
                    model = "moonshotai/Kimi-Dev-72B";
                    _provider = Provider.ChutesKimi;
                    break;
                case "DeepSeek Prover v2":
                    model = "deepseek/deepseek-prover-v2:free";
                    _provider = Provider.OpenRouter;
                    break;
                case "Gemini 2.5 Pro":
                    model = "google/gemini-2.5-pro-exp-03-25";
                    _provider = Provider.OpenRouter;
                    break;
                case "Deepseek r1":
                    model = "deepseek/deepseek-r1:free";
                    _provider = Provider.OpenRouter;
                    break;
                case "Deepseek R1 0528 (Chutes)":
                    model = "deepseek-ai/DeepSeek-R1-0528";
                    _provider = Provider.Chutes;
                    break;
                case "DeepSeek R1T2 Chimera (Chutes)":
                    model = "tngtech/DeepSeek-TNG-R1T2-Chimera";
                    _provider = Provider.Chutes;
                    break;
                case "Gemini 2.0 Flash (AI Studio)":
                    model = "gemini-2.0-flash";
                    _provider = Provider.AiStudio;
                    break;
                case "Gemini 2.5 Pro (AI Studio)":
                    model = "gemini-2.5-pro";
                    _provider = Provider.AiStudio;
                    break;
                case "Gemini 2.5 Flash (AI Studio)":
                    model = "gemini-2.5-flash-preview-05-20";
                    _provider = Provider.AiStudio;
                    break;
                case "Gemini 2.5 Flash (OpenRouter)":
                    model = "gemini-2.5-flash-preview-05-20";
                    _provider = Provider.OpenRouter;
                    break;
                case "Qwen3 235B":
                    model = "qwen/qwen3-235b-a22b:free";
                    _provider = Provider.OpenRouter;
                    break;
                case "DeepSeek r1 v3 Chimera":
                    model = "tngtech/deepseek-r1t-chimera:free";
                    _provider = Provider.OpenRouter;
                    break;
                case "Gemma 3 27B (OpenRouter)":
                    model = "google/gemma-3-27b-it:free";
                    _provider = Provider.OpenRouter;
                    break;
                case "Moonshot Kimi K2 (OpenRouter)":
                    model = "moonshotai/kimi-k2:free";
                    _provider = Provider.OpenRouter;
                    break;
                case "Qwen3 30B":
                    model = "qwen/qwen3-30b-a3b:free";
                    _provider = Provider.OpenRouter;
                    break;
                case "Llama 4 Maverick (OpenRouter)":
                    model = "meta-llama/llama-4-maverick:free";
                    _provider = Provider.OpenRouter;
                    break;
                case "Cypher Alpha (OpenRouter)":
                    model = "openrouter/cypher-alpha:free";
                    _provider = Provider.OpenRouter;
                    break;
                case "Llama 3.1 8B (Groq)":
                    model = "llama-3.1-8b-instant";
                    _provider = Provider.Groq;
                    break;
                case "Llama 4 Scout (Groq)":
                    model = "meta-llama/llama-4-scout-17b-16e-instruct";
                    _provider = Provider.Groq;
                    break;
                case "Devstral Small (Mistral AI)":
                    model = "devstral-latest";
                    _provider = Provider.Mistral;
                    break;
                case "Devstral Medium (Mistral AI)":
                    model = "devstral-medium-latest";
                    _provider = Provider.Mistral;
                    break;
                case "Magistral Small":
                    model = "magistral-small-latest";
                    _provider = Provider.Mistral;
                    break;
                case "Mistral Medium (Mistral AI)":
                    model = "mistral-medium-latest";
                    _provider = Provider.Mistral;
                    break;
                case "Magistral Medium (Mistral AI)":
                    model = "magistral-medium-latest";
                    _provider = Provider.Mistral;
                    break;
                case "Devstral Small (Free Tier)":
                    model = "devstral-latest";
                    _provider = Provider.FreeTier;
                    break;
                default:
                    model = "devstral-latest";
                    _provider = Provider.FreeTier;
                    break;
            }

            switch (_provider)
            {
                case Provider.OpenRouter:
                    await foreach (
                        var chunk in StreamOpenRouterAsync(
                            processedMessages,
                            model,
                            reasoningEffort,
                            userEmail
                        )
                    )
                    {
                        yield return chunk;
                    }
                    break;
                case Provider.AiStudio:
                    await foreach (
                        var chunk in StreamAiStudioAsync(
                            processedMessages,
                            model,
                            reasoningEffort,
                            userEmail
                        )
                    )
                    {
                        yield return chunk;
                    }
                    break;
                case Provider.Groq:
                    await foreach (
                        var chunk in StreamGroqAsync(processedMessages, model, userEmail)
                    )
                    {
                        yield return chunk;
                    }
                    break;
                case Provider.Chutes:
                    await foreach (
                        var chunk in StreamChutesAsync(processedMessages, model, userEmail)
                    )
                    {
                        yield return chunk;
                    }
                    break;
                case Provider.ChutesKimi:
                    await foreach (
                        var chunk in StreamChutesKimiAsync(processedMessages, model, userEmail)
                    )
                    {
                        yield return chunk;
                    }
                    break;
                case Provider.Mistral:
                    await foreach (
                        var chunk in StreamMistralAsync(processedMessages, model, userEmail)
                    )
                    {
                        yield return chunk;
                    }
                    break;
                case Provider.FreeTier:
                    await foreach (
                        var chunk in StreamFreeTierAsync(processedMessages, model, userEmail)
                    )
                    {
                        yield return chunk;
                    }
                    break;
                default:
                    await foreach (
                        var chunk in StreamFreeTierAsync(processedMessages, model, userEmail)
                    )
                    {
                        yield return chunk;
                    }
                    break;
            }
        }

        private async IAsyncEnumerable<string> StreamOpenRouterAsync(
            List<ChatMessageDto> messages,
            string model,
            string reasoningEffort,
            string userEmail
        )
        {
            var requestBody = new
            {
                model,
                reasoningEffort,
                messages = messages.Select(m =>
                {
                    return new { role = m.Role, content = m.Content };
                }),
                stream = true
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Get user's API key or use default
            var userApiKey = await _apiKeyService.GetUserApiKey(userEmail, ProviderOpenRouter);
            var apiKey = ResolveApiKeyOrThrow(
                userApiKey,
                _openRouterApiKey,
                "OpenRouter",
                "OPENROUTER_API_KEY"
            );


            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);
            _httpClient.DefaultRequestHeaders.Remove("HTTP-Referer");
            _httpClient.DefaultRequestHeaders.Add("HTTP-Referer", _publicUrl);
            _httpClient.DefaultRequestHeaders.Remove("X-Title");
            _httpClient.DefaultRequestHeaders.Add("X-Title", _appTitle);

            using var response = await _httpClient.SendAsync(
                new HttpRequestMessage(HttpMethod.Post, _openRouterUri)
                {
                    Content = content
                },
                HttpCompletionOption.ResponseHeadersRead
            );

            Console.WriteLine($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Error response content: {error}");
                throw new Exception(
                    $"OpenRouter API error: {response.StatusCode} - {error}"
                );
            }

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);

            bool inReasoningBlock = false;

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                if (line.StartsWith("data: "))
                {
                    var jsonData = line["data: ".Length..].Trim();
                    if (jsonData == "[DONE]")
                        break;

                    var (contentChunk, reasoning) =
                        TryParseContentChunkOpenrouter(jsonData);
                    if (!string.IsNullOrEmpty(reasoning))
                    {
                        if (!inReasoningBlock)
                        {
                            yield return "```Thinking ";
                            inReasoningBlock = true;
                        }

                        yield return reasoning;
                    }
                    else if (!string.IsNullOrEmpty(contentChunk))
                    {
                        if (inReasoningBlock)
                        {
                            yield return "```\n";
                            inReasoningBlock = false;
                        }

                        yield return contentChunk;
                    }
                }
            }

            if (inReasoningBlock)
            {
                yield return "```\n";
            }
        }

        private async IAsyncEnumerable<string> StreamChutesAsync(
            List<ChatMessageDto> messages,
            string model,
            string userEmail
        )
        {
            var requestBody = new
            {
                model,
                messages = messages.Select(m =>
                {
                    return new { role = m.Role, content = m.Content };
                }),
                stream = true
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Get user's API key or use default
            var userApiKey = await _apiKeyService.GetUserApiKey(userEmail, ProviderChutes);
            var apiKey = ResolveApiKeyOrThrow(
                userApiKey,
                _chutesApiKey,
                "Chutes",
                "CHUTES_API_KEY"
            );

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);
            _httpClient.DefaultRequestHeaders.Remove("HTTP-Referer");
            _httpClient.DefaultRequestHeaders.Add("HTTP-Referer", _publicUrl);
            _httpClient.DefaultRequestHeaders.Remove("X-Title");
            _httpClient.DefaultRequestHeaders.Add("X-Title", _appTitle);

            using var response = await _httpClient.SendAsync(
                new HttpRequestMessage(HttpMethod.Post, _chutesUri)
                {
                    Content = content
                },
                HttpCompletionOption.ResponseHeadersRead
            );

            Console.WriteLine($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Error response content: {error}");
                throw new Exception(
                    $"Chutes API error: {response.StatusCode} - {error}"
                );
            }

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);

            var inReasoningBlock = false;

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                if (!line.StartsWith("data: "))
                    continue;
                var jsonData = line["data: ".Length..].Trim();
                if (jsonData == "[DONE]")
                    break;
                var contentChunk = TryParseContentChunk(jsonData);
                if (string.IsNullOrEmpty(contentChunk))
                    continue;
                switch (contentChunk)
                {
                    case "<think>":
                        yield return "```Thinking ";
                        inReasoningBlock = true;
                        break;
                    case "</think>":
                        yield return "```\n" + "\n";
                        inReasoningBlock = false;
                        break;
                    default:
                        {
                            if (!inReasoningBlock)
                            {
                                yield return contentChunk;
                            }
                            else
                            {
                                yield return contentChunk.Replace("```", "'''");
                            }

                            break;
                        }
                }
            }
        }

        private async IAsyncEnumerable<string> StreamChutesKimiAsync(
            List<ChatMessageDto> messages,
            string model,
            string userEmail
        )
        {
            var requestBody = new
            {
                model,
                messages = messages.Select(m =>
                {
                    return new { role = m.Role, content = m.Content };
                }),
                stream = true
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Get user's API key or use default
            var userApiKey = await _apiKeyService.GetUserApiKey(userEmail, ProviderChutes);
            var apiKey = ResolveApiKeyOrThrow(
                userApiKey,
                _chutesApiKey,
                "Chutes",
                "CHUTES_API_KEY"
            );

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);
            _httpClient.DefaultRequestHeaders.Remove("HTTP-Referer");
            _httpClient.DefaultRequestHeaders.Add("HTTP-Referer", _publicUrl);
            _httpClient.DefaultRequestHeaders.Remove("X-Title");
            _httpClient.DefaultRequestHeaders.Add("X-Title", _appTitle);

            using var response = await _httpClient.SendAsync(
                new HttpRequestMessage(HttpMethod.Post, _chutesUri)
                {
                    Content = content
                },
                HttpCompletionOption.ResponseHeadersRead
            );

            Console.WriteLine($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Error response content: {error}");
                throw new Exception(
                    $"Chutes API error: {response.StatusCode} - {error}"
                );
            }

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);

            var inReasoningBlock = false;

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                if (!line.StartsWith("data: "))
                    continue;
                var jsonData = line["data: ".Length..].Trim();
                if (jsonData == "[DONE]")
                    break;
                var contentChunk = TryParseContentChunk(jsonData);
                if (string.IsNullOrEmpty(contentChunk))
                    continue;
                if (contentChunk == "◁")
                {
                    if (!inReasoningBlock)
                    {
                        yield return "```Thinking ";
                        inReasoningBlock = true;
                    }
                    else
                    {
                        yield return "```\n" + "\n";
                        inReasoningBlock = false;
                    }
                }
                if (!inReasoningBlock)
                {
                    yield return contentChunk;
                }
                else
                {
                    yield return contentChunk.Replace("```", "'''");
                }
            }
        }

        private async IAsyncEnumerable<string> StreamGroqAsync(
            List<ChatMessageDto> messages,
            string model,
            string userEmail
        )
        {
            var requestBody = new
            {
                model,
                messages = messages.Select(m =>
                {
                    return new { role = m.Role, content = m.Content };
                }),
                stream = true
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var userApiKey = await _apiKeyService.GetUserApiKey(userEmail, ProviderGroq);
            var apiKey = ResolveApiKeyOrThrow(
                userApiKey,
                _groqApiKey,
                "Groq",
                "GROQ_API_KEY"
            );

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);
            _httpClient.DefaultRequestHeaders.Remove("HTTP-Referer");
            _httpClient.DefaultRequestHeaders.Add("HTTP-Referer", _publicUrl);
            _httpClient.DefaultRequestHeaders.Remove("X-Title");
            _httpClient.DefaultRequestHeaders.Add("X-Title", _appTitle);

            using var response = await _httpClient.SendAsync(
                new HttpRequestMessage(HttpMethod.Post, _groqUri)
                {
                    Content = content
                },
                HttpCompletionOption.ResponseHeadersRead
            );

            Console.WriteLine($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Error response content: {error}");
                throw new Exception($"Groq API error: {response.StatusCode} - {error}");
            }

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                if (line.StartsWith("data: "))
                {
                    var jsonData = line["data: ".Length..].Trim();
                    if (jsonData == "[DONE]")
                        break;
                    var chunk = TryParseContentChunk(jsonData);
                    if (!string.IsNullOrEmpty(chunk))
                        yield return chunk;
                }
            }
        }

        private async IAsyncEnumerable<string> StreamAiStudioAsync(
            List<ChatMessageDto> messages,
            string model,
            string reasoningEffort,
            string userEmail
        )
        {
            var requestBody = new
            {
                model,
                reasoningEffort,
                messages = messages.Select(m =>
                {
                    return new { role = m.Role, content = m.Content };
                }),
                stream = true,
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Get user's API key or use default
            var userApiKey = await _apiKeyService.GetUserApiKey(userEmail, ProviderAiStudio);
            var apiKey = ResolveApiKeyOrThrow(
                userApiKey,
                _aiStudioApiKey,
                "AI Studio",
                "AISTUDIO_API_KEY"
            );

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);
            _httpClient.DefaultRequestHeaders.Remove("HTTP-Referer");
            _httpClient.DefaultRequestHeaders.Add("HTTP-Referer", _publicUrl);
            _httpClient.DefaultRequestHeaders.Remove("X-Title");
            _httpClient.DefaultRequestHeaders.Add("X-Title", _appTitle);

            using var response = await _httpClient.SendAsync(
                new HttpRequestMessage(HttpMethod.Post, _aiStudioUri)
                {
                    Content = content
                },
                HttpCompletionOption.ResponseHeadersRead
            );

            Console.WriteLine($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Error response content: {error}");
                throw new Exception(
                    $"AI Studio API error: {response.StatusCode} - {error}"
                );
            }

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                if (line.StartsWith("data: "))
                {
                    var jsonData = line["data: ".Length..].Trim();
                    if (jsonData == "[DONE]")
                        break;
                    var chunk = TryParseContentChunk(jsonData);
                    if (!string.IsNullOrEmpty(chunk))
                        yield return chunk;
                }
            }
        }

        private async IAsyncEnumerable<string> StreamMistralAsync(
            List<ChatMessageDto> messages,
            string model,
            string userEmail
        )
        {
            var requestBody = new
            {
                model,
                messages = messages.Select(m =>
                {
                    return new { role = m.Role, content = m.Content };
                }),
                stream = true
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Get user's API key or use default
            var userApiKey = await _apiKeyService.GetUserApiKey(userEmail, ProviderMistral);
            var fallbackMistralKey = string.IsNullOrWhiteSpace(_mistralApiKey)
                ? _freeTierApiKey
                : _mistralApiKey;
            var apiKey = ResolveApiKeyOrThrow(
                userApiKey,
                fallbackMistralKey,
                "Mistral",
                "MISTRAL_API_KEY"
            );

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);
            _httpClient.DefaultRequestHeaders.Remove("HTTP-Referer");
            _httpClient.DefaultRequestHeaders.Add("HTTP-Referer", _publicUrl);
            _httpClient.DefaultRequestHeaders.Remove("X-Title");
            _httpClient.DefaultRequestHeaders.Add("X-Title", _appTitle);

            using var response = await _httpClient.SendAsync(
                new HttpRequestMessage(HttpMethod.Post, _mistralUri)
                {
                    Content = content
                },
                HttpCompletionOption.ResponseHeadersRead
            );

            Console.WriteLine($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Error response content: {error}");
                throw new Exception(
                    $"Mistral API error: {response.StatusCode} - {error}"
                );
            }

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);

            var inReasoningBlock = false;
            var emittedAnyChunk = false;

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                if (!line.StartsWith("data: "))
                    continue;
                var jsonData = line["data: ".Length..].Trim();
                if (jsonData == "[DONE]")
                    break;
                var contentChunk = TryParseContentChunk(jsonData);
                if (string.IsNullOrEmpty(contentChunk))
                    continue;
                switch (contentChunk)
                {
                    case "<th":
                        yield return "```Thinking ";
                        inReasoningBlock = true;
                        break;
                    case "</":
                        yield return "```\n" + "\n";
                        inReasoningBlock = false;
                        break;
                    default:
                        {
                            if (!inReasoningBlock)
                            {
                                yield return contentChunk;
                                emittedAnyChunk = true;
                            }
                            else
                            {
                                yield return contentChunk.Replace("```", "'''");
                                emittedAnyChunk = true;
                            }

                            break;
                        }
                }
            }

            if (!emittedAnyChunk)
            {
                var fallback = await GetMistralNonStreamingFallbackAsync(
                    messages,
                    model,
                    apiKey,
                    _mistralUri
                );

                if (!string.IsNullOrWhiteSpace(fallback))
                {
                    yield return fallback;
                    emittedAnyChunk = true;
                }
            }

            if (!emittedAnyChunk)
            {
                throw new Exception(
                    "Mistral stream completed without any text chunks, and fallback completion was empty."
                );
            }
        }

        private async IAsyncEnumerable<string> StreamFreeTierAsync(
            List<ChatMessageDto> messages,
            string model,
            string userEmail
        )
        {
            var requestBody = new
            {
                model,
                messages = messages.Select(m =>
                {
                    return new { role = m.Role, content = m.Content };
                }),
                stream = true
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var apiKey = ResolveApiKeyOrThrow(
                null,
                _freeTierApiKey,
                "Free Tier",
                "FREE_TIER_API_KEY"
            );

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);
            _httpClient.DefaultRequestHeaders.Remove("HTTP-Referer");
            _httpClient.DefaultRequestHeaders.Add("HTTP-Referer", _publicUrl);
            _httpClient.DefaultRequestHeaders.Remove("X-Title");
            _httpClient.DefaultRequestHeaders.Add("X-Title", _appTitle);

            using var response = await _httpClient.SendAsync(
                new HttpRequestMessage(HttpMethod.Post, _freeTierUri)
                {
                    Content = content
                },
                HttpCompletionOption.ResponseHeadersRead
            );

            Console.WriteLine($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Error response content: {error}");
                throw new Exception(
                    $"Mistral API error: {response.StatusCode} - {error}"
                );
            }

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);

            var inReasoningBlock = false;
            var emittedAnyChunk = false;

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                if (!line.StartsWith("data: "))
                    continue;
                var jsonData = line["data: ".Length..].Trim();
                if (jsonData == "[DONE]")
                    break;
                var contentChunk = TryParseContentChunk(jsonData);
                if (string.IsNullOrEmpty(contentChunk))
                    continue;
                switch (contentChunk)
                {
                    case "<th":
                        yield return "```Thinking ";
                        inReasoningBlock = true;
                        break;
                    case "</":
                        yield return "```\n" + "\n";
                        inReasoningBlock = false;
                        break;
                    default:
                        {
                            if (!inReasoningBlock)
                            {
                                yield return contentChunk;
                                emittedAnyChunk = true;
                            }
                            else
                            {
                                yield return contentChunk.Replace("```", "'''");
                                emittedAnyChunk = true;
                            }

                            break;
                        }
                }
            }

            if (!emittedAnyChunk)
            {
                var fallback = await GetMistralNonStreamingFallbackAsync(
                    messages,
                    model,
                    apiKey,
                    _freeTierUri
                );

                if (!string.IsNullOrWhiteSpace(fallback))
                {
                    yield return fallback;
                    emittedAnyChunk = true;
                }
            }

            if (!emittedAnyChunk)
            {
                throw new Exception(
                    "Free-tier stream completed without any text chunks, and fallback completion was empty."
                );
            }
        }

        private async Task<string?> GetMistralNonStreamingFallbackAsync(
            List<ChatMessageDto> messages,
            string model,
            string apiKey,
            string endpointUri
        )
        {
            var requestBody = new
            {
                model,
                messages = messages.Select(m =>
                {
                    return new { role = m.Role, content = m.Content };
                }),
                stream = false
            };

            var json = JsonSerializer.Serialize(requestBody);
            using var request = new HttpRequestMessage(HttpMethod.Post, endpointUri)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Headers.TryAddWithoutValidation("HTTP-Referer", _publicUrl);
            request.Headers.TryAddWithoutValidation("X-Title", _appTitle);

            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine(
                    $"Fallback non-stream request failed: {response.StatusCode} - {error}"
                );
                return null;
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            try
            {
                using var doc = JsonDocument.Parse(responseJson);
                var root = doc.RootElement;

                if (
                    root.TryGetProperty("choices", out var choices)
                    && choices.ValueKind == JsonValueKind.Array
                    && choices.GetArrayLength() > 0
                )
                {
                    var choice = choices[0];
                    if (choice.TryGetProperty("message", out var message))
                    {
                        if (message.TryGetProperty("content", out var content))
                        {
                            return ExtractTextFromContentElement(content);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"Failed to parse fallback non-stream completion response: {ex.Message}"
                );
            }

            return null;
        }

        private string? TryParseContentChunk(string jsonData)
        {
            try
            {
                using var doc = JsonDocument.Parse(jsonData);
                var root = doc.RootElement;
                var choices = root.GetProperty("choices");
                if (choices.GetArrayLength() > 0)
                {
                    var choice = choices[0];

                    if (choice.TryGetProperty("delta", out var delta))
                    {
                        if (delta.TryGetProperty("content", out var c))
                        {
                            var parsed = ExtractTextFromContentElement(c);
                            if (!string.IsNullOrWhiteSpace(parsed))
                            {
                                return parsed;
                            }
                        }

                        if (delta.TryGetProperty("text", out var t))
                        {
                            var parsed = ExtractTextFromContentElement(t);
                            if (!string.IsNullOrWhiteSpace(parsed))
                            {
                                return parsed;
                            }
                        }

                        if (delta.TryGetProperty("reasoning_content", out var r))
                        {
                            var parsed = ExtractTextFromContentElement(r);
                            if (!string.IsNullOrWhiteSpace(parsed))
                            {
                                return parsed;
                            }
                        }
                    }

                    if (choice.TryGetProperty("message", out var message))
                    {
                        if (message.TryGetProperty("content", out var messageContent))
                        {
                            var parsed = ExtractTextFromContentElement(messageContent);
                            if (!string.IsNullOrWhiteSpace(parsed))
                            {
                                return parsed;
                            }
                        }
                    }
                }

                if (root.TryGetProperty("content", out var rootContent))
                {
                    var parsed = ExtractTextFromContentElement(rootContent);
                    if (!string.IsNullOrWhiteSpace(parsed))
                    {
                        return parsed;
                    }
                }

                if (root.TryGetProperty("delta", out var rootDelta))
                {
                    var parsed = ExtractTextFromContentElement(rootDelta);
                    if (!string.IsNullOrWhiteSpace(parsed))
                    {
                        return parsed;
                    }
                }
            }
            catch (JsonException ex)
            {
                Console.WriteLine(
                    $"Error parsing JSON chunk: {ex.Message}. Data: {jsonData}"
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"Generic error parsing chunk: {ex.Message}. Data: {jsonData}"
                );
            }

            return null;
        }

        private static string? ExtractTextFromContentElement(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    return element.GetString();
                case JsonValueKind.Array:
                {
                    var sb = new StringBuilder();
                    foreach (var item in element.EnumerateArray())
                    {
                        var piece = ExtractTextFromContentElement(item);
                        if (!string.IsNullOrEmpty(piece))
                        {
                            sb.Append(piece);
                        }
                    }

                    return sb.Length == 0 ? null : sb.ToString();
                }
                case JsonValueKind.Object:
                {
                    if (
                        element.TryGetProperty("type", out var type)
                        && type.ValueKind == JsonValueKind.String
                    )
                    {
                        var typeValue = type.GetString();
                        if (typeValue == "text" && element.TryGetProperty("text", out var text))
                        {
                            return ExtractTextFromContentElement(text);
                        }
                    }

                    if (element.TryGetProperty("text", out var textProp))
                    {
                        var text = ExtractTextFromContentElement(textProp);
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            return text;
                        }
                    }

                    if (element.TryGetProperty("content", out var contentProp))
                    {
                        var text = ExtractTextFromContentElement(contentProp);
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            return text;
                        }
                    }

                    if (element.TryGetProperty("value", out var valueProp))
                    {
                        var text = ExtractTextFromContentElement(valueProp);
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            return text;
                        }
                    }

                    return null;
                }
                default:
                    return null;
            }
        }

        private (string? content, string? reasoning) TryParseContentChunkOpenrouter(
            string jsonData
        )
        {
            try
            {
                using var doc = JsonDocument.Parse(jsonData);
                var root = doc.RootElement;
                var choices = root.GetProperty("choices");
                if (choices.GetArrayLength() > 0)
                {
                    var delta = choices[0].GetProperty("delta");
                    string? content = null;
                    string? reasoning = null;

                    // Extract content if present
                    if (
                        delta.TryGetProperty("content", out var c) &&
                        c.ValueKind == JsonValueKind.String
                    )
                    {
                        content = c.GetString();
                    }

                    // Extract reasoning if present
                    if (
                        delta.TryGetProperty("reasoning", out var r) &&
                        r.ValueKind == JsonValueKind.String
                    )
                    {
                        reasoning = r.GetString();
                    }

                    return (content, reasoning);
                }
            }
            catch (JsonException ex)
            {
                Console.WriteLine(
                    $"Error parsing JSON chunk: {ex.Message}. Data: {jsonData}"
                );
            }

            return (null, null);
        }
    }
}
