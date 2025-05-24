using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using m1Chat.Services;
using m1Chat.Data;
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

        public CompletionsController(Completion completion, ChatDbContext db)
        {
            _completion = completion;
            _db = db;
        }

        [HttpPost("stream")]
        public async Task Stream([FromBody] ChatHistoryRequest request)
        {
            Response.ContentType = "application/json"; 
            Response.Headers.Append("Cache-Control", "no-cache");
            Response.Headers.Append("X-Accel-Buffering", "no");

            try
            {
                var dtoList = request.Messages.ToList();
                await foreach (
                    var chunk in _completion.CompleteAsync(
                        dtoList,
                        request.Model,
                        request.ReasoningEffort,
                        _db // Pass database context for file handling
                    )
                )
                {
                    if (!string.IsNullOrEmpty(chunk))
                    {
                        var json = System.Text.Json.JsonSerializer.Serialize(
                            new { content = chunk }
                        );
                        await Response.WriteAsync(json + "\n");
                        await Response.Body.FlushAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"Error in CompletionsController.Stream: {ex.Message}\n{ex.StackTrace}"
                );
                if (!Response.HasStarted)
                {
                    Response.StatusCode = StatusCodes.Status500InternalServerError;
                    await Response.WriteAsync(
                        System.Text.Json.JsonSerializer.Serialize(
                            new { error = "An internal server error occurred." }
                        ) + "\n"
                    );
                }
                else
                {
                    try
                    {
                        var errorJson = System.Text.Json.JsonSerializer.Serialize(
                            new { error = ex.Message }
                        );
                        await Response.WriteAsync(errorJson + "\n");
                        await Response.Body.FlushAsync();
                    }
                    catch (Exception flushEx)
                    {
                        Console.WriteLine(
                            $"Error flushing error message to response: {flushEx.Message}"
                        );
                    }
                }
            }
        }
    }
}