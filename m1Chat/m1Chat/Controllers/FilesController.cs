using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using m1Chat.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace m1Chat.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class FilesController : ControllerBase
    {
        private readonly FileService _fileService;

        public FilesController(FileService fileService)
        {
            _fileService = fileService;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadFile(IFormFile file)
        {
            try
            {
                var email = User.FindFirst(ClaimTypes.Email)?.Value;
                if (email == null) return Unauthorized();

                var uploadedFile = await _fileService.UploadFileAsync(file, email);
                
                return Ok(new UploadFileResponse
                {
                    Id = uploadedFile.Id,
                    OriginalFileName = uploadedFile.OriginalFileName,
                    FileSize = uploadedFile.FileSize,
                    UploadedAt = uploadedFile.UploadedAt,
                    Url = _fileService.GetFileUrl(uploadedFile.Id)
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { error = "An error occurred while uploading the file" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetUserFiles()
        {
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            if (email == null) return Unauthorized();

            var files = await _fileService.GetUserFilesAsync(email);
            var response = files.Select(f => new FileInfoResponse
            {
                Id = f.Id,
                OriginalFileName = f.OriginalFileName,
                FileSize = f.FileSize,
                UploadedAt = f.UploadedAt,
                Url = _fileService.GetFileUrl(f.Id)
            }).ToList();

            return Ok(response);
        }

        [HttpGet("{id:guid}")]
        [AllowAnonymous] // Public access for file content
        public async Task<IActionResult> GetFile(Guid id)
        {
            try
            {
                var file = await _fileService.GetFileAsync(id);
                if (file == null)
                    return NotFound();

                var content = await _fileService.GetFileContentAsync(id);
                return File(System.Text.Encoding.UTF8.GetBytes(content), 
                    file.ContentType, file.OriginalFileName);
            }
            catch (FileNotFoundException)
            {
                return NotFound();
            }
        }

        [HttpGet("{id:guid}/content")]
        [AllowAnonymous] // Public access for raw content
        public async Task<IActionResult> GetFileContent(Guid id)
        {
            try
            {
                var content = await _fileService.GetFileContentAsync(id);
                return Ok(new { content });
            }
            catch (FileNotFoundException)
            {
                return NotFound();
            }
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeleteFile(Guid id)
        {
            try
            {
                var email = User.FindFirst(ClaimTypes.Email)?.Value;
                if (email == null) return Unauthorized();

                await _fileService.DeleteFileAsync(id, email);
                return NoContent();
            }
            catch (FileNotFoundException)
            {
                return NotFound();
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        // DTOs
        public class UploadFileResponse
        {
            public Guid Id { get; set; }
            public string OriginalFileName { get; set; }
            public long FileSize { get; set; }
            public DateTime UploadedAt { get; set; }
            public string Url { get; set; }
        }

        public class FileInfoResponse
        {
            public Guid Id { get; set; }
            public string OriginalFileName { get; set; }
            public long FileSize { get; set; }
            public DateTime UploadedAt { get; set; }
            public string Url { get; set; }
        }
    }
}