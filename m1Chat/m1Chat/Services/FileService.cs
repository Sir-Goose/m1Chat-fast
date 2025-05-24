using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using m1Chat.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace m1Chat.Services
{
    public class FileService
    {
        private readonly ChatDbContext _db;
        private readonly IConfiguration _config;
        private readonly string _uploadPath;
        private readonly string _baseUrl;

        public FileService(ChatDbContext db, IConfiguration config)
        {
            _db = db;
            _config = config;
            _uploadPath = _config.GetValue<string>("FileUpload:Path") ?? 
                Path.Combine(Directory.GetCurrentDirectory(), "uploads");
            _baseUrl = _config.GetValue<string>("FileUpload:BaseUrl") ?? 
                "https://localhost:5001";
            
            // Ensure upload directory exists
            Directory.CreateDirectory(_uploadPath);
        }

        public async Task<UploadedFile> UploadFileAsync(IFormFile file, string userEmail)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("No file provided");

            // Validate file is text-based
            if (!IsTextFile(file))
                throw new ArgumentException("Only text files are allowed");

            // Get user
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == userEmail);
            if (user == null)
                throw new ArgumentException("User not found");

            // Generate unique file ID and path
            var fileId = Guid.NewGuid();
            var extension = Path.GetExtension(file.FileName);
            var fileName = $"{fileId}{extension}";
            var filePath = Path.Combine(_uploadPath, fileName);

            // Save file to disk
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Save to database
            var uploadedFile = new UploadedFile
            {
                Id = fileId,
                OriginalFileName = file.FileName,
                ContentType = file.ContentType,
                FileSize = file.Length,
                FilePath = filePath,
                UploadedByUserId = user.Id
            };

            _db.UploadedFiles.Add(uploadedFile);
            await _db.SaveChangesAsync();

            return uploadedFile;
        }

        public async Task<string> GetFileContentAsync(Guid fileId)
        {
            var file = await _db.UploadedFiles.FindAsync(fileId);
            if (file == null || !File.Exists(file.FilePath))
                throw new FileNotFoundException("File not found");

            return await File.ReadAllTextAsync(file.FilePath);
        }

        public async Task<UploadedFile> GetFileAsync(Guid fileId)
        {
            return await _db.UploadedFiles
                .Include(f => f.UploadedBy)
                .FirstOrDefaultAsync(f => f.Id == fileId);
        }

        public async Task<List<UploadedFile>> GetUserFilesAsync(string userEmail)
        {
            return await _db.UploadedFiles
                .Include(f => f.UploadedBy)
                .Where(f => f.UploadedBy.Email == userEmail)
                .OrderByDescending(f => f.UploadedAt)
                .ToListAsync();
        }

        public async Task AttachFilesToMessageAsync(Guid chatId, int messageIndex, List<Guid> fileIds)
        {
            var chat = await _db.Chats.FindAsync(chatId);
            if (chat == null)
                throw new ArgumentException("Chat not found");

            // Remove existing attachments for this message
            var existing = await _db.ChatMessageFiles
                .Where(cmf => cmf.ChatId == chatId && cmf.MessageIndex == messageIndex)
                .ToListAsync();
            _db.ChatMessageFiles.RemoveRange(existing);

            // Add new attachments
            foreach (var fileId in fileIds)
            {
                var messageFile = new ChatMessageFile
                {
                    ChatId = chatId,
                    MessageIndex = messageIndex,
                    FileId = fileId
                };
                _db.ChatMessageFiles.Add(messageFile);
            }

            await _db.SaveChangesAsync();
        }

        public async Task<List<UploadedFile>> GetMessageFilesAsync(Guid chatId, int messageIndex)
        {
            return await _db.ChatMessageFiles
                .Where(cmf => cmf.ChatId == chatId && cmf.MessageIndex == messageIndex)
                .Include(cmf => cmf.File)
                .Select(cmf => cmf.File)
                .ToListAsync();
        }

        public string GetFileUrl(Guid fileId)
        {
            return $"{_baseUrl}/api/files/{fileId}";
        }

        private bool IsTextFile(IFormFile file)
        {
            // Check content type
            if (file.ContentType.StartsWith("text/"))
                return true;

            // Check common code file extensions
            var allowedExtensions = new[]
            {
                ".txt", ".cs", ".js", ".ts", ".html", ".css", ".scss", ".less",
                ".json", ".xml", ".yaml", ".yml", ".md", ".py", ".java", ".cpp",
                ".c", ".h", ".hpp", ".php", ".rb", ".go", ".rs", ".swift",
                ".kt", ".scala", ".sh", ".bat", ".ps1", ".sql", ".r", ".m",
                ".vue", ".jsx", ".tsx", ".svelte", ".razor", ".cshtml",
                ".config", ".conf", ".ini", ".properties", ".env", ".log"
            };

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            return allowedExtensions.Contains(extension);
        }

        public async Task DeleteFileAsync(Guid fileId, string userEmail)
        {
            var file = await _db.UploadedFiles
                .Include(f => f.UploadedBy)
                .FirstOrDefaultAsync(f => f.Id == fileId);

            if (file == null)
                throw new FileNotFoundException("File not found");

            if (file.UploadedBy.Email != userEmail)
                throw new UnauthorizedAccessException("You can only delete your own files");

            // Remove from filesystem
            if (File.Exists(file.FilePath))
                File.Delete(file.FilePath);

            // Remove from database (cascade will handle ChatMessageFiles)
            _db.UploadedFiles.Remove(file);
            await _db.SaveChangesAsync();
        }
    }
}