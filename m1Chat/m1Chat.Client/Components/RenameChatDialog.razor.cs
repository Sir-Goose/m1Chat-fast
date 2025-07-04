using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;

namespace m1Chat.Client.Components;

public partial class RenameChatDialog : ComponentBase
{
    [CascadingParameter] IMudDialogInstance MudDialog { get; set; } = default!;
    [Parameter] public string CurrentName { get; set; } = "";

    private string NewName { get; set; } = "";
    private MudTextField<string> _newNameTextField = new();

    protected override void OnInitialized()
    {
        NewName = CurrentName;
    }

    private void Submit()
    {
        if (!string.IsNullOrWhiteSpace(NewName))
            MudDialog.Close(DialogResult.Ok(NewName));
    }

    private void Cancel() => MudDialog.Cancel();

    private async Task HandleKeyUp(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
        {
            await _newNameTextField.BlurAsync();
            Submit();
        }
    }
}