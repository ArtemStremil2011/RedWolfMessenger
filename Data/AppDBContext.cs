using Messenger.Models.BaseModels;
using Messenger.Models.ChatModels;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;

namespace Messenger.Data
{
    public class AppDBContext : DbContext
    {
        public AppDBContext(DbContextOptions<AppDBContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<Chat> Chats { get; set; }
        public DbSet<FileMessage> FileMessages { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // ========== USER ==========
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(u => u.Id);
                entity.Property(u => u.Name).IsRequired().HasMaxLength(50);
                entity.Property(u => u.PasswordHash).IsRequired().HasMaxLength(100);
                entity.Property(u => u.PhoneNumber).HasMaxLength(20);
                entity.Property(u => u.AvatarPath).HasMaxLength(500);
                entity.Property(u => u.RegisterDate).IsRequired();
                entity.Property(u => u.IsPhoneNumberConfirmed).HasDefaultValue(false);
                entity.Property(u => u.Role).HasDefaultValue(UserRole.User);

                entity.HasIndex(u => u.Name).IsUnique();
                entity.HasIndex(u => u.PhoneNumber).IsUnique();
            });

            // ========== MESSAGE (только текстовые) ==========
            modelBuilder.Entity<Message>(entity =>
            {
                entity.ToTable("Messages");
                entity.HasKey(m => m.MessageId);
                entity.Property(m => m.MessageText).IsRequired().HasMaxLength(5000);
                entity.Property(m => m.MessageCreateDate).IsRequired();
                entity.Property(m => m.MessageLastUpdateDate).IsRequired();
                entity.Property(m => m.IsDeleted).HasDefaultValue(false);
                entity.Property(m => m.IsSystemMessage).HasDefaultValue(false);

                entity.HasOne(m => m.MessageCreator)
                    .WithMany(u => u.Messages)
                    .HasForeignKey(m => m.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(m => m.Chat)
                    .WithMany(c => c.MessagesHistory)
                    .HasForeignKey(m => m.ChatId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(m => m.MessageCreateDate);
                entity.HasIndex(m => m.UserId);
                entity.HasIndex(m => m.ChatId);
            });

            // ========== FILEMESSAGE (полностью отдельная таблица) ==========
            modelBuilder.Entity<FileMessage>(entity =>
            {
                entity.ToTable("FileMessages");
                entity.HasKey(f => f.MessageId);
                entity.Property(f => f.MessageText).IsRequired().HasMaxLength(5000);
                entity.Property(f => f.MessageCreateDate).IsRequired();
                entity.Property(f => f.MessageLastUpdateDate).IsRequired();
                entity.Property(f => f.IsDeleted).HasDefaultValue(false);
                entity.Property(f => f.IsSystemMessage).HasDefaultValue(false);

                entity.Property(f => f.FileName).IsRequired().HasMaxLength(255);
                entity.Property(f => f.FilePath).IsRequired().HasMaxLength(500);
                entity.Property(f => f.FileSize).IsRequired();
                entity.Property(f => f.ContentType).IsRequired().HasMaxLength(100);

                entity.HasOne(f => f.MessageCreator)
                    .WithMany()
                    .HasForeignKey(f => f.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(f => f.Chat)
                    .WithMany()
                    .HasForeignKey(f => f.ChatId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(f => f.MessageCreateDate);
                entity.HasIndex(f => f.UserId);
                entity.HasIndex(f => f.ChatId);
            });

            // ========== CHAT ==========
            modelBuilder.Entity<Chat>(entity =>
            {
                entity.ToTable("Chats");
                entity.HasKey(c => c.Id);
                entity.Property(c => c.ChatName).IsRequired().HasMaxLength(100);
                entity.Property(c => c.MaxUsers).HasDefaultValue(2);
                entity.Property(c => c.IsPrivate).HasDefaultValue(true);
                entity.Property(c => c.CreatedAt).IsRequired();
                entity.Property(c => c.LastActivityAt).IsRequired(false);

                entity.HasOne(c => c.CreatedBy)
                    .WithMany()
                    .HasForeignKey(c => c.CreatedById)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasMany(c => c.Users)
                    .WithMany(u => u.Chats)
                    .UsingEntity<Dictionary<string, object>>(
                        "ChatUsers",
                        j => j.HasOne<User>().WithMany().HasForeignKey("UserId").OnDelete(DeleteBehavior.Cascade),
                        j => j.HasOne<Chat>().WithMany().HasForeignKey("ChatId").OnDelete(DeleteBehavior.Cascade)
                    );

                entity.HasMany(c => c.MessagesHistory)
                    .WithOne(m => m.Chat)
                    .HasForeignKey(m => m.ChatId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            base.OnModelCreating(modelBuilder);
        }
    }
}