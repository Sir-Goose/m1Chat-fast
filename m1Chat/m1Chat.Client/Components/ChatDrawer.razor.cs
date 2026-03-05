using Microsoft.AspNetCore.Components;
using MudBlazor;
using m1Chat.Client.Services;
using System.Threading;

namespace m1Chat.Client.Components;

public partial class ChatDrawer : ComponentBase, IDisposable
{
    [Inject] private ChatService ChatService { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private SvgIcons SvgIcons { get; set; } = default!;

    [Parameter] public bool DrawerOpen { get; set; }
    [Parameter] public EventCallback<bool> DrawerOpenChanged { get; set; }
    [Parameter] public List<SidebarChat> SidebarChats { get; set; } = new();
    [Parameter] public string UserEmail { get; set; } = "";
    [Parameter] public string? ActiveChatId { get; set; }
    [Parameter] public EventCallback<SidebarChat> OnChatSelected { get; set; }
    [Parameter] public EventCallback OnCreateNewChat { get; set; }
    [Parameter] public EventCallback<SidebarChat> OnChatPinned { get; set; }
    [Parameter] public EventCallback<SidebarChat> OnChatDeleted { get; set; }
    [Parameter] public EventCallback<SidebarChat> OnChatRenamed { get; set; }

    // Search state
    private string _searchQuery = "";
    private List<SidebarChat> _searchResults = new();
    private MudTextField<string> _searchField = default!;
    private bool _isSearching => !string.IsNullOrWhiteSpace(_searchQuery);
    private CancellationTokenSource? _debounceCts;
    private const int DebounceTime = 300; // milliseconds

    // Filtered chat lists
    private List<SidebarChat> pinnedChats = new();
    private List<SidebarChat> todayChats = new();
    private List<SidebarChat> yesterdayChats = new();
    private List<SidebarChat> last7DaysChats = new();
    private List<SidebarChat> olderChats = new();

    // --- Rendering Optimization State ---
    private bool _lastDrawerOpen;
    private List<SidebarChat> _lastSidebarChats = new();
    private string? _lastActiveChatId;
    private string _lastUserEmail = "";

    // New state variables to cache search-related state
    private string _lastSearchQuery = "";
    private List<SidebarChat> _lastSearchResults = new(); // Use reference equality for list instance
    private bool _lastIsSearching;
    // ----------------------------------

    protected override void OnInitialized()
    {
        RebuildChatGroups();

        // Initialize previous values on first load
        _lastDrawerOpen = DrawerOpen;
        _lastSidebarChats = SidebarChats;
        _lastActiveChatId = ActiveChatId;
        _lastUserEmail = UserEmail;

        // Initialize search-related previous values
        _lastSearchQuery = _searchQuery;
        _lastSearchResults = _searchResults;
        _lastIsSearching = _isSearching;
    }

    protected override void OnParametersSet()
    {
        if (!ReferenceEquals(_lastSidebarChats, SidebarChats))
        {
            RebuildChatGroups();
        }
    }

    protected override bool ShouldRender()
    {
        // Determine if any parameter or relevant internal state has changed that warrants a re-render.
        bool shouldRender =
            DrawerOpen != _lastDrawerOpen ||
            ActiveChatId != _lastActiveChatId ||
            UserEmail != _lastUserEmail ||
            !ReferenceEquals(SidebarChats, _lastSidebarChats) || // Check if the list instance itself has changed (e.g., chat added/deleted)
            _searchQuery != _lastSearchQuery || // Check if search query changed
            !ReferenceEquals(_searchResults, _lastSearchResults) || // Check if search results list instance changed
            _isSearching != _lastIsSearching; // Check if search mode changed

        // Update the 'last' values for the next render cycle.
        _lastDrawerOpen = DrawerOpen;
        _lastActiveChatId = ActiveChatId;
        _lastUserEmail = UserEmail;
        _lastSidebarChats = SidebarChats;

        // Update search-related 'last' values
        _lastSearchQuery = _searchQuery;
        _lastSearchResults = _searchResults;
        _lastIsSearching = _isSearching;

        // Console.WriteLine($"ChatDrawer ShouldRender: {shouldRender}"); // Optional debug log

        return shouldRender;
    }

    private async Task ToggleDrawer()
    {
        DrawerOpen = !DrawerOpen;
        await DrawerOpenChanged.InvokeAsync(DrawerOpen);
    }

    private async Task HandleDrawerOpenChanged(bool isOpen)
    {
        DrawerOpen = isOpen;
        await DrawerOpenChanged.InvokeAsync(isOpen);
    }

    private async Task HandleCreateNewChat()
    {
        _ = ToggleDrawer();
        await OnCreateNewChat.InvokeAsync();
    }


    private async Task HandleSelectChat(SidebarChat chat)
    {
        _ = ToggleDrawer();
        await OnChatSelected.InvokeAsync(chat);
    }

    private async Task HandlePinChat(SidebarChat chat) =>
      await OnChatPinned.InvokeAsync(chat);

    private async Task HandleDeleteChat(SidebarChat chat) =>
      await OnChatDeleted.InvokeAsync(chat);

    private Task HandleExportChat(SidebarChat chat)
    {
        Snackbar.Add("Feature not yet implemented", Severity.Warning);
        return Task.CompletedTask;
    }


    private async Task HandleRenameChat(SidebarChat chat)
    {
        var parameters = new DialogParameters { ["CurrentName"] = chat.Name };
        var options =
          new DialogOptions
          {
              CloseButton = true,
              MaxWidth = MaxWidth.Small,
              FullWidth = true,
              BackgroundClass = "blur-background"
          };

        var dialog = await DialogService.ShowAsync<RenameChatDialog>("Rename Chat", parameters, options);
        var result = await dialog.Result;

        if (!(result?.Canceled ?? true) && result!.Data is string newName && !string.IsNullOrWhiteSpace(newName))
        {
            var updatedChat = chat with { Name = newName };
            await OnChatRenamed.InvokeAsync(updatedChat);
        }
    }

    private async Task OnSearchValueChanged(string value)
    {
        _searchQuery = value;
        await TriggerSearch();
    }

    private void ViewProfile()
    {
        NavigationManager.NavigateTo("/Profile");
    }

    private async Task OnSearchInput(ChangeEventArgs args)
    {
        _searchQuery = args.Value?.ToString() ?? "";
        await TriggerSearch();
    }

    private async Task TriggerSearch()
    {
        // Cancel any previous search
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        _debounceCts = new CancellationTokenSource();

        try
        {
            // Wait for the debounce time
            await Task.Delay(DebounceTime, _debounceCts.Token);
            await SearchChats();
        }
        catch (TaskCanceledException)
        {
            // Search was canceled by new input - ignore
        }
    }

    private async Task SearchChats()
    {
        if (string.IsNullOrWhiteSpace(_searchQuery))
        {
            _searchResults = new();
            StateHasChanged();
            return;
        }

        try
        {
            var searchResults = await ChatService.SearchChatsAsync(_searchQuery);
            var sidebarResults = searchResults.Select(r => new SidebarChat(
              r.Id.ToString(),
              r.Name,
              r.Model,
              r.IsPinned,
              r.LastUpdatedAt
            )).ToList();

            // IMPORTANT: Assign a NEW list instance to _searchResults
            // This ensures that `!ReferenceEquals(_searchResults, _lastSearchResults)` in ShouldRender will be true
            _searchResults = sidebarResults
              .OrderByDescending(r => r.LastUpdatedAt)
              .ToList();

            StateHasChanged();
        }
        catch (Exception)
        {
            Snackbar.Add("Search failed", Severity.Error);
            _searchResults = new();
            StateHasChanged();
        }
    }

    private async Task ClearSearch()
    {
        _searchQuery = "";
        _searchResults = new();
        _debounceCts?.Cancel();

        if (_searchField != null)
        {
            await _searchField.FocusAsync();
        }

        StateHasChanged();
    }

    public async Task FocusSearchFieldAsync()
    {
        if (_searchField != null)
        {
            await _searchField.FocusAsync();
        }
    }


    public void Dispose()
    {
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
    }

    private void RebuildChatGroups()
    {
        var sortedPinned = SidebarChats
            .Where(c => c.IsPinned)
            .OrderByDescending(c => c.LastUpdatedAt)
            .ToList();

        var sortedNonPinned = SidebarChats
            .Where(c => !c.IsPinned)
            .OrderByDescending(c => c.LastUpdatedAt)
            .ToList();

        var today = DateTime.Today;
        var yesterday = today.AddDays(-1);
        var lastWeekStart = today.AddDays(-8);

        pinnedChats = sortedPinned;
        todayChats = sortedNonPinned.Where(c => c.LastUpdatedAt.Date == today).ToList();
        yesterdayChats = sortedNonPinned.Where(c => c.LastUpdatedAt.Date == yesterday).ToList();
        last7DaysChats = sortedNonPinned
            .Where(c => c.LastUpdatedAt.Date >= lastWeekStart && c.LastUpdatedAt.Date <= today.AddDays(-2))
            .ToList();
        olderChats = sortedNonPinned.Where(c => c.LastUpdatedAt.Date < lastWeekStart).ToList();
    }

    public record SidebarChat(
      string Id,
      string Name,
      string Model,
      bool IsPinned = false,
      DateTime LastUpdatedAt = default
    );
}
