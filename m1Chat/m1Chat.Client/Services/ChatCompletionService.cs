using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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

        public IAsyncEnumerable<string> StreamCompletionAsync(List<ChatMessage> messages)
        {
            // unbounded channel to push chunks into
            var channel = Channel.CreateUnbounded<string>();
            var writer = channel.Writer;

            // create a .NET object reference that JS can call into
            var dotnetRef = DotNetObjectReference.Create(new StreamCallback(writer));

            // fire-and-forget JS interop
            _ = _jsRuntime.InvokeVoidAsync(
                "streamChatCompletion",
                "/api/completions/stream",
                messages.Select(m => new { role = m.Role, content = m.Content }).ToArray(),
                dotnetRef
            );

            return channel.Reader.ReadAllAsync();
        }

        private class StreamCallback
        {
            private readonly ChannelWriter<string> _writer;
            public StreamCallback(ChannelWriter<string> writer) => _writer = writer;

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