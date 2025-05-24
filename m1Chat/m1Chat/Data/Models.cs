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
        public string Email { get; set; }

        public ICollection<Chat> Chats { get; set; }
    }

    public class Chat
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public string Name { get; set; }

        [Required]
        public string Model { get; set; }

        [Required]
        public string HistoryJson { get; set; } // Store chat history as JSON

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;

        public bool IsPinned { get; set; } = false;

        // Foreign key
        [Required]
        public Guid UserId { get; set; }
        public User User { get; set; }

        public ICollection<ChatMessageFile> MessageFiles { get; set; }
    }

    public class UploadedFile
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public string OriginalFileName { get; set; }

        [Required]
        public string ContentType { get; set; }

        [Required]
        public long FileSize { get; set; }

        [Required]
        public string FilePath { get; set; } // Physical path on server

        [Required]
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        [Required]
        public Guid UploadedByUserId { get; set; }
        public User UploadedBy { get; set; }

        public ICollection<ChatMessageFile> ChatMessageFiles { get; set; }
    }

    public class ChatMessageFile
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid ChatId { get; set; }
        public Chat Chat { get; set; }

        [Required]
        public int MessageIndex { get; set; } // Which message in the chat

        [Required]
        public Guid FileId { get; set; }
        public UploadedFile File { get; set; }

        [Required]
        public DateTime AttachedAt { get; set; } = DateTime.UtcNow;
    }
}