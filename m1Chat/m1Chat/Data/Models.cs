using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace m1Chat.Data
{
    public class User
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid(); // UUID v4

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        public ICollection<Chat> Chats { get; set; } = new List<Chat>();
    }

    public class Chat
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string Model { get; set; } = string.Empty;

        [Required]
        public string HistoryJson { get; set; } = string.Empty; // Store chat history as JSON

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;

        public bool IsPinned { get; set; } = false;

        // Foreign key
        [Required]
        public Guid UserId { get; set; }
        public User User { get; set; } = default!;

        public ICollection<ChatMessageFile> MessageFiles { get; set; } = new List<ChatMessageFile>();
    }

    public class UploadedFile
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public string OriginalFileName { get; set; } = string.Empty;

        [Required]
        public string ContentType { get; set; } = string.Empty;

        [Required]
        public long FileSize { get; set; }

        [Required]
        public string FilePath { get; set; } = string.Empty; // Physical path on server

        [Required]
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        [Required]
        public Guid UploadedByUserId { get; set; }
        public User UploadedBy { get; set; } = default!;

        public ICollection<ChatMessageFile> ChatMessageFiles { get; set; } = new List<ChatMessageFile>();
    }

    public class ChatMessageFile
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid ChatId { get; set; }
        public Chat Chat { get; set; } = default!;

        [Required]
        public int MessageIndex { get; set; } // Which message in the chat

        [Required]
        public Guid FileId { get; set; }
        public UploadedFile File { get; set; } = default!;

        [Required]
        public DateTime AttachedAt { get; set; } = DateTime.UtcNow;
    }
    
    public class UserApiKey
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid UserId { get; set; }
        public User User { get; set; } = default!;

        [Required]
        [MaxLength(50)]
        public string Provider { get; set; } = string.Empty; // "OpenRouter", "AIStudio", "Chutes", "Mistral"

        [Required]
        [MaxLength(256)]
        public string ApiKey { get; set; } = string.Empty;
    }

}
