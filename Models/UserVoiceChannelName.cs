namespace DiscordChannelsBot.Models;

public class UserVoiceChannelName
{
    public ulong GuildId { get; init; }

    public ulong UserId { get; init; }

    public string Name { get; set; }
}
