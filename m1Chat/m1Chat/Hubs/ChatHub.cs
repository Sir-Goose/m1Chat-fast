using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System; // Added for Guid
using System.Threading.Tasks;

namespace m1Chat.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        // Changed to accept requestId and include it in the message
        public Task SendChunk(string connectionId, Guid requestId, string chunk)
        {
            // The client-side "ReceiveChunk" handler will now expect (Guid, string)
            return Clients.Client(connectionId).SendAsync("ReceiveChunk", requestId, chunk);
        }

        // Changed to accept requestId and include it in the message
        public Task CompleteStream(string connectionId, Guid requestId)
        {
            // The client-side "StreamComplete" handler will now expect (Guid)
            return Clients.Client(connectionId).SendAsync("StreamComplete", requestId);
        }

        // Changed to accept requestId and include it in the message
        public Task StreamError(string connectionId, Guid requestId, string error)
        {
            // The client-side "StreamError" handler will now expect (Guid, string)
            return Clients.Client(connectionId).SendAsync("StreamError", requestId, error);
        }
    }
}