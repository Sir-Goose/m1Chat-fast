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
        private readonly string _openRouterURI;
        private readonly string _groqURI;
        private string _activeApiKey;
        private string _activeURI;

        public Completion()
        {
            _httpClient = new HttpClient();
            _openRouterApiKey = "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";
            _groqApiKey = "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";
            _openRouterURI = "https://openrouter.ai/api/v1/chat/completions";
            _groqURI = "https://api.groq.com/openai/v1/chat/completions";

            if (string.IsNullOrEmpty(_openRouterApiKey))
            {
                throw new InvalidOperationException(
                    "OpenRouter API key not found."
                );
            }
        }

        // now accepts a model
        public async IAsyncEnumerable<string> CompleteAsync(
                List<ChatMessageDto> messages,
                string model
            )
            // convert human name from selector into names used by providers
        {
            switch (model)
            {
                case "DeepSeek v3":
                    model = "deepseek/deepseek-chat-v3-0324:free";
                    _activeApiKey = _openRouterApiKey;
                    _activeURI = _openRouterURI;
                    break;
                case "DeepSeek Prover v2":
                    model = "deepseek/deepseek-prover-v2:free";
                    _activeApiKey = _openRouterApiKey;
                    _activeURI = _openRouterURI;
                    break;
                case "Gemini 2.5 Pro":
                    model = "google/gemini-2.5-pro-exp-03-25";
                    _activeApiKey = _openRouterApiKey;
                    _activeURI = _openRouterURI;
                    break;
                case "Deepseek r1":
                    model = "deepseek/deepseek-r1:free";
                    _activeApiKey = _openRouterApiKey;
                    _activeURI = _openRouterURI;
                    break;
                case "Gemini 2.0 Flash":
                    model = "google/gemini-2.0-flash-exp:free";
                    _activeApiKey = _openRouterApiKey;
                    _activeURI = _openRouterURI;
                    break;
                case "Qwen3 235B":
                    model = "qwen/qwen3-235b-a22b:free";
                    _activeApiKey = _openRouterApiKey;
                    _activeURI = _openRouterURI;
                    break;
                case "DeepSeek r1 v3 Chimera":
                    model = "tngtech/deepseek-r1t-chimera:free";
                    _activeApiKey = _openRouterApiKey;
                    _activeURI = _openRouterURI;
                    break;
                case "Gemma 3 27B":
                    model = "google/gemma-3-27b-it:free";
                    _activeApiKey = _openRouterApiKey;
                    _activeURI = _openRouterURI;
                    break;
                case "Qwen3 30B":
                    model = "qwen/qwen3-30b-a3b:free";
                    _activeApiKey = _openRouterApiKey;
                    _activeURI = _openRouterURI;
                    break;
                case "llama-3.1-8b-instant":
                    model = "llama-3.1-8b-instant";
                    _activeApiKey = _groqApiKey;
                    _activeURI = _groqURI;
                    break;
                default:
                    model = "google/gemma-3-27b-it:free";
                    _activeApiKey = _openRouterApiKey;
                    _activeURI = _openRouterURI;
                    break;
            }


            await foreach (var chunk in StreamOpenRouterAsync(messages, model))
            {
                yield return chunk;
            }
        }

        private async IAsyncEnumerable<string> StreamOpenRouterAsync(
            List<ChatMessageDto> messages,
            string model
        )
        {
            var requestBody = new
            {
                model,
                messages = messages
                    .Select(m => new { role = m.Role, content = m.Content }),
                stream = true
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _activeApiKey);

            // any other headers you need...
            _httpClient.DefaultRequestHeaders.Remove("HTTP-Referer");
            _httpClient.DefaultRequestHeaders.Add(
                "HTTP-Referer",
                "https://chat.mattdev.im"
            );
            _httpClient.DefaultRequestHeaders.Remove("X-Title");
            _httpClient.DefaultRequestHeaders.Add("X-Title", "m1Chat");

            using var response = await _httpClient.SendAsync(
                new HttpRequestMessage(
                    HttpMethod.Post,
                    _activeURI
                )
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
                Console.WriteLine($"Error response content: {error}"); // Added logging
                throw new Exception(
                    $"OpenRouter API error: {response.StatusCode} - {error}"
                );
            }

            // Log successful response content
            var responseContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Response content: {responseContent}");

            // Reset the stream for reading
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(responseContent));
            using var reader = new StreamReader(stream);

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line)) continue;

                if (line.StartsWith("data: "))
                {
                    var jsonData = line["data: ".Length..].Trim();
                    if (jsonData == "[DONE]") break;
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
                    if (delta.TryGetProperty("content", out var c) &&
                        c.ValueKind == JsonValueKind.String)
                    {
                        return c.GetString();
                    }
                }
            }
            catch
            {
                // ignore
            }

            return null;
        }
    }
}