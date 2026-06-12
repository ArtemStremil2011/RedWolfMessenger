using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Messenger.Models.BaseModels;

namespace Messenger.Data.Configurations
{
    public class MessageConfiguration : IEntityTypeConfiguration<Message>
    {
        public void Configure(EntityTypeBuilder<Message> builder)
        {
            builder.ToTable("Messages");
            
            builder.HasKey(m => m.MessageId);
            
            builder.Property(m => m.MessageText)
                .HasMaxLength(5000);  // Теперь необязательное (может быть null)
            
            builder.Property(m => m.EncryptedData)
                .HasMaxLength(5000);
            
            builder.Property(m => m.Iv)
                .HasMaxLength(50);
            
            builder.Property(m => m.MessageCreateDate)
                .IsRequired()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            
            builder.Property(m => m.MessageLastUpdateDate)
                .IsRequired()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            
            builder.Property(m => m.IsDeleted)
                .HasDefaultValue(false);
            
            builder.Property(m => m.IsSystemMessage)
                .HasDefaultValue(false);
            
            builder.Property(m => m.IsRead)
                .HasDefaultValue(false);
            
            // Связь с автором
            builder.HasOne(m => m.MessageCreator)
                .WithMany(u => u.Messages)
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            
            // Связь с чатом
            builder.HasOne(m => m.Chat)
                .WithMany(c => c.MessagesHistory)
                .HasForeignKey(m => m.ChatId)
                .OnDelete(DeleteBehavior.Cascade);
            
            // Индексы
            builder.HasIndex(m => m.MessageCreateDate);
            builder.HasIndex(m => m.UserId);
            builder.HasIndex(m => m.ChatId);
            builder.HasIndex(m => m.IsDeleted);
        }
    }
}