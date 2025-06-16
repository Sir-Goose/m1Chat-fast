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
        private readonly string _openRouterUri;
        private readonly string _groqUri;
        private readonly string _aiStudioUri;
        private readonly string _chutesUri;
        private readonly string _mistralUri;
        private Provider _provider;
        private readonly string _systemPrompt;
        private DateTime _dateTime;
        private readonly ApiKeyService _apiKeyService;

        // Provider constants
        public const string ProviderOpenRouter = "OpenRouter";
        public const string ProviderGroq = "Groq";
        public const string ProviderAIStudio = "AIStudio";
        public const string ProviderChutes = "Chutes";
        public const string ProviderMistral = "Mistral";

        private enum Provider
        {
            Chutes,
            OpenRouter,
            AiStudio,
            Groq,
            Mistral
        }

        public Completion(ApiKeyService apiKeyService)
        {
            _httpClient = new HttpClient();

            // --- Retrieve API Keys from Environment Variables ---
            _openRouterApiKey = GetEnvironmentVariableOrThrow(
                "OPENROUTER_API_KEY",
                "OpenRouter API key"
            );
            _groqApiKey = GetEnvironmentVariableOrThrow("GROQ_API_KEY", "Groq API key");
            _aiStudioApiKey = GetEnvironmentVariableOrThrow(
                "AISTUDIO_API_KEY",
                "AI Studio API key"
            );
            _chutesApiKey = GetEnvironmentVariableOrThrow(
                "CHUTES_API_KEY",
                "Chutes API key"
            );
            _mistralApiKey = GetEnvironmentVariableOrThrow(
                "MISTRAL_API_KEY",
                "Mistral API key"
            );

            // --- Retrieve URIs from Environment Variables or use defaults ---
            _openRouterUri =
                Environment.GetEnvironmentVariable("OPENROUTER_URI")
                ?? "https://openrouter.ai/api/v1/chat/completions";
            _groqUri =
                Environment.GetEnvironmentVariable("GROQ_URI")
                ?? "https://api.groq.com/openai/v1/chat/completions";
            _aiStudioUri =
                Environment.GetEnvironmentVariable("AISTUDIO_URI")
                ?? "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions";
            _chutesUri =
                Environment.GetEnvironmentVariable("CHUTES_URI")
                ?? "https://llm.chutes.ai/v1/chat/completions";
            _mistralUri =
                Environment.GetEnvironmentVariable("MISTRAL_URI")
                ?? "https://api.mistral.ai/v1/chat/completions";

            _provider = Provider.OpenRouter; // Default provider
            _dateTime = DateTime.Now;
            _systemPrompt =
                $"You are M1 Chat, an AI assistant. Your role is to assist and engage in conversation while being helpful, respectful, and engaging.\n- The current date and time including timezone is {_dateTime}.\n- Always use LaTeX for mathematical expressions:\n    - Inline math must be wrapped in single dollar signs: $ content $ \n    - Display math must be wrapped in double dollar signs: $$ content $$\n-   \n- When generating code:\n    - Ensure it is properly formatted using Prettier with a print width of 80 characters\n    - Present it in Markdown code blocks with the correct language extension indicated";
            _apiKeyService = apiKeyService;
        }

        /// <summary>
        /// Retrieves an environment variable and throws an InvalidOperationException if it's null or empty.
        /// </summary>
        /// <param name="variableName">The name of the environment variable.</param>
        /// <param name="friendlyName">A friendly name for the variable, used in error messages.</param>
        /// <returns>The value of the environment variable.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the environment variable is not found or is empty.</exception>
        private string GetEnvironmentVariableOrThrow(
            string variableName,
            string friendlyName
        )
        {
            var value = Environment.GetEnvironmentVariable(variableName);
            if (string.IsNullOrEmpty(value))
            {
                throw new InvalidOperationException(
                    $"{friendlyName} not found. Please set the '{variableName}' environment variable."
                );
            }
            return value;
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
            processedMessages.Add(
                new ChatMessageDto { Role = "system", Content = _systemPrompt }
            );

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
                case "Gemini 2.0 Flash (AI Studio)":
                    model = "gemini-2.0-flash";
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
                case "Qwen3 30B":
                    model = "qwen/qwen3-30b-a3b:free";
                    _provider = Provider.OpenRouter;
                    break;
                case "Llama 4 Maverick (OpenRouter)":
                    model = "meta-llama/llama-4-maverick:free";
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
                    model = "devstral-small-latest";
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
                case "Magistral Medium":
                    model = "magistral-medium-latest";
                    _provider = Provider.Mistral;
                    break;
                default:
                    model = "google/gemma-3-27b-it:free";
                    _provider = Provider.OpenRouter;
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
                case Provider.Mistral:
                    await foreach (
                        var chunk in StreamMistralAsync(processedMessages, model, userEmail)
                    )
                    {
                        yield return chunk;
                    }
                    break;
                default:
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
            var apiKey =
                await _apiKeyService.GetUserApiKey(userEmail, ProviderOpenRouter)
                ?? _openRouterApiKey;

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);
            _httpClient.DefaultRequestHeaders.Remove("HTTP-Referer");
            _httpClient.DefaultRequestHeaders.Add("HTTP-Referer", "https://chat.mattdev.im");
            _httpClient.DefaultRequestHeaders.Remove("X-Title");
            _httpClient.DefaultRequestHeaders.Add("X-Title", "m1Chat");

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
            var apiKey =
                await _apiKeyService.GetUserApiKey(userEmail, ProviderChutes)
                ?? _chutesApiKey;

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);
            _httpClient.DefaultRequestHeaders.Remove("HTTP-Referer");
            _httpClient.DefaultRequestHeaders.Add("HTTP-Referer", "https://chat.mattdev.im");
            _httpClient.DefaultRequestHeaders.Remove("X-Title");
            _httpClient.DefaultRequestHeaders.Add("X-Title", "m1Chat");

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

            // Get user's API key or use default
            var apiKey =
                await _apiKeyService.GetUserApiKey(userEmail, ProviderGroq) ?? _groqApiKey;

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);
            _httpClient.DefaultRequestHeaders.Remove("HTTP-Referer");
            _httpClient.DefaultRequestHeaders.Add("HTTP-Referer", "https://chat.mattdev.im");
            _httpClient.DefaultRequestHeaders.Remove("X-Title");
            _httpClient.DefaultRequestHeaders.Add("X-Title", "m1Chat");

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
            var apiKey =
                await _apiKeyService.GetUserApiKey(userEmail, ProviderAIStudio)
                ?? _aiStudioApiKey;

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);
            _httpClient.DefaultRequestHeaders.Remove("HTTP-Referer");
            _httpClient.DefaultRequestHeaders.Add("HTTP-Referer", "https://chat.mattdev.im");
            _httpClient.DefaultRequestHeaders.Remove("X-Title");
            _httpClient.DefaultRequestHeaders.Add("X-Title", "m1Chat");

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
            var apiKey =
                await _apiKeyService.GetUserApiKey(userEmail, ProviderMistral)
                ?? _mistralApiKey;

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);
            _httpClient.DefaultRequestHeaders.Remove("HTTP-Referer");
            _httpClient.DefaultRequestHeaders.Add("HTTP-Referer", "https://chat.mattdev.im");
            _httpClient.DefaultRequestHeaders.Remove("X-Title");
            _httpClient.DefaultRequestHeaders.Add("X-Title", "m1Chat");

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

        private string? TryParseContentChunk(string jsonData)
        {
            try
            {
                using var doc = JsonDocument.Parse(jsonData);
                var root = doc.RootElement;
                var choices = root.GetProperty("choices");
                if (choices.GetArrayLength() > 0)
                {
                    var delta = choices[0].GetProperty("delta");
                    if (
                        delta.TryGetProperty("content", out var c) &&
                        c.ValueKind == JsonValueKind.String
                    )
                    {
                        return c.GetString();
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
