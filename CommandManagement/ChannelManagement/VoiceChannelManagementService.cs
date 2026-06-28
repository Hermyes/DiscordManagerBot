using Discord;
using Discord.WebSocket;
using DiscordChannelsBot.Common;
using DiscordChannelsBot.Configuration;
using DiscordChannelsBot.Models;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordChannelsBot.CommandManagement.ChannelManagement;

public class VoiceChannelManagementService : IVoiceChannelManagementService
{
    private readonly IDiscordBotConfigurationService _discordBotConfigurationService;
    private readonly DiscordSocketClient _discordClient;

    private readonly Dictionary<ulong, CancellationTokenSource>
        _voiceChannelDeletionCheckCancellationTokenSourcesDictionary;

    private readonly HashSet<ulong> _managedVoiceChannels;

    public VoiceChannelManagementService(IServiceProvider serviceProvider)
    {
        _voiceChannelDeletionCheckCancellationTokenSourcesDictionary =
            new Dictionary<ulong, CancellationTokenSource>();
        _managedVoiceChannels = new HashSet<ulong>();
        _discordBotConfigurationService = serviceProvider.GetRequiredService<IDiscordBotConfigurationService>();
        _discordClient = serviceProvider.GetRequiredService<DiscordSocketClient>();

        _discordClient.UserVoiceStateUpdated += UserVoiceStateUpdatedHandleAsync;
    }

    public async Task CreateVoiceChannelAsync(IGuild guild, string name, GuildGroupsContext guildGroupsContext)
    {
        var guildConfiguration = await _discordBotConfigurationService.GetGuildConfigurationAsync(guild.Id);
        if (guildConfiguration == null)
        {
            throw new ArgumentException("Не указана категория для создания голосовых каналов!");
        }

        var categoryChannel =
            await DiscordBotUtils.GetCategoryAsync(guild, guildConfiguration.VoiceChannelCreationCategory);

        Action<VoiceChannelProperties> voiceChannelProperties = _ => { };
        if (categoryChannel != null)
        {
            voiceChannelProperties += channel => channel.CategoryId = categoryChannel.Id;
        }

        var voiceChannel = await guild.CreateVoiceChannelAsync(name, voiceChannelProperties);

        if (guildGroupsContext != null)
        {
            if (guildGroupsContext.Roles != null && guildGroupsContext.Roles.Any() ||
                guildGroupsContext.Users != null && guildGroupsContext.Users.Any())
            {
                await AllowOnlyRolesAsync(guild, voiceChannel, guildGroupsContext);
            }
        }

        _managedVoiceChannels.Add(voiceChannel.Id);
        RunDeletionCheckAsync(voiceChannel.Id);
    }

    private async Task AllowOnlyRolesAsync(IGuild guild, IVoiceChannel voiceChannel,
        GuildGroupsContext guildGroupsContext)
    {
        var rolePermissions = new OverwritePermissions().Modify(viewChannel: PermValue.Allow,
            connect: PermValue.Allow,
            speak: PermValue.Allow);

        var denyPermissions = new OverwritePermissions().Modify(viewChannel: PermValue.Deny,
            connect: PermValue.Deny,
            speak: PermValue.Deny);

        await voiceChannel.AddPermissionOverwriteAsync(guild.EveryoneRole, denyPermissions);

        await voiceChannel.AddPermissionOverwriteAsync(guildGroupsContext.CurrentUser, rolePermissions);

        if (guildGroupsContext.Roles != null)
        {
            foreach (var role in guildGroupsContext.Roles)
            {
                await voiceChannel.AddPermissionOverwriteAsync(role, rolePermissions);
            }
        }

        if (guildGroupsContext.Users != null)
        {
            foreach (var user in guildGroupsContext.Users)
            {
                await voiceChannel.AddPermissionOverwriteAsync(user, rolePermissions);
            }
        }
    }

    private async Task UserVoiceStateUpdatedHandleAsync(SocketUser user, SocketVoiceState originalState,
        SocketVoiceState updatedState)
    {
        if (updatedState.VoiceChannel != null)
        {
            var voiceChannel = updatedState.VoiceChannel;
            CancelDeletionCheckIfExists(voiceChannel.Id);

            await HandleCreatorChannelJoinAsync(user, voiceChannel);
        }

        if (originalState.VoiceChannel != null)
        {
            var voiceChannel = originalState.VoiceChannel;
            if (_managedVoiceChannels.Contains(voiceChannel.Id) && voiceChannel.ConnectedUsers.Count == 0)
            {
                RunDeletionCheckAsync(voiceChannel.Id);
            }
        }
    }

    private async Task HandleCreatorChannelJoinAsync(SocketUser user, SocketVoiceChannel joinedChannel)
    {
        var guild = joinedChannel.Guild;
        var guildConfiguration = await _discordBotConfigurationService.GetGuildConfigurationAsync(guild.Id);

        if (guildConfiguration?.VoiceChannelCreatorChannelId == null ||
            joinedChannel.Id != guildConfiguration.VoiceChannelCreatorChannelId.Value)
        {
            return;
        }

        if (user is not SocketGuildUser guildUser)
        {
            return;
        }

        var customName = await _discordBotConfigurationService.GetUserChannelNameAsync(guild.Id, guildUser.Id);
        var channelName = !string.IsNullOrWhiteSpace(customName?.Name)
            ? customName.Name
            : $"{guildUser.DisplayName ?? guildUser.Username}'s channel";

        var newChannel = await guild.CreateVoiceChannelAsync(channelName,
            channel => channel.CategoryId = joinedChannel.CategoryId);

        _managedVoiceChannels.Add(newChannel.Id);

        await guildUser.ModifyAsync(properties => properties.Channel = newChannel);

        RunDeletionCheckAsync(newChannel.Id);
    }

    private async void RunDeletionCheckAsync(ulong voiceChannelId)
    {
        CancelDeletionCheckIfExists(voiceChannelId);

        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        _voiceChannelDeletionCheckCancellationTokenSourcesDictionary[voiceChannelId] = cancellationTokenSource;

        try
        {
            await Task.Delay(TimeSpan.FromMinutes(3), cancellationToken);

            var voiceChannel = (SocketVoiceChannel) _discordClient.GetChannel(voiceChannelId);
            if (voiceChannel != null && voiceChannel.ConnectedUsers.Count == 0)
            {
                await voiceChannel.DeleteAsync();
                _managedVoiceChannels.Remove(voiceChannelId);
            }

            _voiceChannelDeletionCheckCancellationTokenSourcesDictionary.Remove(voiceChannelId);
        }
        catch (TaskCanceledException)
        {
        }
    }

    private void CancelDeletionCheckIfExists(ulong voiceChannelId)
    {
        if (_voiceChannelDeletionCheckCancellationTokenSourcesDictionary.ContainsKey(voiceChannelId))
        {
            _voiceChannelDeletionCheckCancellationTokenSourcesDictionary[voiceChannelId].Cancel();
            _voiceChannelDeletionCheckCancellationTokenSourcesDictionary.Remove(voiceChannelId);
        }
    }
}