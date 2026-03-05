using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace m1Chat.Client.Components;

public partial class ScrollToBottomChip : ComponentBase
{
    [Inject] private IScrollManager ScrollManager { get; set; } = default!;

    private MudScrollToTop _scrollToBottomChip = default!;
    
    private async Task ScrollToBottom()
    {
        await ScrollManager.ScrollToBottomAsync(".chat-container", ScrollBehavior.Auto);
    }
}
