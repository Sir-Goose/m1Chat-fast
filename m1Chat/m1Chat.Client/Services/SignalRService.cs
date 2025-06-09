using Microsoft.AspNetCore.SignalR.Client;
using System;
using Microsoft.AspNetCore.Components;
using System.Threading.Tasks;

namespace m1Chat.Client.Services
{
    public class SignalRService
    {
        private HubConnection _hubConnection;
        private readonly NavigationManager _navigationManager;
        private bool _isInitialized;
        private readonly object _lock = new object();

        // Updated event signatures to include Guid requestId
        public event Action<Guid, string>? OnChunkReceived;
        public event Func<Guid, Task>? OnStreamCompleted;
        public event Action<Guid, string>? OnStreamError;

        public SignalRService(NavigationManager navigationManager)
        {
            _navigationManager = navigationManager;
        }

        public async Task InitializeAsync()
        {
            lock (_lock)
            {
                if (_isInitialized) return;
                _isInitialized = true;
            }

            _hubConnection = new HubConnectionBuilder()
                .WithUrl(_navigationManager.ToAbsoluteUri("/chathub"))
                .WithAutomaticReconnect()
                .Build();

            // Updated to receive Guid requestId and string chunk
            _hubConnection.On<Guid, string>("ReceiveChunk", (requestId, chunk) =>
                OnChunkReceived?.Invoke(requestId, chunk));

            // Updated to receive Guid requestId and pass it to the Func<Task>
            _hubConnection.On<Guid>("StreamComplete", async (requestId) =>
            {
                if (OnStreamCompleted != null)
                {
                    await OnStreamCompleted.Invoke(requestId);
                }
            });

            // Updated to receive Guid requestId and string error
            _hubConnection.On<Guid, string>("StreamError", (requestId, error) =>
                OnStreamError?.Invoke(requestId, error));

            await _hubConnection.StartAsync();
        }

        public string? GetConnectionId() => _hubConnection?.ConnectionId;
    }
}
