using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using m1Chat.Services;            // <-- ChatMessageDto & Completion live here

namespace m1Chat.Controllers
{
    // DTO for binding model + messages
    public class ChatHistoryRequest
    {
        public string            Model    { get; set; } = string.Empty;
        public ChatMessageDto[]  Messages { get; set; } = Array.Empty<ChatMessageDto>();
    }

    [ApiController]
    [Route("api/[controller]")]
    public class CompletionsController : ControllerBase
    {
        private readonly Completion _completion;

        public CompletionsController(Completion completion)
        {
            _completion = completion;
        }

        [HttpPost("stream")]
        public async Task Stream([FromBody] ChatHistoryRequest request)
        {
            Response.ContentType = "application/json";
            Response.Headers.Add("Cache-Control", "no-cache");

            try
            {
                // Directly pass the DTO list to your service
                var dtoList = request.Messages.ToList();
                await foreach (var chunk in _completion.CompleteAsync(dtoList, request.Model))
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(new { content = chunk });
                    await Response.WriteAsync(json + "\n");
                    await Response.Body.FlushAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in CompletionsController.Stream: " + ex);
            }
        }
    }
}