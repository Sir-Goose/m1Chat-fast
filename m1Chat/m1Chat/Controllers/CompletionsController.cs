using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using m1Chat.Data;
using m1Chat.Hubs;
using m1Chat.Services;
using Microsoft.AspNetCore.Http; // Included, though not directly used in this specific fix
using Microsoft.Extensions.DependencyInjection; // Required for IServiceScopeFactory

namespace m1Chat.Controllers
{
    public class ChatHistoryRequest
    {
        // Unique identifier for the streaming request
        public Guid RequestId { get; set; }
        public string Model { get; set; } = string.Empty;
        public string ReasoningEffort { get; set; } = "Medium";
        public ChatMessageDto[] Messages { get; set; } = Array.Empty<ChatMessageDto>();
    }

    [ApiController]
    [Route("api/[controller]")]
    public class CompletionsController : ControllerBase
    {
        // We'll keep these for the initial request handling
        private readonly IHubContext<ChatHub> _hubContext;
        // This factory is key for creating new scopes for background tasks
        private readonly IServiceScopeFactory _serviceScopeFactory;

        // Note: Completion and ChatDbContext are no longer directly injected here
        // as they are scoped services that will be resolved within the background task's scope.
        // We only need IHubContext and IServiceScopeFactory here.
        public CompletionsController(
            IHubContext<ChatHub> hubContext,
            IServiceScopeFactory serviceScopeFactory) // <--- Add IServiceScopeFactory
        {
            _hubContext = hubContext;
            _serviceScopeFactory = serviceScopeFactory; // <--- Store it
        }

        [HttpPost("stream")]
        // Changed to IActionResult as we are not awaiting the long-running task here
        public IActionResult Stream(
            [FromBody] ChatHistoryRequest request,
            [FromQuery] string connectionId)
        {
            // Validate the requestId
            if (request.RequestId == Guid.Empty)
            {
                return BadRequest(new { error = "Streaming RequestId is required." });
            }

            // Capture necessary data for the background task
            // Primitive types and DTOs can be safely captured by the closure.
            var dtoList = request.Messages.ToList();
            var requestId = request.RequestId;
            var model = request.Model;
            var reasoningEffort = request.ReasoningEffort;
            var email = User.FindFirst(ClaimTypes.Email)?.Value;

            // Start the long-running operation in a background task.
            // _ = Task.Run allows the current HTTP request to complete immediately.
            _ = Task.Run(async () =>
            {
                // Create a new dependency injection scope for this background task.
                // This is crucial for correctly managing scoped services like DbContext
                // and any services that depend on it (like 'Completion').
                using (var scope = _serviceScopeFactory.CreateScope())
                {
                    // Resolve services from the new scope
                    var scopedCompletion = scope.ServiceProvider.GetRequiredService<Completion>();
                    var scopedDb = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

                    try
                    {
                        // The actual long-running AI generation and SignalR pushing
                        await foreach (var chunk in scopedCompletion.CompleteAsync(
                                           dtoList,
                                           model, // Use captured variables
                                           reasoningEffort, // Use captured variables
                                           scopedDb,
                                           email)) // Use the scoped DbContext
                        {
                            if (!string.IsNullOrEmpty(chunk))
                            {
                                // Pass the requestId to the Hub method
                                await _hubContext.Clients.Client(connectionId)
                                    .SendAsync("ReceiveChunk", requestId, chunk);
                            }
                        }

                        // Signal completion via SignalR
                        await _hubContext.Clients.Client(connectionId)
                            .SendAsync("StreamComplete", requestId);
                    }
                    catch (Exception ex)
                    {
                        // Log the exception for server-side monitoring
                        Console.WriteLine($"Error in background streaming task for RequestId {requestId}: {ex.Message}\n{ex.StackTrace}");
                        // Signal error to the client via SignalR
                        await _hubContext.Clients.Client(connectionId)
                            .SendAsync("StreamError", requestId, ex.Message);
                    }
                } // The 'scope' and its resolved services (like scopedDb) are disposed here.
            });

            // CRITICAL: Immediately return an HTTP 200 OK to the client.
            // This tells Cloudflare (and the client) that the initial request
            // was successfully received and the streaming process has been initiated.
            // The 524 timeout will no longer occur for the initial HTTP request.
            return Ok(new { message = "Streaming process initiated successfully via SignalR." });
        }
    }
}
