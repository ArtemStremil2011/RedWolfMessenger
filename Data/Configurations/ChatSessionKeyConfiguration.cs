using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Messenger.Models.ChatModels;

namespace Messenger.Data.Configurations
{
    public class ChatSessionKeyConfiguration : IEntityTypeConfiguration<ChatSessionKey>
    {
        public void Configure(EntityTypeBuilder<ChatSessionKey> builder)
        {
            builder.ToTable("ChatSessionKeys");
            
            builder.HasKey(k => k.Id);
            
            builder.Property(k => k.EncryptedSessionKey)
                .IsRequired()
                .HasMaxLength(1000);
            
            builder.Property(k => k.CreatedAt)
                .IsRequired()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            
            builder.HasOne(k => k.Chat)
                .WithMany()
                .HasForeignKey(k => k.ChatId)
                .OnDelete(DeleteBehavior.Cascade);
            
            builder.HasOne(k => k.User)
                .WithMany()
                .HasForeignKey(k => k.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            
            // Уникальный индекс на пару ChatId + UserId
            builder.HasIndex(k => new { k.ChatId, k.UserId }).IsUnique();
        }
    }
}