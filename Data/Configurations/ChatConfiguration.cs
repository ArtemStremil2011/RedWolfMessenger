using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Messenger.Models.ChatModels;
using Messenger.Models.BaseModels;

namespace Messenger.Data.Configurations
{
    public class ChatConfiguration : IEntityTypeConfiguration<Chat>
    {
        public void Configure(EntityTypeBuilder<Chat> builder)
        {
            builder.ToTable("Chats");
            
            builder.HasKey(c => c.Id);
            
            builder.Property(c => c.ChatName)
                .IsRequired()
                .HasMaxLength(100);
            
            builder.Property(c => c.AvatarPath)
                .HasMaxLength(500);
            
            builder.Property(c => c.MaxUsers)
                .HasDefaultValue(2);
            
            builder.Property(c => c.IsPrivate)
                .HasDefaultValue(true);
            
            builder.Property(c => c.CreatedAt)
                .IsRequired()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            
            builder.Property(c => c.LastActivityAt)
                .IsRequired(false);
            
            // Связь с создателем
            builder.HasOne(c => c.CreatedBy)
                .WithMany()
                .HasForeignKey(c => c.CreatedById)
                .OnDelete(DeleteBehavior.SetNull);
            
            // Many-to-many с пользователями
            builder.HasMany(c => c.Users)
                .WithMany(u => u.Chats)
                .UsingEntity<Dictionary<string, object>>(
                    "ChatUsers",
                    j => j.HasOne<User>().WithMany().HasForeignKey("UserId").OnDelete(DeleteBehavior.Cascade),
                    j => j.HasOne<Chat>().WithMany().HasForeignKey("ChatId").OnDelete(DeleteBehavior.Cascade),
                    j => j.HasKey("UserId", "ChatId")
                );
            
            // Индексы
            builder.HasIndex(c => c.CreatedAt);
            builder.HasIndex(c => c.LastActivityAt);
        }
    }
}