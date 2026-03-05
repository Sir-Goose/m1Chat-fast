using Microsoft.AspNetCore.Components;
using m1Chat.Client.Models;

namespace m1Chat.Client.Components;

public partial class ChatMessageItem : ComponentBase
{
    [Parameter] public ClientChatMessage Message { get; set; } = default!;
    [Parameter] public EventCallback<ClientChatMessage> OnEdit { get; set; }
    [Parameter] public EventCallback<ClientChatMessage> OnRegenerate { get; set; }
    [Parameter] public EventCallback<ClientChatMessage> OnCopy { get; set; }
    [Parameter] public EventCallback<ClientChatMessage> OnBranch { get; set; }
    
    private string? _lastMessageId;
    private string? _lastMessageText;
    private bool _lastIsUser;
    private bool _lastIsStreaming;
    private List<Guid>? _lastFileIds;

    protected override bool ShouldRender()
    {
        // If Message is null, always render
        if (Message == null)
            return true;

        bool shouldRender =
            _lastMessageId != Message.Id ||
            _lastMessageText != Message.Text ||
            _lastIsUser != Message.IsUser ||
            _lastIsStreaming != Message.IsStreaming ||
            !AreFileIdsEqual(_lastFileIds, Message.FileIds);

        // Update cache for next render
        _lastMessageId = Message.Id;
        _lastMessageText = Message.Text;
        _lastIsUser = Message.IsUser;
        _lastIsStreaming = Message.IsStreaming;
        _lastFileIds = Message.FileIds != null ? new List<Guid>(Message.FileIds) : null;

        return shouldRender;
    }

    private static bool AreFileIdsEqual(List<Guid>? a, List<Guid>? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a == null || b == null) return false;
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
        {
            if (a[i] != b[i]) return false;
        }
        return true;
    }
}
