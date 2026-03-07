using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MudBlazor;
using m1Chat.Client.Services;
using System.Linq;

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
    [Parameter] public Dictionary<string, string> UserApiKeys { get; set; } = new();
    [Parameter] public List<FileUploadService.UploadedFileInfo> CurrentMessageFiles { get; set; } = new();
    [Parameter] public EventCallback<List<FileUploadService.UploadedFileInfo>> CurrentMessageFilesChanged { get; set; }
    [Parameter] public bool IsSendingMessage { get; set; }
    [Parameter] public bool ShowFileUpload { get; set; }
    [Parameter] public EventCallback<bool> ShowFileUploadChanged { get; set; }

    // Callbacks
    [Parameter] public EventCallback<string> OnMessageSubmitted { get; set; }
    [Parameter] public EventCallback<KeyboardEventArgs> OnKeyUp { get; set; }
    [Parameter] public EventCallback OnMessageAccepted { get; set; }

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
    private Dictionary<string, string> _prevUserApiKeys = new();
    private bool _forceRender;
    private (long Id, System.Diagnostics.Stopwatch Timer, string Source)? _pendingSetInputPerf;
    private long _uiPerfSequence;

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
            { "Moonshot Kimi K2 (OpenRouter)", ThinkingOptionsNoneOnly },
            { "tngtech/DeepSeek-TNG-R1T2-Chimera", ThinkingOptionsNoneOnly},
            { "Gemini 2.5 Pro (AI Studio)", ThinkingOptionsLowMediumHigh},
            { "Gemini 2.5 Flash (AI Studio)", ThinkingOptionsNoneLowMediumHigh },
            { "Gemini 2.0 Flash (AI Studio)", ThinkingOptionsNoneOnly },
            { "Qwen3 235B", ThinkingOptionsLowMediumHigh },
            { "Qwen3 30B", ThinkingOptionsLowMediumHigh },
            { "Gemma 3 27B (OpenRouter)", ThinkingOptionsNoneOnly },
            { "Devstral Small (Mistral AI)", ThinkingOptionsNoneOnly },
            { "Devstral Medium (Mistral AI)", ThinkingOptionsNoneOnly },
            { "Magistral Small (Mistral AI)", ThinkingOptionsNoneOnly },
            { "Mistral Medium (Mistral AI)", ThinkingOptionsNoneOnly },
            { "Magistral Medium", ThinkingOptionsNoneOnly },
            { "Cypher Alpha (OpenRouter)", ThinkingOptionsNoneOnly },
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
            !ReferenceEquals(_prevCurrentMessageFiles, CurrentMessageFiles) ||
            !_prevUserApiKeys.SequenceEqual(UserApiKeys);

        // Update previous values
        _prevIsSendingMessage = IsSendingMessage;
        _prevSelectedModel = SelectedModel;
        _prevThinkingSelectedOption = ThinkingSelectedOption;
        _prevShowFileUpload = ShowFileUpload;
        _prevCurrentMessageFiles = CurrentMessageFiles;
        _prevUserApiKeys = new Dictionary<string, string>(UserApiKeys);

        return shouldRender;
    }

    private bool IsModelDisabled(string modelName)
    {
        // Free tier models are never disabled
        if (modelName.Contains("Free Tier"))
        {
            return false;
        }

        // Map model names to required API key providers
        var requiredProvider = modelName switch
        {
            var name when name.Contains("AI Studio") => "AIStudio",
            var name when name.Contains("Chutes") => "Chutes",
            var name when name.Contains("Mistral AI") => "Mistral",
            var name when name.Contains("OpenRouter") => "OpenRouter",
            var name when name.Contains("Groq") => "Groq",
            _ => null
        };

        // If we can't determine the provider, don't disable the model
        if (requiredProvider == null)
        {
            return false;
        }

        // Check if user has a valid API key for this provider
        return !UserApiKeys.TryGetValue(requiredProvider, out var apiKey) || string.IsNullOrEmpty(apiKey);
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
            await Js.InvokeVoidAsync("event.preventDefault"); // Prevent newline in textarea
            if (!string.IsNullOrWhiteSpace(_messageTextInternal) || CurrentMessageFiles.Any())
            {
                await HandleSendMessage();
            }
        }
    }

    private async Task HandleSendMessage()
    {
        if (!IsSendingMessage && OnMessageSubmitted.HasDelegate)
        {
            // Store the message text before attempting to send
            var messageToSend = _messageTextInternal;

            // Invoke the OnMessageSubmitted callback with the current internal text
            await OnMessageSubmitted.InvokeAsync(messageToSend);

            // Note: Input clearing is now handled by the parent component
            // after confirming the message was accepted
        }
    }

    /// <summary>
    /// Public method to clear the input text. Called by parent when message is successfully accepted.
    /// </summary>
    public async Task ClearInput()
    {
        await SetInputText("", "ClearInput");
    }

    /// <summary>
    /// Public method to programmatically set the message text in the input field.
    /// Used by the parent component for "Edit Message" functionality.
    /// </summary>
    public async Task SetInputText(string text, string source = "Unknown")
    {
        var perf = StartUiPerf("SetInputText", source);
        _messageTextInternal = text;
        _forceRender = true;
        _pendingSetInputPerf = (perf.Id, perf.Timer, source);
        StateHasChanged(); // Re-render this component to reflect the new text
        LogUiPerf("SetInputText", source, perf, "first-statehaschanged");
        await FocusAsync(source); // Focus the input field after setting its content
        LogUiPerf("SetInputText", source, perf, "focus-complete");
    }

    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        if (_pendingSetInputPerf != null)
        {
            var perf = _pendingSetInputPerf.Value;
            LogUiPerf("SetInputText", perf.Source, (perf.Id, perf.Timer), "post-render");
            _pendingSetInputPerf = null;
        }

        return Task.CompletedTask;
    }

    private async Task FocusAsync(string source)
    {
        var perf = StartUiPerf("FocusInput", source);
        if (_messageTextField != null)
        {
            await _messageTextField.FocusAsync();
        }
        LogUiPerf("FocusInput", source, perf, "complete");
    }

    private (long Id, System.Diagnostics.Stopwatch Timer) StartUiPerf(string action, string source)
    {
        var id = System.Threading.Interlocked.Increment(ref _uiPerfSequence);
        var timer = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine($"[UIPERF][ChatInputArea][{action}][{source}]#{id} start");
        return (id, timer);
    }

    private static void LogUiPerf(string action, string source, (long Id, System.Diagnostics.Stopwatch Timer) perf, string checkpoint)
    {
        Console.WriteLine($"[UIPERF][ChatInputArea][{action}][{source}]#{perf.Id} {checkpoint} +{perf.Timer.ElapsedMilliseconds}ms");
    }
}
