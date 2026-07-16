using Microsoft.EntityFrameworkCore;
using InvestigationTeam.Chat.Api.Models;

namespace InvestigationTeam.Chat.Api.Data;

public class ChatDbContext : DbContext
{
    public ChatDbContext(DbContextOptions<ChatDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.Email).HasMaxLength(255);
        });

        modelBuilder.Entity<ChatSession>(e =>
        {
            e.HasIndex(s => new { s.UserId, s.CreatedAt });
        });

        modelBuilder.Entity<ChatMessage>(e =>
        {
            e.HasIndex(m => new { m.SessionId, m.CreatedAt });
        });
    }
}
