using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.ComponentModel.DataAnnotations;
using Markdig;
using Ganss.Xss;
using System.Text.RegularExpressions;
using System.Net;

namespace m1Chat.Client.Components;

public partial class UserMarkdown : ComponentBase
{
    [Inject] private IJSRuntime Js { get; set; } = default!;

    [Parameter] public string Markdown { get; set; } = "";

    private ElementReference _userDiv;
    private string _lastMarkdown = "";
    private string _lastProcessedHtml = ""; // Cache the final result

    // Use the same pipeline configuration as AiMarkdown for consistency
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    // Configure sanitizer once as static
    private static readonly HtmlSanitizer HtmlSanitizer = new HtmlSanitizer();
    
    // Compile regex once for better performance
    private static readonly Regex HtmlTagRegex = new Regex(@"<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex MarkdownSyntaxRegex = new(@"(^|\n)\s{0,3}(#{1,6}\s|[-*+]\s|\d+\.\s|>)|(```|`|\*\*|__|\[.+\]\(.+\)|\|.+\|)", RegexOptions.Compiled);
    private static readonly Regex MathRegex = new(@"(\$\$.*?\$\$|\\\(.*?\\\))", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex CodeRegex = new(@"(```|`[^`\n]+`)", RegexOptions.Compiled);
    
    static UserMarkdown()
    {
        // Configure sanitizer once in static constructor
        HtmlSanitizer.AllowedTags.Add("pre");
        HtmlSanitizer.AllowedTags.Add("code");
        HtmlSanitizer.AllowedTags.Add("span");
        HtmlSanitizer.AllowedAttributes.Add("class");
    }
    
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // Check if the markdown content has actually changed to avoid unnecessary JS calls
        if (Markdown != _lastMarkdown)
        {
            _lastMarkdown = Markdown;

            string safeHtml;
            bool shouldRenderMath;
            bool shouldHighlightCode;

            if (LooksLikePlainText(Markdown))
            {
                safeHtml = ConvertPlainTextToHtml(Markdown);
                shouldRenderMath = false;
                shouldHighlightCode = false;
            }
            else
            {
                var processedMarkdown = ContainsHtml(Markdown)
                    ? WebUtility.HtmlEncode(Markdown)
                    : Markdown;

                var unsafeHtml = Markdig.Markdown.ToHtml(processedMarkdown, Pipeline);
                safeHtml = HtmlSanitizer.Sanitize(unsafeHtml);
                shouldRenderMath = ContainsMath(Markdown);
                shouldHighlightCode = ContainsCode(Markdown);
            }

            if (safeHtml != _lastProcessedHtml)
            {
                _lastProcessedHtml = safeHtml;
                await Js.InvokeVoidAsync("setInnerHtmlAndRenderMath", _userDiv, safeHtml, shouldRenderMath, shouldHighlightCode);
            }
        }
    }
    
    private static bool LooksLikePlainText(string content) =>
        !ContainsHtml(content) && !MarkdownSyntaxRegex.IsMatch(content) && !ContainsMath(content) && !ContainsCode(content);

    private static bool ContainsHtml(string content)
    {
        // Optimized: check for angle brackets first (cheapest check)
        return content.Contains('<') && content.Contains('>') && HtmlTagRegex.IsMatch(content);
    }

    private static bool ContainsMath(string content) => MathRegex.IsMatch(content);

    private static bool ContainsCode(string content) => CodeRegex.IsMatch(content);

    private static string ConvertPlainTextToHtml(string content) =>
        WebUtility.HtmlEncode(content).Replace("\r\n", "\n").Replace("\n", "<br />");
}
