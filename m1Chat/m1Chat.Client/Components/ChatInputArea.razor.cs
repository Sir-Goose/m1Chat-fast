using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MudBlazor;
using m1Chat.Client.Services;

namespace m1Chat.Client.Components;

public partial class ChatInputArea : ComponentBase
{
    [Inject] private IJSRuntime Js { get; set; } = default!;
    [Inject] private SvgIcons SvgIcons { get; set; } = default!;

    // Parameters from parent
    [Parameter] public string SelectedModel { get; set; } = "";
    [Parameter] public EventCallback<string> SelectedModelChanged { get; set; }
    [Parameter] public string ThinkingSelectedOption { get; set; } = "None";
    [Parameter] public EventCallback<string> ThinkingSelectedOptionChanged { get; set; }
    [Parameter] public Dictionary<string, List<string>> EnabledModelsByProvider { get; set; } = new();
    [Parameter] public List<FileUploadService.UploadedFileInfo> CurrentMessageFiles { get; set; } = new();
    [Parameter] public EventCallback<List<FileUploadService.UploadedFileInfo>> CurrentMessageFilesChanged { get; set; }
    [Parameter] public bool IsSendingMessage { get; set; }
    [Parameter] public bool ShowFileUpload { get; set; }
    [Parameter] public EventCallback<bool> ShowFileUploadChanged { get; set; }

    // Callbacks
    [Parameter] public EventCallback<string> OnMessageSubmitted { get; set; }
    [Parameter] public EventCallback<KeyboardEventArgs> OnKeyUp { get; set; }

    // Internal state
    private string _messageTextInternal = "";
    private bool _showFileUpload;
    private bool _isThinkingSelectorDisabled;
    private List<string> _currentThinkingOptions = new();
    private Dictionary<string, List<string>> _modelThinkingOptionsMap = new();

    // Component references
    private MudTextField<string>? _messageTextField;
    private MudSelect<string>? _modelSelect;
    private FileUploadComponent? _fileUploadComponentRef;
    private MudMenu? _mudMenu;

    // Static options
    private static readonly List<string> ThinkingOptionsLowMediumHigh = new() { "Low", "Medium", "High" };
    private static readonly List<string> ThinkingOptionsNoneLowMediumHigh = new() { "None", "Low", "Medium", "High" };
    private static readonly List<string> ThinkingOptionsNoneOnly = new() { "None" };

    // Rendering optimization state
    private bool _prevIsSendingMessage;
    private string _prevSelectedModel = "";
    private string _prevThinkingSelectedOption = "";
    private bool _prevShowFileUpload;
    private List<FileUploadService.UploadedFileInfo> _prevCurrentMessageFiles = new();
    private bool _forceRender;

    protected override void OnInitialized()
    {
        _showFileUpload = ShowFileUpload;

        _modelThinkingOptionsMap = new Dictionary<string, List<string>>()
        {
            { "Devstral Small (Free Tier)", ThinkingOptionsNoneOnly },
            { "Deepseek v3 (Chutes)", ThinkingOptionsNoneOnly },
            { "DeepSeek Prover v2", ThinkingOptionsLowMediumHigh },
            { "Deepseek r1", ThinkingOptionsLowMediumHigh },
            { "Deepseek R1 0528 (Chutes)", ThinkingOptionsNoneOnly },
            { "DeepSeek r1 v3 Chimera", ThinkingOptionsLowMediumHigh },
            { "tngtech/DeepSeek-TNG-R1T2-Chimera", ThinkingOptionsNoneOnly},
            { "Gemini 2.5 Pro (AI Studio)", ThinkingOptionsLowMediumHigh},
            { "Gemini 2.5 Flash (AI Studio)", ThinkingOptionsNoneLowMediumHigh },
            { "Gemini 2.0 Flash (AI Studio)", ThinkingOptionsNoneOnly },
            { "Qwen3 235B", ThinkingOptionsLowMediumHigh },
            { "Qwen3 30B", ThinkingOptionsLowMediumHigh },
            { "Gemma 3 27B (OpenRouter)", ThinkingOptionsNoneOnly },
            { "Devstral Small (Mistral AI)", ThinkingOptionsNoneOnly },
            { "Magistral Small (Mistral AI)", ThinkingOptionsNoneOnly },
            { "Mistral Medium (Mistral AI)", ThinkingOptionsNoneOnly },
            { "Magistral Medium", ThinkingOptionsNoneOnly },
            { "Llama 3.1 8B (Groq)", ThinkingOptionsNoneOnly },
            { "Llama 4 Scout (Groq)", ThinkingOptionsNoneOnly },
            { "Llama 4 Maverick (OpenRouter)", ThinkingOptionsNoneOnly }
        };

        ApplyThinkingOptionsForModel(SelectedModel);
    }

    protected override void OnParametersSet()
    {
        // Keep _showFileUpload in sync with parent's ShowFileUpload parameter
        _showFileUpload = ShowFileUpload;
    }

    // Rendering optimization: Only re-render when critical parameters change
    protected override bool ShouldRender()
    {
        if (_forceRender)
        {
            _forceRender = false;
            return true;
        }

        bool shouldRender =
            _prevIsSendingMessage != IsSendingMessage ||
            _prevSelectedModel != SelectedModel ||
            _prevThinkingSelectedOption != ThinkingSelectedOption ||
            _prevShowFileUpload != ShowFileUpload ||
            !ReferenceEquals(_prevCurrentMessageFiles, CurrentMessageFiles);

        // Update previous values
        _prevIsSendingMessage = IsSendingMessage;
        _prevSelectedModel = SelectedModel;
        _prevThinkingSelectedOption = ThinkingSelectedOption;
        _prevShowFileUpload = ShowFileUpload;
        _prevCurrentMessageFiles = CurrentMessageFiles;

        return shouldRender;
    }

    private string CurrentThinkingIcon
    {
        get
        {
            return ThinkingSelectedOption switch
            {
                "None" => SvgIcons.BrainIcons[0],
                "Low" => SvgIcons.BrainIcons[1],
                "Medium" => SvgIcons.BrainIcons[2],
                "High" => SvgIcons.BrainIcons[3],
                _ => SvgIcons.BrainIcons[0]
            };
        }
    }

    private string GetThinkingOptionIcon(string option)
    {
        return option switch
        {
            "None" => SvgIcons.BrainIcons[0],
            "Low" => SvgIcons.BrainIcons[1],
            "Medium" => SvgIcons.BrainIcons[2],
            "High" => SvgIcons.BrainIcons[3],
            _ => SvgIcons.BrainIcons[0]
        };
    }

    private void ApplyThinkingOptionsForModel(string modelName)
    {
        if (_modelThinkingOptionsMap.TryGetValue(modelName, out var options))
        {
            _currentThinkingOptions = options;
            if (modelName == "llama-3.1-8b-instant")
            {
                _isThinkingSelectorDisabled = true;
                _ = SetThinkingOption("None");
            }
            else
            {
                _isThinkingSelectorDisabled = false;
                if (!_currentThinkingOptions.Contains(ThinkingSelectedOption))
                {
                    _ = SetThinkingOption(_currentThinkingOptions.FirstOrDefault() ?? "None");
                }
            }
        }
        else
        {
            _currentThinkingOptions = ThinkingOptionsNoneLowMediumHigh;
            _isThinkingSelectorDisabled = false;
            if (!_currentThinkingOptions.Contains(ThinkingSelectedOption))
            {
                _ = SetThinkingOption("None");
            }
        }
    }

    private async Task OnModelChanged(string value)
    {
        ApplyThinkingOptionsForModel(value);
        await SelectedModelChanged.InvokeAsync(value);
    }

    private async Task SetThinkingOption(string option)
    {
        if (ThinkingSelectedOption != option)
        {
            await ThinkingSelectedOptionChanged.InvokeAsync(option);
        }
    }

    private async Task ToggleFileUpload()
    {
        _showFileUpload = !_showFileUpload;
        await ShowFileUploadChanged.InvokeAsync(_showFileUpload);
    }

    private async Task ToggleMenuAsync(MouseEventArgs args)
    {
        if (_mudMenu != null)
        {
            await _mudMenu.ToggleMenuAsync(args);
        }
    }

    private async Task HandleKeyUp(KeyboardEventArgs args)
    {

        // Handle Enter key (without Shift) to send message
        if (args is { Key: "Enter", ShiftKey: false })
        {
            _ = BlurAsync();
            _ = FocusAsync();
            await Js.InvokeVoidAsync("event.preventDefault"); // Prevent newline in textarea
            if (!string.IsNullOrWhiteSpace(_messageTextInternal) || CurrentMessageFiles.Any())
            {
                await HandleSendMessage();
            }
            await FocusAsync(); // Re-focus the text field after sending
        }
    }

    private async Task HandleSendMessage()
    {
        if (!IsSendingMessage && OnMessageSubmitted.HasDelegate)
        {
            // Invoke the OnMessageSubmitted callback with the current internal text
            _ = OnMessageSubmitted.InvokeAsync(_messageTextInternal);

            // Clear the input area internally AFTER the message has been submitted
            await SetInputText("");
            // The parent (Chat.razor) will handle clearing CurrentMessageFiles and _showFileUpload,
            // as those are bindable parameters owned by the parent.
        }
    }

    /// <summary>
    /// Public method to programmatically set the message text in the input field.
    /// Used by the parent component for "Edit Message" functionality.
    /// </summary>
    public async Task SetInputText(string text)
    {
        _messageTextInternal = text;
        _forceRender = true;
        StateHasChanged(); // Re-render this component to reflect the new text
        await FocusAsync(); // Focus the input field after setting its content
    }

    private async Task FocusAsync()
    {
        if (_messageTextField != null)
        {
            await _messageTextField.FocusAsync();
        }
    }

    private async Task BlurAsync()
    {
        if (_messageTextField != null)
        {
            await _messageTextField.BlurAsync();
        }
    }

    protected override void OnAfterRender(bool firstRender)
    {
        Console.WriteLine($"Input area rendered");
    }
}
