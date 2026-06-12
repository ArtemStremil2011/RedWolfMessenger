using Microsoft.EntityFrameworkCore;
using Messenger.Models.BaseModels;
using Messenger.Models.ChatModels;
using Messenger.Data.Configurations;

namespace Messenger.Data
{
    public class AppDBContext : DbContext
    {
        public AppDBContext(DbContextOptions<AppDBContext> options) : base(options) { }
        
        public DbSet<User> Users { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<FileMessage> FileMessages { get; set; }
        public DbSet<Chat> Chats { get; set; }
        public DbSet<ChatSessionKey> ChatSessionKeys { get; set; }  // ← ЭТО ДОЛЖНО БЫТЬ
        
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new UserConfiguration());
            modelBuilder.ApplyConfiguration(new ChatConfiguration());
            modelBuilder.ApplyConfiguration(new MessageConfiguration());
            modelBuilder.ApplyConfiguration(new FileMessageConfiguration());
            modelBuilder.ApplyConfiguration(new ChatSessionKeyConfiguration());
            
            base.OnModelCreating(modelBuilder);
        }
    }
}