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

        // Foreign key
        [Required]
        public Guid UserId { get; set; }
        public User User { get; set; }
    }
}