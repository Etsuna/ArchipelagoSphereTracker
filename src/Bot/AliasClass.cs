using ArchipelagoSphereTracker.src.Resources;
using Discord;
using Discord.WebSocket;
using System.Text;

public class AliasClass
{
    public static async Task<string> AddAlias(SocketSlashCommand command, string message, string? alias, string channelId, string guildId)
    {
        var userId = command.User.Id.ToString();
        var skipUselessMention = command.Data.Options.ElementAtOrDefault(1)?.Value as bool? ?? false;

        if (string.IsNullOrWhiteSpace(alias))
        {
            return Resource.AliasEmpty;
        }

        var getReceiverAlias = await ReceiverAliasesCommands.GetAllUsersIds(guildId, channelId, alias);

        if (getReceiverAlias.Contains(userId))
        {
            return string.Format(Resource.AliasAlreadyRegistered, alias, userId);
        }

        await ReceiverAliasesCommands.InsertReceiverAlias(guildId, channelId, alias, userId, skipUselessMention);

        var checkRecapList = await RecapListCommands.CheckIfExists(guildId, channelId, userId, alias);
        if (!checkRecapList)
        {
            await RecapListCommands.AddOrEditRecapListAsync(guildId, channelId, userId, alias);
        }

        var getAliasItems = await DisplayItemCommands.GetAliasItems(guildId, channelId, alias);
        if (getAliasItems != null)
        {
            await RecapListCommands.AddOrEditRecapListItemsAsync(guildId, channelId, alias, getAliasItems);
        }

        message = string.Format(Resource.AliasAdded, alias, userId);
        return message;
    }

    public static async Task<string> DeleteAlias(SocketSlashCommand command, IGuildUser? guildUser, string message, string? alias, string channelId, string guildId)
    {
        var checkChannel = await DatabaseCommands.CheckIfChannelExistsAsync(guildId, channelId, "ChannelsAndUrlsTable");
        var getReceiverAliases = await ReceiverAliasesCommands.GetReceiver(guildId, channelId);
        async Task<bool> HasValidChannelDataAsync(string guildId, string channelId)
        {
            return checkChannel = await DatabaseCommands.CheckIfChannelExistsAsync(guildId, channelId, "ChannelsAndUrlsTable");
        }

        if (!await HasValidChannelDataAsync(guildId, channelId))
        {
            message = Resource.AliasOrUrlNotRegistered;
        }
        else
        {
            getReceiverAliases = await ReceiverAliasesCommands.GetReceiver(guildId, channelId);

            if (getReceiverAliases.Count == 0)
            {
                message = Resource.AliasNotRegistered;
            }
            else if (alias != null)
            {
                var getUserId = await ReceiverAliasesCommands.GetReceiverUserIdsAsync(guildId, channelId, alias);

                if (getUserId != null)
                {
                    message = string.Format(Resource.AliasNotFound, alias);
                    foreach (var value in getUserId.Select(x => x.UserId))
                    {
                        if (value == command.User.Id.ToString() || guildUser != null && guildUser.GuildPermissions.Administrator)
                        {
                            await ReceiverAliasesCommands.DeleteReceiverAlias(guildId, channelId, alias);

                            message = value == command.User.Id.ToString()
                                ? string.Format(Resource.AliasDeleted, alias)
                                : $"ADMIN: " + string.Format(Resource.AliasDeleted, alias);

                            await RecapListCommands.DeleteAliasAndRecapListAsync(guildId, channelId, value, alias);
                        }
                        else
                        {
                            message = string.Format(Resource.AliasOtherOwner, alias);
                        }
                    }
                }
                else
                {
                    message = string.Format(Resource.AliasNotFound, alias);
                }
            }
        }

        return message;
    }

    public static async Task<string> GetAlias(string message, string channelId, string guildId)
    {
        var checkChannel = await DatabaseCommands.CheckIfChannelExistsAsync(guildId, channelId, "ChannelsAndUrlsTable");
        var getReceiverAliases = await ReceiverAliasesCommands.GetReceiver(guildId, channelId);

        if (!checkChannel)
        {
            message = Resource.NoUrlRegistered;
        }
        else if (getReceiverAliases.Count == 0)
        {
            message = Resource.AliasNotRegistered;
        }
        else
        {
            var sb = new StringBuilder(Resource.AliasTable);
            sb.AppendLine();
            foreach (var getReceiverAliase in getReceiverAliases)
            {
                var getUserIds = await ReceiverAliasesCommands.GetReceiverUserIdsAsync(guildId, channelId, getReceiverAliase);

                foreach (var value in getUserIds)
                {
                    var user = await Declare.Client.GetUserAsync(ulong.Parse(value.UserId));
                    sb.AppendLine(string.Format(Resource.AliasTableValue, user.Username, getReceiverAliase, HelperClass.TranslateBool(value.IsEnabled)));
                }
            }
            message = sb.ToString();
        }

        return message;
    }
}
