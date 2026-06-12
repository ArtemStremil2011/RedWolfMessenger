using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Messenger.Models.BaseModels;

namespace Messenger.Data.Configurations
{
    public class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.ToTable("Users");
            
            builder.HasKey(u => u.Id);
            
            builder.Property(u => u.Name)
                .IsRequired()
                .HasMaxLength(50);
            
            builder.Property(u => u.PasswordHash)
                .IsRequired()
                .HasMaxLength(100);
            
            builder.Property(u => u.PhoneNumber)
                .IsRequired()
                .HasMaxLength(20);
            
            builder.Property(u => u.AvatarPath)
                .HasMaxLength(500);
            
            builder.Property(u => u.PublicKey)
                .HasMaxLength(500);
            
            builder.Property(u => u.RegisterDate)
                .IsRequired()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            
            builder.Property(u => u.IsPhoneNumberConfirmed)
                .HasDefaultValue(false);
            
            builder.Property(u => u.Role)
                .HasDefaultValue(UserRole.User);
            
            // Индексы
            builder.HasIndex(u => u.Name).IsUnique();
            builder.HasIndex(u => u.PhoneNumber).IsUnique();
        }
    }
}