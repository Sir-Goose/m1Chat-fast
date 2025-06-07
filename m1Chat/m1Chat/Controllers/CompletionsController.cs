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
        public async Task<IActionResult> Stream(
            [FromBody] ChatHistoryRequest request,
            [FromQuery] string connectionId)
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
                        await _hubContext.Clients.Client(connectionId)
                            .SendAsync("ReceiveChunk", chunk);
                    }
                }

                await _hubContext.Clients.Client(connectionId)
                    .SendAsync("StreamComplete");

                return Ok();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in CompletionsController.Stream: {ex.Message}\n{ex.StackTrace}");
                await _hubContext.Clients.Client(connectionId)
                    .SendAsync("StreamError", ex.Message);
                return StatusCode(500, new { error = "An error occurred during streaming" });
            }
        }
    }
}