using Messenger.Models.BaseModels;
using Messenger.Models.ChatModels;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;

namespace Messenger.Data
{
    public class AppDBContext : DbContext
    {
        // Конструктор для миграций
        public AppDBContext() { }

        // Конструктор для DI
        public AppDBContext(DbContextOptions<AppDBContext> options) : base(options) { }

        // DbSet для всех таблиц
        public DbSet<User> Users { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<FileMessage> FileMessages { get; set; }
        public DbSet<Chat> Chats { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=RedWolfMessenger;Username=postgres;Password=root");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // =========================================================================
            // ТАБЛИЦА USERS (Пользователи)
            // =========================================================================
            modelBuilder.Entity<User>(entity =>
            {
                // Первичный ключ
                entity.HasKey(u => u.Id);

                // Поля
                entity.Property(u => u.Name)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(u => u.PasswordHash)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(u => u.PhoneNumber)
                    .IsRequired(false)
                    .HasMaxLength(20);

                entity.Property(u => u.AvatarPath)
                    .IsRequired(false)
                    .HasMaxLength(500);

                entity.Property(u => u.RegisterDate)
                    .IsRequired()
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(u => u.IsPhoneNumberConfirmed)
                    .IsRequired()
                    .HasDefaultValue(false);

                entity.Property(u => u.PhoneVerificationCode)
                    .IsRequired(false)
                    .HasMaxLength(10);

                entity.Property(u => u.VerificationCodeExpiry)
                    .IsRequired(false);

                entity.Property(u => u.Role)
                    .IsRequired()
                    .HasDefaultValue(UserRole.User);

                // Индексы (уникальные)
                entity.HasIndex(u => u.Name).IsUnique();
                entity.HasIndex(u => u.PhoneNumber).IsUnique();
            });

            // =========================================================================
            // ТАБЛИЦА CHATS (Чаты и группы)
            // =========================================================================
            modelBuilder.Entity<Chat>(entity =>
            {
                // Первичный ключ
                entity.HasKey(c => c.Id);

                // Поля
                entity.Property(c => c.ChatName)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(c => c.AvatarPath)
                    .IsRequired(false)
                    .HasMaxLength(500);

                entity.Property(c => c.MaxUsers)
                    .IsRequired()
                    .HasDefaultValue(2);

                entity.Property(c => c.IsPrivate)
                    .IsRequired()
                    .HasDefaultValue(true);

                entity.Property(c => c.CreatedAt)
                    .IsRequired()
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(c => c.LastActivityAt)
                    .IsRequired(false);

                entity.Property(c => c.CreatedById)
                    .IsRequired(false);

                // Связь с создателем чата
                entity.HasOne(c => c.CreatedBy)
                    .WithMany()
                    .HasForeignKey(c => c.CreatedById)
                    .OnDelete(DeleteBehavior.SetNull);

                // Индексы
                entity.HasIndex(c => c.CreatedAt);
                entity.HasIndex(c => c.LastActivityAt);
            });

            // =========================================================================
            // ТАБЛИЦА CHATUSERS (Связь многие-ко-многим: Чат - Пользователь)
            // =========================================================================
            modelBuilder.Entity<Chat>()
                .HasMany(c => c.Users)
                .WithMany(u => u.Chats)
                .UsingEntity<Dictionary<string, object>>(
                    "ChatUsers",
                    j => j.HasOne<User>().WithMany().HasForeignKey("UserId").OnDelete(DeleteBehavior.Cascade),
                    j => j.HasOne<Chat>().WithMany().HasForeignKey("ChatId").OnDelete(DeleteBehavior.Cascade),
                    j =>
                    {
                        j.HasKey("UserId", "ChatId");
                        j.ToTable("ChatUsers");
                    }
                );

            // =========================================================================
            // ТАБЛИЦА MESSAGES (Текстовые сообщения)
            // =========================================================================
            modelBuilder.Entity<Message>(entity =>
            {
                // Первичный ключ
                entity.HasKey(m => m.MessageId);

                // Поля
                entity.Property(m => m.MessageText)
                    .IsRequired()
                    .HasMaxLength(5000);

                entity.Property(m => m.MessageCreateDate)
                    .IsRequired()
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(m => m.MessageLastUpdateDate)
                    .IsRequired()
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(m => m.UserId)
                    .IsRequired();

                entity.Property(m => m.ChatId)
                    .IsRequired();

                entity.Property(m => m.IsDeleted)
                    .IsRequired()
                    .HasDefaultValue(false);

                entity.Property(m => m.IsSystemMessage)
                    .IsRequired()
                    .HasDefaultValue(false);

                // Связь с пользователем (автор)
                entity.HasOne(m => m.MessageCreator)
                    .WithMany(u => u.Messages)
                    .HasForeignKey(m => m.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Связь с чатом
                entity.HasOne(m => m.Chat)
                    .WithMany(c => c.MessagesHistory)
                    .HasForeignKey(m => m.ChatId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Индексы
                entity.HasIndex(m => m.MessageCreateDate);
                entity.HasIndex(m => m.UserId);
                entity.HasIndex(m => m.ChatId);
                entity.HasIndex(m => m.IsDeleted);
            });

            // =========================================================================
            // ТАБЛИЦА FILEMESSAGES (Файловые сообщения)
            // =========================================================================
            modelBuilder.Entity<FileMessage>(entity =>
            {
                // Первичный ключ
                entity.HasKey(f => f.MessageId);

                // Поля (наследуемые от Message)
                entity.Property(f => f.MessageText)
                    .IsRequired()
                    .HasMaxLength(5000);

                entity.Property(f => f.MessageCreateDate)
                    .IsRequired()
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(f => f.MessageLastUpdateDate)
                    .IsRequired()
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(f => f.UserId)
                    .IsRequired();

                entity.Property(f => f.ChatId)
                    .IsRequired();

                entity.Property(f => f.IsDeleted)
                    .IsRequired()
                    .HasDefaultValue(false);

                entity.Property(f => f.IsSystemMessage)
                    .IsRequired()
                    .HasDefaultValue(false);

                // Специфичные поля для файлов
                entity.Property(f => f.FileName)
                    .IsRequired()
                    .HasMaxLength(255);

                entity.Property(f => f.FilePath)
                    .IsRequired()
                    .HasMaxLength(500);

                entity.Property(f => f.FileSize)
                    .IsRequired();

                entity.Property(f => f.ContentType)
                    .IsRequired()
                    .HasMaxLength(100);

                // Связь с пользователем (автор)
                entity.HasOne(f => f.MessageCreator)
                    .WithMany()
                    .HasForeignKey(f => f.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Связь с чатом
                entity.HasOne(f => f.Chat)
                    .WithMany()
                    .HasForeignKey(f => f.ChatId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Индексы
                entity.HasIndex(f => f.MessageCreateDate);
                entity.HasIndex(f => f.UserId);
                entity.HasIndex(f => f.ChatId);
                entity.HasIndex(f => f.IsDeleted);
            });

            base.OnModelCreating(modelBuilder);
        }
    }
}