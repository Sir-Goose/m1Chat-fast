// File: m1Chat.Controllers/CompletionsController.cs
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using m1Chat.Services; // ChatMessageDto & Completion live here
using Microsoft.AspNetCore.Http; // Required for Response.WriteAsync

namespace m1Chat.Controllers
{
    // DTO for binding model + messages
    public class ChatHistoryRequest
    {
        public string Model { get; set; } = string.Empty;
        public string ReasoningEffort { get; set; } = "Medium"; // Added, with default
        public ChatMessageDto[] Messages { get; set; } =
            Array.Empty<ChatMessageDto>();
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
            Response.Headers.Append("Cache-Control", "no-cache");
            Response.Headers.Append("X-Accel-Buffering", "no"); // Useful for Nginx

            try
            {
                var dtoList = request.Messages.ToList();
                await foreach (
                    var chunk in _completion.CompleteAsync(
                        dtoList,
                        request.Model,
                        request.ReasoningEffort
                    ) // Pass ReasoningEffort
                )
                {
                    if (!string.IsNullOrEmpty(chunk)) // Ensure chunk is not null or empty before serializing
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
                // Consider how to report this error back to the client if the stream has already started.
                // If headers not sent, can send a 500. Otherwise, might need a special error object in the stream.
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
                    // Stream already started, try to send an error object if possible
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