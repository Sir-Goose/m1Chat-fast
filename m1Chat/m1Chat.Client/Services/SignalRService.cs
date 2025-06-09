using Microsoft.AspNetCore.SignalR.Client;
using System;
using Microsoft.AspNetCore.Components;
using System.Threading.Tasks; // Added for Func<Task>

namespace m1Chat.Client.Services
{
    public class SignalRService
    {
        private HubConnection _hubConnection;
        private readonly NavigationManager _navigationManager;
        private bool _isInitialized;
        private readonly object _lock = new object();

        public event Action<string> OnChunkReceived;
        public event Func<Task> OnStreamCompleted; // Changed to Func<Task>
        public event Action<string> OnStreamError;

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

            _hubConnection.On<string>("ReceiveChunk", chunk =>
                OnChunkReceived?.Invoke(chunk));

            // Changed to await the Func<Task> if it exists
            _hubConnection.On("StreamComplete", async () =>
            {
                if (OnStreamCompleted != null)
                {
                    await OnStreamCompleted.Invoke();
                }
            });

            _hubConnection.On<string>("StreamError", error =>
                OnStreamError?.Invoke(error));

            await _hubConnection.StartAsync();
        }

        public string? GetConnectionId() => _hubConnection?.ConnectionId;
    }
}