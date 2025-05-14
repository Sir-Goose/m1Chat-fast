using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.IO;

namespace m1Chat.Services
{
    public class Completion
    {
        private readonly HttpClient _httpClient;
        private readonly string _openRouterApiKey;

        public Completion()
        {
            _httpClient = new HttpClient();
            _openRouterApiKey = "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";
            
            if (string.IsNullOrEmpty(_openRouterApiKey))
            {
                throw new InvalidOperationException(
                    "OpenRouter API key not found in environment variables."
                );
            }
        }

        public async IAsyncEnumerable<string> CompleteAsync(List<ChatMessage> messages)
        {
            await foreach (var chunk in StreamOpenRouterAsync(messages))
            {
                yield return chunk;
            }
        }

        private async IAsyncEnumerable<string> StreamOpenRouterAsync(List<ChatMessage> messages)
        {
            var requestBody = new
            {
                model = "google/gemini-2.0-flash-exp:free",
                messages = messages.Select(m => new { role = m.Role, content = m.Content }),
                stream = true
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _openRouterApiKey);

            if (_httpClient.DefaultRequestHeaders.Contains("HTTP-Referer"))
                _httpClient.DefaultRequestHeaders.Remove("HTTP-Referer");
            if (_httpClient.DefaultRequestHeaders.Contains("X-Title"))
                _httpClient.DefaultRequestHeaders.Remove("X-Title");

            _httpClient.DefaultRequestHeaders.Add("HTTP-Referer", "https://yourdomain.com");
            _httpClient.DefaultRequestHeaders.Add("X-Title", "m1Chat");

            var request = new HttpRequestMessage(
                HttpMethod.Post,
                "https://openrouter.ai/api/v1/chat/completions"
            )
            {
                Content = content
            };

            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead
            );

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception(
                    $"OpenRouter API error: {response.StatusCode} - {error}"
                );
            }

            using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                if (line.StartsWith("data: "))
                {
                    var jsonData = line.Substring("data: ".Length).Trim();

                    if (jsonData == "[DONE]")
                        break;

                    string? chunk = TryParseContentChunk(jsonData);
                    if (!string.IsNullOrEmpty(chunk))
                    {
                        yield return chunk;
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
                    var choice = choices[0];
                    if (choice.TryGetProperty("delta", out var delta) &&
                        delta.TryGetProperty("content", out var contentProp) &&
                        contentProp.ValueKind == JsonValueKind.String)
                    {
                        return contentProp.GetString();
                    }
                }
            }
            catch
            {
                // Ignore malformed chunks
            }
            return null;
        }
    }
}
