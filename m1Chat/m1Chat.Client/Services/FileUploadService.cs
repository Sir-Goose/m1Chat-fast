using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Forms;

namespace m1Chat.Client.Services
{
    public class FileUploadService
    {
        private readonly HttpClient _http;

        public FileUploadService(HttpClient http)
        {
            _http = http;
        }

        public async Task<UploadedFileInfo> UploadFileAsync(IBrowserFile file)
        {
            using var content = new MultipartFormDataContent();
            using var fileStream = file.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024); // 10MB
            using var streamContent = new StreamContent(fileStream);
            
            content.Add(streamContent, "file", file.Name);

            var response = await _http.PostAsync("api/files/upload", content);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Upload failed: {error}");
            }

            return await response.Content.ReadFromJsonAsync<UploadedFileInfo>();
        }

        public async Task<List<UploadedFileInfo>> GetUserFilesAsync()
        {
            return await _http.GetFromJsonAsync<List<UploadedFileInfo>>("api/files") ?? new();
        }

        public async Task DeleteFileAsync(Guid fileId)
        {
            var response = await _http.DeleteAsync($"api/files/{fileId}");
            response.EnsureSuccessStatusCode();
        }

        public class UploadedFileInfo
        {
            public Guid Id { get; set; }
            public string OriginalFileName { get; set; }
            public long FileSize { get; set; }
            public DateTime UploadedAt { get; set; }
            public string Url { get; set; }
        }
    }
}