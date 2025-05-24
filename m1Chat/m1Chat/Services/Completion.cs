// File: m1Chat.Services/Completion.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace m1Chat.Services
{
    // Reuse the same DTO as your controller
    public class ChatMessageDto
    {
        public string Role { get; set; }
        public string Content { get; set; }
    }

    public class Completion
    {
        private readonly HttpClient _httpClient;
        private readonly string _openRouterApiKey;
        private readonly string _groqApiKey;
        private readonly string _aiStudioApiKey;
        private readonly string _openRouterURI;
        private readonly string _groqURI;
        private readonly string _aiStudioURI;
        private string _activeApiKey;
        private string _activeURI;
        private string _provider;

        public Completion()
        {
            _httpClient = new HttpClient();
            _openRouterApiKey =
                "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";
            _groqApiKey =
                "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";
            _aiStudioApiKey = "AIzaSyDpr4nFieUgQ08NlnQOGyMQ4CYHnEm-7hw";
            _openRouterURI = "https://openrouter.ai/api/v1/chat/completions";
            _groqURI = "https://api.groq.com/openai/v1/chat/completions";
            _aiStudioURI =
                "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions";
            _provider = "openrouter"; // Default provider

            if (string.IsNullOrEmpty(_openRouterApiKey))
            {
                throw new InvalidOperationException(
                    "OpenRouter API key not found."
                );
            }
        }

        public async IAsyncEnumerable<string> CompleteAsync(
            List<ChatMessageDto> messages,
            string model,
            string reasoningEffort // This parameter was already here
        )
        {
            reasoningEffort = reasoningEffort.ToLower();
            switch (model)
            {
                case "DeepSeek v3":
                    model = "deepseek/deepseek-chat-v3-0324:free";
                    _activeApiKey = _openRouterApiKey;
                    _activeURI = _openRouterURI;
                    _provider = "openrouter";
                    break;
                case "DeepSeek Prover v2":
                    model = "deepseek/deepseek-prover-v2:free";
                    _activeApiKey = _openRouterApiKey;
                    _activeURI = _openRouterURI;
                    _provider = "openrouter";
                    break;
                case "Gemini 2.5 Pro":
                    model = "google/gemini-2.5-pro-exp-03-25";
                    _activeApiKey = _openRouterApiKey;
                    _activeURI = _openRouterURI;
                    _provider = "openrouter";
                    break;
                case "Deepseek r1":
                    model = "deepseek/deepseek-r1:free";
                    _activeApiKey = _openRouterApiKey;
                    _activeURI = _openRouterURI;
                    _provider = "openrouter";
                    break;
                case "Gemini 2.0 Flash":
                    model = "gemini-2.0-flash";
                    _activeApiKey = _aiStudioApiKey;
                    _activeURI = _aiStudioURI;
                    _provider = "aistudio"; // Corrected provider
                    break;
                case "Gemini 2.5 Flash":
                    model = "gemini-2.5-flash-preview-05-20";
                    _activeApiKey = _aiStudioApiKey;
                    _activeURI = _aiStudioURI;
                    _provider = "aistudio"; // Corrected provider
                    break;
                case "Qwen3 235B":
                    model = "qwen/qwen3-235b-a22b:free";
                    _activeApiKey = _openRouterApiKey;
                    _activeURI = _openRouterURI;
                    _provider = "openrouter";
                    break;
                case "DeepSeek r1 v3 Chimera":
                    model = "tngtech/deepseek-r1t-chimera:free";
                    _activeApiKey = _openRouterApiKey;
                    _activeURI = _openRouterURI;
                    _provider = "openrouter";
                    break;
                case "Gemma 3 27B":
                    model = "google/gemma-3-27b-it:free";
                    _activeApiKey = _openRouterApiKey;
                    _activeURI = _openRouterURI;
                    _provider = "openrouter";
                    break;
                case "Qwen3 30B":
                    model = "qwen/qwen3-30b-a3b:free";
                    _activeApiKey = _openRouterApiKey;
                    _activeURI = _openRouterURI;
                    _provider = "openrouter";
                    break;
                case "llama-3.1-8b-instant":
                    model = "llama-3.1-8b-instant";
                    _activeApiKey = _groqApiKey;
                    _activeURI = _groqURI;
                    _provider = "groq"; // Corrected provider
                    break;
                default:
                    model = "google/gemma-3-27b-it:free"; // Default model
                    _activeApiKey = _openRouterApiKey;
                    _activeURI = _openRouterURI;
                    _provider = "openrouter";
                    break;
            }

            switch (_provider)
            {
                case "openrouter":
                    await foreach (
                        var chunk in StreamOpenRouterAsync(
                            messages,
                            model,
                            reasoningEffort
                        )
                    )
                    {
                        yield return chunk;
                    }
                    break;
                case "aistudio":
                    await foreach (
                        var chunk in StreamAiStudioAsync(
                            messages,
                            model,
                            reasoningEffort
                        )
                    )
                    {
                        yield return chunk;
                    }
                    break;
                case "groq":
                    await foreach (
                        var chunk in StreamGroqAsync(messages, model)
                    ) // Groq might not use reasoningEffort
                    {
                        yield return chunk;
                    }
                    break;
                default: // Fallback to openrouter if provider is unknown
                    await foreach (
                        var chunk in StreamOpenRouterAsync(
                            messages,
                            model,
                            reasoningEffort
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
            string reasoningEffort
        )
        {
            var requestBody = new
            {
                model,
                reasoningEffort, // Included
                messages = messages.Select(
                    m => new { role = m.Role, content = m.Content }
                ),
                stream = true
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _activeApiKey);
            _httpClient.DefaultRequestHeaders.Remove("HTTP-Referer");
            _httpClient.DefaultRequestHeaders.Add(
                "HTTP-Referer",
                "https://chat.mattdev.im"
            );
            _httpClient.DefaultRequestHeaders.Remove("X-Title");
            _httpClient.DefaultRequestHeaders.Add("X-Title", "m1Chat");

            using var response = await _httpClient.SendAsync(
                new HttpRequestMessage(HttpMethod.Post, _activeURI)
                {
                    Content = content
                },
                HttpCompletionOption.ResponseHeadersRead
            );

            Console.WriteLine(
                $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}"
            );

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

        private async IAsyncEnumerable<string> StreamGroqAsync(
            List<ChatMessageDto> messages,
            string model
        )
        {
            var requestBody = new
            {
                model,
                // reasoningEffort is not included here as Groq might not support it
                messages = messages.Select(
                    m => new { role = m.Role, content = m.Content }
                ),
                stream = true
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _activeApiKey);
            _httpClient.DefaultRequestHeaders.Remove("HTTP-Referer");
            _httpClient.DefaultRequestHeaders.Add(
                "HTTP-Referer",
                "https://chat.mattdev.im"
            );
            _httpClient.DefaultRequestHeaders.Remove("X-Title");
            _httpClient.DefaultRequestHeaders.Add("X-Title", "m1Chat");

            using var response = await _httpClient.SendAsync(
                new HttpRequestMessage(HttpMethod.Post, _activeURI)
                {
                    Content = content
                },
                HttpCompletionOption.ResponseHeadersRead
            );

            Console.WriteLine(
                $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}"
            );

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Error response content: {error}");
                throw new Exception(
                    $"Groq API error: {response.StatusCode} - {error}"
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

        private async IAsyncEnumerable<string> StreamAiStudioAsync(
            List<ChatMessageDto> messages,
            string model,
            string reasoningEffort
        )
        {
            var requestBody = new
            {
                model,
                reasoningEffort, // Included
                messages = messages.Select(
                    m => new { role = m.Role, content = m.Content }
                ),
                stream = true
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _activeApiKey);
            _httpClient.DefaultRequestHeaders.Remove("HTTP-Referer");
            _httpClient.DefaultRequestHeaders.Add(
                "HTTP-Referer",
                "https://chat.mattdev.im"
            );
            _httpClient.DefaultRequestHeaders.Remove("X-Title");
            _httpClient.DefaultRequestHeaders.Add("X-Title", "m1Chat");

            using var response = await _httpClient.SendAsync(
                new HttpRequestMessage(HttpMethod.Post, _activeURI)
                {
                    Content = content
                },
                HttpCompletionOption.ResponseHeadersRead
            );

            Console.WriteLine(
                $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}"
            );

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
                Console.WriteLine($"Error parsing JSON chunk: {ex.Message}. Data: {jsonData}");
                // ignore and continue
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Generic error parsing chunk: {ex.Message}. Data: {jsonData}");
                // ignore and continue
            }
            return null;
        }
    }
}