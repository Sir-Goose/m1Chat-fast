using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace m1Chat.Client.Components;

public partial class ChatList<TItem> : ComponentBase
{
    [Parameter]
    public IEnumerable<TItem> Items { get; set; } = new List<TItem>();

    [Parameter]
    public string? ActiveItemId { get; set; }

    [Parameter]
    public EventCallback<TItem> OnItemSelected { get; set; }

    [Parameter]
    public EventCallback<TItem> OnItemPinned { get; set; }

    [Parameter]
    public EventCallback<TItem> OnItemDeleted { get; set; }

    [Parameter]
    public EventCallback<TItem> OnItemRenamed { get; set; }

    [Parameter]
    public EventCallback<TItem> OnItemExported { get; set; }

    [Parameter]
    public Func<TItem, string> GetItemId { get; set; } = item => item?.ToString() ?? string.Empty;

    [Parameter]
    public Func<TItem, string> GetItemName { get; set; } = item => item?.ToString() ?? string.Empty;
    
    [Parameter]
    public EventCallback<TItem> OnItemMouseEnter { get; set; }
}