using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using m1Chat.Data;
using m1Chat.Hubs;
using m1Chat.Services;
using Microsoft.AspNetCore.Http; // Make sure this is included for HttpContext if needed, though not directly used in the fix

namespace m1Chat.Controllers
{
    public class ChatHistoryRequest
    {
        // ADDED: Unique identifier for the streaming request
        public Guid RequestId { get; set; }
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
        public async Task<IActionResult> Stream(
            [FromBody] ChatHistoryRequest request,
            [FromQuery] string connectionId)
        {
            // Validate the requestId
            if (request.RequestId == Guid.Empty)
            {
                // This indicates a client-side problem or a request not adhering to the new protocol
                // You might want to log this or return a specific error
                return BadRequest(new { error = "Streaming RequestId is required." });
            }

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
                        // MODIFIED: Pass the requestId to the Hub method
                        await _hubContext.Clients.Client(connectionId)
                            .SendAsync("ReceiveChunk", request.RequestId, chunk);
                    }
                }

                // MODIFIED: Pass the requestId to the Hub method
                await _hubContext.Clients.Client(connectionId)
                    .SendAsync("StreamComplete", request.RequestId);

                return Ok();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in CompletionsController.Stream: {ex.Message}\n{ex.StackTrace}");
                // MODIFIED: Pass the requestId to the Hub method for errors as well
                await _hubContext.Clients.Client(connectionId)
                    .SendAsync("StreamError", request.RequestId, ex.Message);
                return StatusCode(500, new { error = "An error occurred during streaming" });
            }
        }
    }
}
