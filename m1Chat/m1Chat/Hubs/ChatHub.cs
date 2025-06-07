using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace m1Chat.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        public Task SendChunk(string connectionId, string chunk)
        {
            return Clients.Client(connectionId).SendAsync("ReceiveChunk", chunk);
        }

        public Task CompleteStream(string connectionId)
        {
            return Clients.Client(connectionId).SendAsync("StreamComplete");
        }

        public Task StreamError(string connectionId, string error)
        {
            return Clients.Client(connectionId).SendAsync("StreamError", error);
        }
    }
}