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

        private enum Provider
        {
            Chutes,
            OpenRouter,
            AiStudio,
            Groq,
            Mistral
            
        }

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
            _mistralApiKey = "7pENTY2DoHpnna2SGvSIaA0K9rEfBAxA";
            _openRouterUri = "https://openrouter.ai/api/v1/chat/completions";
            _groqUri = "https://api.groq.com/openai/v1/chat/completions";
            _aiStudioUri =
                "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions";
            _chutesUri = "https://llm.chutes.ai/v1/chat/completions";
            _mistralUri = "https://api.mistral.ai/v1/chat/completions";
            _provider = Provider.OpenRouter; // Default provider
            _dateTime = DateTime.Now;
            _systemPrompt =
                $"You are M1 Chat, an AI assistant. Your role is to assist and engage in conversation while being helpful, respectful, and engaging.\n- The current date and time including timezone is {_dateTime}.\n- Always use LaTeX for mathematical expressions:\n    - Inline math must be wrapped in escaped parentheses: \\( content \\)\n    - Do not use single dollar signs for inline math\n    - Display math must be wrapped in double dollar signs: $$ content $$\n- Do not use the backslash character to escape parenthesis. Use the actual parentheses instead.\n- When generating code:\n    - Ensure it is properly formatted using Prettier with a print width of 80 characters\n    - Present it in Markdown code blocks with the correct language extension indicated";

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
            ChatDbContext db
        )
        {
            // Process messages and include file content if database context is provided
            var processedMessages = new List<ChatMessageDto>();
            // Add system prompt if provided
            processedMessages.Add(new ChatMessageDto 
            { 
                Role = "system", 
                Content = _systemPrompt 
            });

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

                    if (fileContents.Count != 0)
                    {
                        content = string.Join("\n", fileContents) + "\n" + content;
                    }
                }
                processedMessages.Add(new ChatMessageDto { Role = message.Role, Content = content });
            }

            reasoningEffort = reasoningEffort.ToLower();
            switch (model)
            {
                case "DeepSeek v3":
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
                case "Deepseek R1 0528":
                    model = "deepseek-ai/DeepSeek-R1-0528";
                    _provider = Provider.Chutes;
                    break;
                case "Gemini 2.0 Flash":
                    model = "gemini-2.0-flash";
                    _provider = Provider.AiStudio;
                    break;
                case "Gemini 2.5 Flash":
                    model = "gemini-2.5-flash-preview-05-20";
                    _provider = Provider.AiStudio;
                    break;
                case "Qwen3 235B":
                    model = "qwen/qwen3-235b-a22b:free";
                    _provider = Provider.OpenRouter;
                    break;
                case "DeepSeek r1 v3 Chimera":
                    model = "tngtech/deepseek-r1t-chimera:free";
                    _provider = Provider.OpenRouter;
                    break;
                case "Gemma 3 27B":
                    model = "google/gemma-3-27b-it:free";
                    _provider = Provider.OpenRouter; 
                    break;
                case "Qwen3 30B":
                    model = "qwen/qwen3-30b-a3b:free";
                    _provider = Provider.OpenRouter;
                    break;
                case "llama-3.1-8b-instant":
                    model = "llama-3.1-8b-instant";
                    _provider = Provider.Groq;
                    break;
                case "Devstral Small":
                    model = "devstral-small-latest";
                    _provider = Provider.Mistral;
                    break;
                case "Magistral Small":
                    model = "magistral-small-latest";
                    _provider = Provider.Mistral;
                    break;
                case "Mistral Medium":
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
                            reasoningEffort
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
                            reasoningEffort
                        )
                    )
                    {
                        yield return chunk;
                    }

                    break;
                case Provider.Groq:
                    await foreach (
                        var chunk in StreamGroqAsync(processedMessages, model)
                    )
                    {
                        yield return chunk;
                    }

                    break;
                case Provider.Chutes:
                    await foreach (
                        var chunk in StreamChutesAsync(processedMessages, model)
                    )
                    {
                        yield return chunk;
                    }
                    
                    break;
                    
                case Provider.Mistral:
                    await foreach (
                        var chunk in StreamMistralAsync(processedMessages, model)
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
                new AuthenticationHeaderValue("Bearer", _openRouterApiKey);
            _httpClient.DefaultRequestHeaders.Remove("HTTP-Referer");
            _httpClient.DefaultRequestHeaders.Add(
                "HTTP-Referer",
                "https://chat.mattdev.im"
            );
            _httpClient.DefaultRequestHeaders.Remove("X-Title");
            _httpClient.DefaultRequestHeaders.Add("X-Title", "m1Chat");

            using var response = await _httpClient.SendAsync(
                new HttpRequestMessage(HttpMethod.Post, _openRouterUri)
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
                new AuthenticationHeaderValue("Bearer", _chutesApiKey);
            _httpClient.DefaultRequestHeaders.Remove("HTTP-Referer");
            _httpClient.DefaultRequestHeaders.Add(
                "HTTP-Referer",
                "https://chat.mattdev.im"
            );
            _httpClient.DefaultRequestHeaders.Remove("X-Title");
            _httpClient.DefaultRequestHeaders.Add("X-Title", "m1Chat");

            using var response = await _httpClient.SendAsync(
                new HttpRequestMessage(HttpMethod.Post, _chutesUri)
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
                    $"Chutes API error: {response.StatusCode} - {error}"
                );
            }

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);

            var inReasoningBlock = false;

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line)) continue;

                if (!line.StartsWith("data: ")) continue;
                var jsonData = line["data: ".Length..].Trim();
                if (jsonData == "[DONE]") break;
                //Console.WriteLine(jsonData);
                var contentChunk = TryParseContentChunk(jsonData);
                if (string.IsNullOrEmpty(contentChunk)) continue;
                switch (contentChunk)
                {
                    case "<think>":
                        yield return "```Thinking ";
                        inReasoningBlock = true;
                        break;
                    case "</think>":
                        yield return "```\n" +
                                     "\n";
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
                new AuthenticationHeaderValue("Bearer", _groqApiKey);
            _httpClient.DefaultRequestHeaders.Remove("HTTP-Referer");
            _httpClient.DefaultRequestHeaders.Add(
                "HTTP-Referer",
                "https://chat.mattdev.im"
            );
            _httpClient.DefaultRequestHeaders.Remove("X-Title");
            _httpClient.DefaultRequestHeaders.Add("X-Title", "m1Chat");

            using var response = await _httpClient.SendAsync(
                new HttpRequestMessage(HttpMethod.Post, _groqUri)
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
                new AuthenticationHeaderValue("Bearer", _aiStudioApiKey);
            _httpClient.DefaultRequestHeaders.Remove("HTTP-Referer");
            _httpClient.DefaultRequestHeaders.Add(
                "HTTP-Referer",
                "https://chat.mattdev.im"
            );
            _httpClient.DefaultRequestHeaders.Remove("X-Title");
            _httpClient.DefaultRequestHeaders.Add("X-Title", "m1Chat");

            using var response = await _httpClient.SendAsync(
                new HttpRequestMessage(HttpMethod.Post, _aiStudioUri)
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
        
        private async IAsyncEnumerable<string> StreamMistralAsync(
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
                new AuthenticationHeaderValue("Bearer", _mistralApiKey);
            _httpClient.DefaultRequestHeaders.Remove("HTTP-Referer");
            _httpClient.DefaultRequestHeaders.Add(
                "HTTP-Referer",
                "https://chat.mattdev.im"
            );
            _httpClient.DefaultRequestHeaders.Remove("X-Title");
            _httpClient.DefaultRequestHeaders.Add("X-Title", "m1Chat");

            using var response = await _httpClient.SendAsync(
                new HttpRequestMessage(HttpMethod.Post, _mistralUri)
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
                    $"Mistral API error: {response.StatusCode} - {error}"
                );
            }

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);

            var inReasoningBlock = false;

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line)) continue;

                if (!line.StartsWith("data: ")) continue;
                var jsonData = line["data: ".Length..].Trim();
                if (jsonData == "[DONE]") break;
                //Console.WriteLine(jsonData);
                var contentChunk = TryParseContentChunk(jsonData);
                if (string.IsNullOrEmpty(contentChunk)) continue;
                switch (contentChunk)
                {
                    case "<think>":
                        yield return "```Thinking ";
                        inReasoningBlock = true;
                        break;
                    case "</think>":
                        yield return "```\n" +
                                     "\n";
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