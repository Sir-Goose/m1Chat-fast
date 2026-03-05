using Microsoft.AspNetCore.Components;
using m1Chat.Client.Models;

namespace m1Chat.Client.Components;

public partial class ChatTranscript : ComponentBase
{
    [Parameter] public IReadOnlyList<ClientChatMessage> Messages { get; set; } = Array.Empty<ClientChatMessage>();
    [Parameter] public bool IsWaitingForFirstChunk { get; set; }
    [Parameter] public int Version { get; set; }
    [Parameter] public EventCallback<ClientChatMessage> OnEdit { get; set; }
    [Parameter] public EventCallback<ClientChatMessage> OnRegenerate { get; set; }
    [Parameter] public EventCallback<ClientChatMessage> OnCopy { get; set; }
    [Parameter] public EventCallback<ClientChatMessage> OnBranch { get; set; }

    private int _lastVersion = -1;

    protected override bool ShouldRender()
    {
        if (_lastVersion == Version)
        {
            return false;
        }

        _lastVersion = Version;
        return true;
    }
}
