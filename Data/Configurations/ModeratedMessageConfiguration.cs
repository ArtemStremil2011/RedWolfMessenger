using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Messenger.Models.BaseModels;

namespace Messenger.Data.Configurations
{
    public class ModeratedMessageConfiguration : IEntityTypeConfiguration<ModeratedMessage>
    {
        public void Configure(EntityTypeBuilder<ModeratedMessage> builder)
        {
            builder.ToTable("ModeratedMessages");
            
            builder.HasKey(m => m.Id);
            
            builder.Property(m => m.PlainText)
                .IsRequired()
                .HasMaxLength(5000);
            
            builder.Property(m => m.CreatedAt)
                .IsRequired()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            
            // Связь с Message
            builder.HasOne(m => m.Message)
                .WithMany()
                .HasForeignKey(m => m.MessageId)
                .OnDelete(DeleteBehavior.Cascade);
            
            // Связь с Chat
            builder.HasOne(m => m.Chat)
                .WithMany()
                .HasForeignKey(m => m.ChatId)
                .OnDelete(DeleteBehavior.Cascade);
            
            // Связь с User
            builder.HasOne(m => m.User)
                .WithMany()
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            
            // Индексы
            builder.HasIndex(m => m.MessageId);
            builder.HasIndex(m => m.CreatedAt);
            builder.HasIndex(m => m.ChatId);
        }
    }
}