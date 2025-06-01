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
        private readonly string _openRouterURI;
        private readonly string _groqURI;
        private readonly string _aiStudioURI;
        private readonly string _chutesUri;
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
            _chutesApiKey =
                "cpk_d1c9605d36354f7697c33b118005c996.ec62a91ca96e58f8a6d6ddc854bfd71b.DG1hrwmJ1Q2BekstSUJruw2v7wMAWAhm";
            _openRouterURI = "https://openrouter.ai/api/v1/chat/completions";
            _groqURI = "https://api.groq.com/openai/v1/chat/completions";
            _aiStudioURI =
                "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions";
            _chutesUri = "https://llm.chutes.ai/v1/chat/completions";
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
            string reasoningEffort,
            ChatDbContext db = null
        )
        {
            // Process messages and include file content if database context is provided
            var processedMessages = new List<ChatMessageDto>();

            if (db != null)
            {
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
                                    var fileContent = await File.ReadAllTextAsync(file.FilePath);
                                    fileContents.Add(
                                        $"--- File: {file.OriginalFileName} ---\n{fileContent}\n--- End of {file.OriginalFileName} ---\n");
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error reading file {fileId}: {ex.Message}");
                            }
                        }

                        if (fileContents.Any())
                        {
                            content = string.Join("\n", fileContents) + "\n" + content;
                        }
                    }

                    processedMessages.Add(new ChatMessageDto { Role = message.Role, Content = content });
                }
            }
            else
            {
                // No database context, use messages as-is but strip file IDs
                processedMessages = messages.Select(m => new ChatMessageDto
                {
                    Role = m.Role,
                    Content = m.Content
                }).ToList();
            }

            reasoningEffort = reasoningEffort.ToLower();
            switch (model)
            {
                case "DeepSeek v3":
                    model = "deepseek-ai/DeepSeek-V3-0324";
                    _activeApiKey = _chutesApiKey;
                    _activeURI = _chutesUri;
                    _provider = "chutes";
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
                case "Deepseek R1 0528":
                    model = "deepseek-ai/DeepSeek-R1-0528";
                    _activeApiKey = _chutesApiKey;
                    _activeURI = _chutesUri;
                    _provider = "chutes";
                    break;
                case "Gemini 2.0 Flash":
                    model = "gemini-2.0-flash";
                    _activeApiKey = _aiStudioApiKey;
                    _activeURI = _aiStudioURI;
                    _provider = "aistudio";
                    break;
                case "Gemini 2.5 Flash":
                    model = "gemini-2.5-flash-preview-05-20";
                    _activeApiKey = _aiStudioApiKey;
                    _activeURI = _aiStudioURI;
                    _provider = "aistudio";
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
                    _provider = "groq";
                    break;
                case "Devstral Small":
                    model = "mistralai/devstral-small:free";
                    _activeApiKey = _openRouterApiKey;
                    _activeURI = _openRouterURI;
                    _provider = "openrouter";
                    break;
                default:
                    model = "google/gemma-3-27b-it:free";
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
                            processedMessages,
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
                            processedMessages,
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
                        var chunk in StreamGroqAsync(processedMessages, model)
                    )
                    {
                        yield return chunk;
                    }

                    break;
                case "chutes":
                    await foreach (
                        var chunk in StreamChutesAsync(processedMessages, model)
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
                reasoningEffort,
                messages = messages.Select(m => new { role = m.Role, content = m.Content }
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

            bool inReasoningBlock = false;

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line)) continue;

                if (line.StartsWith("data: "))
                {
                    var jsonData = line["data: ".Length..].Trim();
                    if (jsonData == "[DONE]") break;

                    var (contentChunk, reasoning) = TryParseContentChunkOpenrouter(jsonData);
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
            string model
        )
        {
            var requestBody = new
            {
                model,
                messages = messages.Select(m => new { role = m.Role, content = m.Content }
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

            bool inReasoningBlock = false;

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line)) continue;

                if (line.StartsWith("data: "))
                {
                    var jsonData = line["data: ".Length..].Trim();
                    if (jsonData == "[DONE]") break;
                    //Console.WriteLine(jsonData);
                    var contentChunk = TryParseContentChunk(jsonData);
                    if (!string.IsNullOrEmpty(contentChunk))
                    {
                        if (contentChunk == "<think>")
                        {
                            yield return "```Thinking\n ";
                            inReasoningBlock = true;
                        }
                        else if (contentChunk == "</think>")
                        {
                            yield return "```\n" +
                                         "\n";
                            inReasoningBlock = false;
                        }
                        else
                        {
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
                messages = messages.Select(m => new { role = m.Role, content = m.Content }
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
                reasoningEffort,
                messages = messages.Select(m => new { role = m.Role, content = m.Content }
                ),
                stream = true,
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
                    //Console.WriteLine($"Raw chunk: {jsonData}");
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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Generic error parsing chunk: {ex.Message}. Data: {jsonData}");
            }

            return null;
        }

        private (string? content, string? reasoning) TryParseContentChunkOpenrouter(string jsonData)
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
                    if (delta.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String)
                    {
                        content = c.GetString();
                    }

                    // Extract reasoning if present
                    if (delta.TryGetProperty("reasoning", out var r) && r.ValueKind == JsonValueKind.String)
                    {
                        reasoning = r.GetString();
                    }

                    return (content, reasoning);
                }
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"Error parsing JSON chunk: {ex.Message}. Data: {jsonData}");
            }

            return (null, null);
        }
    }
}