using Microsoft.EntityFrameworkCore;

namespace DiscordChannelsBot.Models;

public sealed class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
        Database.EnsureCreated();
        Database.Migrate();
    }

    public DbSet<DiscordGuildConfiguration> GuildConfigurations { get; set; }

    public DbSet<UserVoiceChannelName> UserVoiceChannelNames { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<UserVoiceChannelName>()
            .HasKey(name => new {name.GuildId, name.UserId});
    }
}