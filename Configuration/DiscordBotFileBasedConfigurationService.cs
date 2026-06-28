using DiscordChannelsBot.Models;

namespace DiscordChannelsBot.Configuration;

public class DiscordBotFileBasedConfigurationService : IDiscordBotConfigurationService
{
    private readonly ApplicationDbContext _applicationDbContext;

    public DiscordBotFileBasedConfigurationService(ApplicationDbContext applicationDbContext)
    {
        _applicationDbContext = applicationDbContext;
    }

    public async ValueTask<DiscordGuildConfiguration> GetGuildConfigurationAsync(ulong guildId)
    {
        return await _applicationDbContext.GuildConfigurations
            .FindAsync(guildId);
    }

    public async Task UpdateAsync(DiscordGuildConfiguration discordGuildConfiguration)
    {
        _applicationDbContext.GuildConfigurations.Update(discordGuildConfiguration);
        await _applicationDbContext.SaveChangesAsync();
    }

    public async Task SaveAsync(DiscordGuildConfiguration discordGuildConfiguration)
    {
        await _applicationDbContext.GuildConfigurations.AddAsync(discordGuildConfiguration);
        await _applicationDbContext.SaveChangesAsync();
    }

    public async ValueTask<UserVoiceChannelName> GetUserChannelNameAsync(ulong guildId, ulong userId)
    {
        return await _applicationDbContext.UserVoiceChannelNames
            .FindAsync(guildId, userId);
    }

    public async Task SetUserChannelNameAsync(ulong guildId, ulong userId, string name)
    {
        var existing = await _applicationDbContext.UserVoiceChannelNames
            .FindAsync(guildId, userId);

        if (existing == null)
        {
            await _applicationDbContext.UserVoiceChannelNames.AddAsync(new UserVoiceChannelName
            {
                GuildId = guildId,
                UserId = userId,
                Name = name
            });
        }
        else
        {
            existing.Name = name;
            _applicationDbContext.UserVoiceChannelNames.Update(existing);
        }

        await _applicationDbContext.SaveChangesAsync();
    }
}