using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Markdig;
using Ganss.Xss;
using System.Net;
using System.Text.RegularExpressions;

namespace m1Chat.Client.Components;

public partial class AiMarkdown : ComponentBase
{
    [Inject] private IJSRuntime Js { get; set; } = default!;

    [Parameter] public string Markdown { get; set; } = "";
    [Parameter] public bool IsStreaming { get; set; } = false;

    private ElementReference _aiDiv;
    private string _lastMarkdown = "";
    private string _lastProcessedHtml = ""; // Cache the final result
    private bool _lastIsStreaming = false; // Cache streaming state
    private bool _lastShouldRenderMath;
    private bool _lastShouldHighlightCode;
    
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();
        
    // Configure sanitizer once as static
    private static readonly HtmlSanitizer HtmlSanitizer = new HtmlSanitizer();
    private static readonly Regex HtmlTagRegex = new(@"<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex MarkdownSyntaxRegex = new(@"(^|\n)\s{0,3}(#{1,6}\s|[-*+]\s|\d+\.\s|>)|(```|`|\*\*|__|\[.+\]\(.+\)|\|.+\|)", RegexOptions.Compiled);
    private static readonly Regex MathRegex = new(@"(\$\$.*?\$\$|\\\(.*?\\\))", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex CodeRegex = new(@"(```|`[^`\n]+`)", RegexOptions.Compiled);
    
    static AiMarkdown()
    {
        // Configure sanitizer once in static constructor
        HtmlSanitizer.AllowedTags.Add("pre");
        HtmlSanitizer.AllowedTags.Add("code");
        HtmlSanitizer.AllowedTags.Add("span");
        HtmlSanitizer.AllowedAttributes.Add("class");
    }
    
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (Markdown != _lastMarkdown || IsStreaming != _lastIsStreaming)
        {
            _lastMarkdown = Markdown;
            _lastIsStreaming = IsStreaming;

            string safeHtml;
            bool shouldRenderMath;
            bool shouldHighlightCode;

            if (IsStreaming)
            {
                safeHtml = $"<div class='ai-message-streaming-text'>{ConvertPlainTextToHtml(Markdown)}</div>";
                shouldRenderMath = false;
                shouldHighlightCode = false;
            }
            else if (LooksLikePlainText(Markdown))
            {
                safeHtml = ConvertPlainTextToHtml(Markdown);
                shouldRenderMath = false;
                shouldHighlightCode = false;
            }
            else
            {
                var unsafeHtml = Markdig.Markdown.ToHtml(Markdown, Pipeline);
                safeHtml = HtmlSanitizer.Sanitize(unsafeHtml);
                shouldRenderMath = ContainsMath(Markdown);
                shouldHighlightCode = ContainsCode(Markdown);
            }

            if (safeHtml != _lastProcessedHtml
                || shouldRenderMath != _lastShouldRenderMath
                || shouldHighlightCode != _lastShouldHighlightCode)
            {
                _lastProcessedHtml = safeHtml;
                _lastShouldRenderMath = shouldRenderMath;
                _lastShouldHighlightCode = shouldHighlightCode;
                await Js.InvokeVoidAsync("setInnerHtmlAndRenderMath", _aiDiv, safeHtml, shouldRenderMath, shouldHighlightCode);
            }
        }
    }

    private static bool LooksLikePlainText(string content) =>
        !ContainsHtml(content) && !MarkdownSyntaxRegex.IsMatch(content) && !ContainsMath(content) && !ContainsCode(content);

    private static bool ContainsHtml(string content) =>
        content.Contains('<') && content.Contains('>') && HtmlTagRegex.IsMatch(content);

    private static bool ContainsMath(string content) => MathRegex.IsMatch(content);

    private static bool ContainsCode(string content) => CodeRegex.IsMatch(content);

    private static string ConvertPlainTextToHtml(string content) =>
        WebUtility.HtmlEncode(content).Replace("\r\n", "\n").Replace("\n", "<br />");
}
