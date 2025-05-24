using Microsoft.EntityFrameworkCore;

namespace m1Chat.Data
{
    public class ChatDbContext : DbContext
    {
        public ChatDbContext(DbContextOptions<ChatDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Chat> Chats { get; set; }
        public DbSet<UploadedFile> UploadedFiles { get; set; }
        public DbSet<ChatMessageFile> ChatMessageFiles { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasMany(u => u.Chats)
                .WithOne(c => c.User)
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure UploadedFile relationships
            modelBuilder.Entity<UploadedFile>()
                .HasOne(f => f.UploadedBy)
                .WithMany()
                .HasForeignKey(f => f.UploadedByUserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure ChatMessageFile relationships
            modelBuilder.Entity<ChatMessageFile>()
                .HasOne(cmf => cmf.Chat)
                .WithMany(c => c.MessageFiles)
                .HasForeignKey(cmf => cmf.ChatId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ChatMessageFile>()
                .HasOne(cmf => cmf.File)
                .WithMany(f => f.ChatMessageFiles)
                .HasForeignKey(cmf => cmf.FileId)
                .OnDelete(DeleteBehavior.Cascade);

            // Ensure unique constraint on chat + message index + file
            modelBuilder.Entity<ChatMessageFile>()
                .HasIndex(cmf => new { cmf.ChatId, cmf.MessageIndex, cmf.FileId })
                .IsUnique();
        }
    }
}