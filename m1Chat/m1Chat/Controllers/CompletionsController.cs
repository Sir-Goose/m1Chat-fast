using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using m1Chat.Data;
using m1Chat.Hubs;
using m1Chat.Services;
using Microsoft.AspNetCore.Http;

namespace m1Chat.Controllers
{
    public class ChatHistoryRequest
    {
        public string Model { get; set; } = string.Empty;
        public string ReasoningEffort { get; set; } = "Medium";
        public ChatMessageDto[] Messages { get; set; } = Array.Empty<ChatMessageDto>();
    }

    [ApiController]
    [Route("api/[controller]")]
    public class CompletionsController : ControllerBase
    {
        private readonly Completion _completion;
        private readonly ChatDbContext _db;
        private readonly IHubContext<ChatHub> _hubContext;

        public CompletionsController(
            Completion completion,
            ChatDbContext db,
            IHubContext<ChatHub> hubContext)
        {
            _completion = completion;
            _db = db;
            _hubContext = hubContext;
        }

        [HttpPost("stream")]
        public IActionResult Stream( // Changed to IActionResult and removed async from signature
            [FromBody] ChatHistoryRequest request,
            [FromQuery] string connectionId)
        {
            if (string.IsNullOrEmpty(connectionId))
            {
                return BadRequest("ConnectionId is required.");
            }

            // Start the AI completion process in a background task
            // Fire-and-forget, the HTTP request returns immediately.
            _ = Task.Run(async () =>
            {
                try
                {
                    var dtoList = request.Messages.ToList();
                    await foreach (var chunk in _completion.CompleteAsync(
                        dtoList,
                        request.Model,
                        request.ReasoningEffort,
                        _db))
                    {
                        if (!string.IsNullOrEmpty(chunk))
                        {
                            // Send to the specific client via SignalR
                            await _hubContext.Clients.Client(connectionId)
                                .SendAsync("ReceiveChunk", chunk);
                        }
                    }

                    // Signal completion to the client
                    await _hubContext.Clients.Client(connectionId)
                        .SendAsync("StreamComplete");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during background AI streaming: {ex.Message}\n{ex.StackTrace}");
                    // Signal error to the client
                    await _hubContext.Clients.Client(connectionId)
                        .SendAsync("StreamError", ex.Message);
                }
            });

            // Return HTTP 200 OK immediately, indicating the request was accepted
            return Ok(); // Or return Accepted() for a more semantically correct response.
        }
    }
}