using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Messenger.Models.ChatModels;

namespace Messenger.Data.Configurations
{
    public class FileMessageConfiguration : IEntityTypeConfiguration<FileMessage>
    {
        public void Configure(EntityTypeBuilder<FileMessage> builder)
        {
            builder.ToTable("FileMessages");

            builder.HasKey(f => f.MessageId);

            builder.Property(f => f.MessageText)
                .HasMaxLength(5000);

            builder.Property(f => f.EncryptedData)
                .HasMaxLength(5000);

            builder.Property(f => f.Iv)
                .HasMaxLength(50);

            builder.Property(f => f.MessageCreateDate)
                .IsRequired()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            builder.Property(f => f.MessageLastUpdateDate)
                .IsRequired()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            builder.Property(f => f.IsDeleted)
                .HasDefaultValue(false);

            builder.Property(f => f.IsSystemMessage)
                .HasDefaultValue(false);

            builder.Property(f => f.IsRead)
                .HasDefaultValue(false);

            // ===== ПОЛЯ ДЛЯ ФАЙЛОВ =====
            builder.Property(f => f.FileName)
                .IsRequired()
                .HasMaxLength(255);

            builder.Property(f => f.FilePath)
                .IsRequired()
                .HasMaxLength(500);

            builder.Property(f => f.FileSize)
                .IsRequired();

            builder.Property(f => f.ContentType)
                .IsRequired()
                .HasMaxLength(100);

            // ===== НОВЫЕ ПОЛЯ =====
            builder.Property(f => f.Duration)
                .IsRequired(false); // Только для голосовых

            builder.Property(f => f.MessageType)
                .IsRequired()
                .HasMaxLength(20)
                .HasDefaultValue("file");

            // ===== СВЯЗИ =====
            builder.HasOne(f => f.MessageCreator)
                .WithMany()
                .HasForeignKey(f => f.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(f => f.Chat)
                .WithMany()
                .HasForeignKey(f => f.ChatId)
                .OnDelete(DeleteBehavior.Cascade);

            // ===== ИНДЕКСЫ =====
            builder.HasIndex(f => f.MessageCreateDate);
            builder.HasIndex(f => f.UserId);
            builder.HasIndex(f => f.ChatId);
            builder.HasIndex(f => f.IsDeleted);
            builder.HasIndex(f => f.MessageType); // ← Новый индекс
        }
    }
}