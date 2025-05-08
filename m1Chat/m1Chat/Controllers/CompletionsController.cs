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
            Response.Headers.Add("Content-Type", "text/event-stream");

            await foreach (var chunk in _completion.CompleteAsync(request.Messages))
            {
                await Response.WriteAsync($"data: {chunk}\n\n");
                await Response.Body.FlushAsync();
            }
        }
    }
}