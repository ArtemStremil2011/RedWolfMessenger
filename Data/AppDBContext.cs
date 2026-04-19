using Messenger.Models.BaseModels;
using Messenger.Models.ChatModels;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;

namespace Messenger.Data
{
    public class AppDBContext : DbContext
    {
        // Конструктор для приёма настроек из Program.cs
        public AppDBContext(DbContextOptions<AppDBContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<Chat> Chats { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Конфигурация модели User
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(u => u.Id);
                entity.Property(u => u.Name).IsRequired().HasMaxLength(50);
                entity.Property(u => u.PasswordHash).IsRequired().HasMaxLength(100);
                entity.Property(u => u.PhoneNumber).HasMaxLength(20);
                entity.Property(u => u.AvatarPath).HasMaxLength(500);
                entity.HasIndex(u => u.Name).IsUnique();
                entity.HasIndex(u => u.PhoneNumber).IsUnique();
            });

            // Конфигурация модели Message
            modelBuilder.Entity<Message>(entity =>
            {
                entity.HasKey(m => m.MessageId);
                entity.Property(m => m.MessageText).IsRequired().HasMaxLength(5000);
                entity.Property(m => m.MessageCreateDate).IsRequired();
                entity.Property(m => m.IsDeleted).HasDefaultValue(false);

                entity.HasOne(m => m.MessageCreator)
                    .WithMany(u => u.Messages)
                    .HasForeignKey(m => m.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(m => m.MessageCreateDate);
                entity.HasIndex(m => m.UserId);
                entity.HasIndex(m => m.ChatId);
            });

            // Конфигурация модели Chat
            modelBuilder.Entity<Chat>(entity =>
            {
                entity.HasKey(c => c.Id);
                entity.Property(c => c.ChatName).IsRequired().HasMaxLength(100);
                entity.Property(c => c.MaxUsers).HasDefaultValue(2);
                entity.Property(c => c.IsPrivate).HasDefaultValue(true);
                entity.Property(c => c.CreatedAt).IsRequired();

                // Связь создателя чата
                entity.HasOne(c => c.CreatedBy)
                    .WithMany()
                    .HasForeignKey(c => c.CreatedById)
                    .OnDelete(DeleteBehavior.SetNull);

                // Конфигурация связи «многие-ко-многим» с явным указанием внешних ключей
                entity.HasMany(c => c.Users)
                    .WithMany(u => u.Chats)
                    .UsingEntity<Dictionary<string, object>>(
                        "ChatUsers",
                        j => j.HasOne<User>().WithMany().HasForeignKey("UserId"),
                        j => j.HasOne<Chat>().WithMany().HasForeignKey("ChatId")
                    );

                // Связь сообщений с чатом
                entity.HasMany(c => c.MessagesHistory)
                    .WithOne(m => m.Chat)
                    .HasForeignKey(m => m.ChatId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Ограничение на количество пользователей в чате
                entity.ToTable(t => t.HasCheckConstraint("CK_Chat_MaxUsers", "[MaxUsers] = 2"));
            });

            base.OnModelCreating(modelBuilder);
        }
    }
}