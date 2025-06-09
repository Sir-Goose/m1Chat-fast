using System; 
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace m1Chat.Client.Services
{
    public class ChatMessage
    {
        public required string Role { get; set; }
        public required string Content { get; set; }
        public List<Guid>? FileIds { get; set; }
    }

    public class ChatCompletionService
    {
        private readonly SignalRService _signalRService;
        private readonly HttpClient _http;

        public ChatCompletionService(
            HttpClient http,
            SignalRService signalRService)
        {
            _http = http;
            _signalRService = signalRService;
        }

        public async Task StreamCompletion(
            List<ChatMessage> messages,
            string model,
            string reasoningEffort,
            Action<string> onChunk,
            Action onComplete,
            Action<string> onError)
        {
            await _signalRService.InitializeAsync();
            var connectionId = _signalRService.GetConnectionId();

            if (string.IsNullOrEmpty(connectionId))
            {
                onError?.Invoke("SignalR connection not established.");
                return;
            }

            var payload = new
            {
                model,
                reasoningEffort,
                messages = messages
                    .Select(m => new
                    {
                        role = m.Role,
                        content = m.Content,
                        fileIds = m.FileIds
                    })
                    .ToArray()
            };

            // TaskCompletionSource to signal when this specific stream is complete
            var tcs = new TaskCompletionSource<bool>();

            // Declare handlers for this specific stream request
            void ChunkHandler(string chunk) => onChunk?.Invoke(chunk); // Null check for onChunk
            void CompleteHandler()
            {
                onComplete?.Invoke(); // Null check for onComplete
                UnregisterHandlers();
                tcs.TrySetResult(true); // Signal completion of this stream
            }

            void ErrorHandler(string error)
            {
                onError?.Invoke(error); // Null check for onError
                UnregisterHandlers();
                tcs.TrySetResult(false); // Signal error and completion of this stream
            }

            // Register handlers for this session
            void RegisterHandlers()
            {
                _signalRService.OnChunkReceived += ChunkHandler;
                _signalRService.OnStreamCompleted += CompleteHandler;
                _signalRService.OnStreamError += ErrorHandler;
            }

            // Unregister handlers
            void UnregisterHandlers()
            {
                _signalRService.OnChunkReceived -= ChunkHandler;
                _signalRService.OnStreamCompleted -= CompleteHandler;
                _signalRService.OnStreamError -= ErrorHandler;
            }

            // Register handlers before initiating the HTTP request
            RegisterHandlers();

            try
            {
                // Send the HTTP POST request to initiate the stream on the server
                var response = await _http.PostAsJsonAsync(
                    $"/api/completions/stream?connectionId={connectionId}",
                    payload);

                response.EnsureSuccessStatusCode();

                // Await the SignalR stream completion via the TaskCompletionSource
                await tcs.Task;
            }
            catch (Exception ex)
            {
                // If an HTTP error occurs before SignalR messages even start
                onError?.Invoke($"Failed to initiate streaming: {ex.Message}");
                // Ensure TCS is set if an exception occurs before stream completion is signaled
                tcs.TrySetResult(false);
                // Also ensure handlers are unregistered in case of an early HTTP error
                UnregisterHandlers();
            }
        }
    }
}
