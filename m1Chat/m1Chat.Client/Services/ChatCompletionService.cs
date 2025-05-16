using System.Collections.Generic;
using System.Linq;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace m1Chat.Client.Services
{
    public class ChatMessage
    {
        public string Role { get; set; }
        public string Content { get; set; }
    }

    public class ChatCompletionService
    {
        private readonly IJSRuntime _jsRuntime;

        public ChatCompletionService(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        // now accepts a model name
        public IAsyncEnumerable<string> StreamCompletionAsync(
            List<ChatMessage> messages,
            string model
        )
        {
            var channel = Channel.CreateUnbounded<string>();
            var writer = channel.Writer;
            var dotnetRef = DotNetObjectReference.Create(
                new StreamCallback(writer)
            );

            // send both messages and model in one payload
            var payload = new
            {
                model,
                messages = messages
                    .Select(m => new { role = m.Role, content = m.Content })
                    .ToArray()
            };

            _ = _jsRuntime.InvokeVoidAsync(
                "streamChatCompletion",
                "/api/completions/stream",
                payload,
                dotnetRef
            );

            return channel.Reader.ReadAllAsync();
        }

        private class StreamCallback
        {
            private readonly ChannelWriter<string> _writer;
            public StreamCallback(ChannelWriter<string> writer) =>
                _writer = writer;

            [JSInvokable("Receive")]
            public void Receive(string chunk)
            {
                _writer.TryWrite(chunk);
            }

            [JSInvokable("Complete")]
            public void Complete()
            {
                _writer.TryComplete();
            }
        }
    }
}