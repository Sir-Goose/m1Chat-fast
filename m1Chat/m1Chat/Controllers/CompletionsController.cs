using Microsoft.AspNetCore.Mvc;
using m1Chat.Services;

namespace m1Chat.Controllers
{
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
                await foreach (var chunk in _completion.CompleteAsync(request.Messages))
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(new { content = chunk });
                    await Response.WriteAsync(json + "\n");
                    await Response.Body.FlushAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("I am in the Completions Controller");
            }
        }
    }
}