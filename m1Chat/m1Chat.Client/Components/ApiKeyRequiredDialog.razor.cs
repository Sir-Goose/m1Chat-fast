using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace m1Chat.Client.Components;

public partial class ApiKeyRequiredDialog : ComponentBase
{
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    [CascadingParameter] IMudDialogInstance MudDialog { get; set; } = default!;

    private void GoToProfile()
    {
        // Close the dialog and indicate that the user chose to go to the profile
        MudDialog.Close(DialogResult.Ok(true));
    }

    private void Cancel()
    {
        // Just cancel the dialog
        MudDialog.Cancel();
    }
}