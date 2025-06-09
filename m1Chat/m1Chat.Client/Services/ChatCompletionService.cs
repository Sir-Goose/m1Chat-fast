using System;
using System.Collections.Concurrent; // Added for ConcurrentDictionary
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

        // Concurrent Dictionaries to store request-specific callbacks and TaskCompletionSources
        // This allows each stream to manage its own state independently
        private readonly ConcurrentDictionary<Guid, Action<string>> _onChunkCallbacks =
            new();
        private readonly ConcurrentDictionary<Guid, Func<Task>> _onCompleteCallbacks =
            new();
        private readonly ConcurrentDictionary<Guid, Action<string>> _onErrorCallbacks =
            new();
        private readonly ConcurrentDictionary<Guid, TaskCompletionSource<bool>>
            _completionSources = new();

        public ChatCompletionService(HttpClient http, SignalRService signalRService)
        {
            _http = http;
            _signalRService = signalRService;

            // Subscribe to SignalRService events once for the service instance.
            // These global handlers will dispatch messages to the correct stream using the requestId.
            _signalRService.OnChunkReceived += HandleChunkReceived;
            _signalRService.OnStreamCompleted += HandleStreamCompleted;
            _signalRService.OnStreamError += HandleStreamError;
        }

        // Global event handler for chunks received from SignalR
        private void HandleChunkReceived(Guid requestId, string chunk)
        {
            // Only invoke the callback associated with the matching requestId
            if (_onChunkCallbacks.TryGetValue(requestId, out var callback))
            {
                callback?.Invoke(chunk);
            }
        }

        // Global event handler for stream completion received from SignalR
        private async Task HandleStreamCompleted(Guid requestId)
        {
            // Only invoke the callback associated with the matching requestId
            if (_onCompleteCallbacks.TryGetValue(requestId, out var callback))
            {
                if (callback != null)
                {
                    await callback(); // Await the completion callback
                }
            }
            // Clean up resources and signal completion for this specific stream
            CleanupStreamResources(requestId, true);
        }

        // Global event handler for stream errors received from SignalR
        private void HandleStreamError(Guid requestId, string error)
        {
            // Only invoke the callback associated with the matching requestId
            if (_onErrorCallbacks.TryGetValue(requestId, out var callback))
            {
                callback?.Invoke(error);
            }
            // Clean up resources and signal error for this specific stream
            CleanupStreamResources(requestId, false);
        }

        // Helper method to remove all resources associated with a stream once it's done
        private void CleanupStreamResources(Guid requestId, bool success)
        {
            _onChunkCallbacks.TryRemove(requestId, out _);
            _onCompleteCallbacks.TryRemove(requestId, out _);
            _onErrorCallbacks.TryRemove(requestId, out _);

            if (_completionSources.TryRemove(requestId, out var tcs))
            {
                tcs.TrySetResult(success);
            }
        }

        public async Task StreamCompletion(
            List<ChatMessage> messages,
            string model,
            string reasoningEffort,
            Action<string> onChunk,
            Func<Task> onComplete,
            Action<string> onError)
        {
            // Generate a unique request ID for this specific stream initiation
            var requestId = Guid.NewGuid();

            // Store the provided callbacks and TaskCompletionSource in our dictionaries
            // associated with this requestId.
            _onChunkCallbacks[requestId] = onChunk;
            _onCompleteCallbacks[requestId] = onComplete;
            _onErrorCallbacks[requestId] = onError;
            var tcs = new TaskCompletionSource<bool>();
            _completionSources[requestId] = tcs;

            try
            {
                await _signalRService.InitializeAsync();
                var connectionId = _signalRService.GetConnectionId();

                if (string.IsNullOrEmpty(connectionId))
                {
                    onError?.Invoke("SignalR connection not established.");
                    // Ensure resources are cleaned up if connection is not ready
                    CleanupStreamResources(requestId, false);
                    return;
                }

                var payload = new
                {
                    // Include the unique requestId in the payload sent to the server.
                    // The server needs to pick this up and use it in SignalR messages.
                    requestId,
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

                // Send the HTTP POST request to initiate the stream on the server
                var response = await _http.PostAsJsonAsync(
                    $"/api/completions/stream?connectionId={connectionId}",
                    payload);

                response.EnsureSuccessStatusCode();

                // Await the SignalR stream completion via the TaskCompletionSource
                // This task will complete only when CleanupStreamResources is called for this requestId.
                _ = tcs.Task;
            }
            catch (Exception ex)
            {
                // If an HTTP error occurs before SignalR messages even start
                onError?.Invoke($"Failed to initiate streaming: {ex.Message}");
                // Ensure TCS is set and resources are cleaned up for this request,
                // regardless of whether SignalR events were ever fired.
                CleanupStreamResources(requestId, false);
            }
        }
    }
}
