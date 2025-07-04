using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using MudBlazor;
using m1Chat.Client.Services;

namespace m1Chat.Client.Components;

public partial class FileUploadComponent : ComponentBase
{
    [Inject] private IJSRuntime Js { get; set; } = default!;
    [Inject] private FileUploadService FileUploadService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    [Parameter] public List<FileUploadService.UploadedFileInfo> UploadedFiles { get; set; } = new();
    [Parameter] public EventCallback<List<FileUploadService.UploadedFileInfo>> UploadedFilesChanged { get; set; }

    private const string DefaultDragClass = "relative rounded-lg border-2 border-dashed pa-4 mud-width-full mud-height-full d-flex align-center justify-center";
    private string _dragClass = DefaultDragClass;
    private MudFileUpload<IReadOnlyList<IBrowserFile>> _mudFileUpload = default!;

    public async Task TriggerUploadAsync() => await OpenFilePickerAsync();
    
    private Task OpenFilePickerAsync() 
        => _mudFileUpload?.OpenFilePickerAsync() ?? Task.CompletedTask;

    private async Task OnFilesChanged(InputFileChangeEventArgs e)
    {
        ClearDragClass();
        var files = e.GetMultipleFiles(101);
        Console.WriteLine($"MudFileUpload: User selected {files.Count} files in this operation.");
        
        foreach (var file in files)
        {
            if (file.Size > 10 * 1024 * 1024)
            {
                Snackbar.Add($"File {file.Name} is too large (max 10MB)", Severity.Warning);
                continue;
            }

            // if (UploadedFiles.Any(f => f.OriginalFileName == file.Name))
            // {
            //     Snackbar.Add($"File {file.Name} is already uploaded", Severity.Info);
            //     continue;
            // }

            try
            {
                var uploadedFile = await FileUploadService.UploadFileAsync(file);
                UploadedFiles.Add(uploadedFile);
                await UploadedFilesChanged.InvokeAsync(UploadedFiles);
                //Snackbar.Add($"File {file.Name} uploaded successfully", Severity.Success);
            }
            catch (Exception ex)
            {
                Snackbar.Add($"Failed to upload {file.Name}: {ex.Message}", Severity.Error);
            }
        }
    }

    private async Task RemoveFile(FileUploadService.UploadedFileInfo file)
    {
        UploadedFiles.Remove(file);
        await UploadedFilesChanged.InvokeAsync(UploadedFiles);
        Snackbar.Add($"Removed {file.OriginalFileName}", Severity.Info);
    }

    private async Task ClearAllFiles()
    {
        UploadedFiles.Clear();
        await UploadedFilesChanged.InvokeAsync(UploadedFiles);
        await (_mudFileUpload?.ClearAsync() ?? Task.CompletedTask);
        Snackbar.Add("All files cleared", Severity.Info);
    }

    private void SetDragClass() 
        => _dragClass = $"{DefaultDragClass} mud-border-primary mud-theme-primary";
    
    private void ClearDragClass() 
        => _dragClass = DefaultDragClass;

    private string FormatFileSize(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB" };
        int counter = 0;
        decimal number = bytes;
        while (Math.Round(number / 1024) >= 1)
        {
            number /= 1024;
            counter++;
        }
        return string.Format("{0:n1}{1}", number, suffixes[counter]);
    }

    private string GetFileIcon(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".txt" or ".md" => Icons.Material.Filled.TextSnippet,
            ".cs" or ".js" or ".ts" or ".py" or ".java" or ".cpp" or ".c" or ".h" or ".php" or ".rb" or ".go" or ".rs" or ".swift" or ".kt" or ".scala" => Icons.Material.Filled.Code,
            ".html" or ".css" => Icons.Material.Filled.Web,
            ".json" or ".xml" => Icons.Material.Filled.DataObject,
            ".sh" or ".bat" => Icons.Material.Filled.Terminal,
            ".sql" => Icons.Material.Filled.Storage,
            _ => Icons.Material.Filled.InsertDriveFile
        };
    }
}