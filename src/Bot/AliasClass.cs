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
            return "L'alias ne peut pas être vide.";
        }

        var getReceiverAlias = await ReceiverAliasesCommands.GetAllUsersIds(guildId, channelId, alias);

        if (getReceiverAlias.Contains(userId))
        {
            return $"L'alias '{alias}' est déjà enregistré pour <@{userId}>.";
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

        message = $"Alias ajouté : {alias} est maintenant associé à <@{userId}> et son récap généré.";
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
            message = "Pas d'URL Enregistrée pour ce channel ou aucun Alias enregistré.";
        }
        else
        {
            getReceiverAliases = await ReceiverAliasesCommands.GetReceiver(guildId, channelId);

            if (getReceiverAliases.Count == 0)
            {
                message = "Aucun Alias est enregistré.";
            }
            else if (alias != null)
            {
                var getUserId = await ReceiverAliasesCommands.GetReceiverUserIdsAsync(guildId, channelId, alias);

                if (getUserId != null)
                {
                    message = $"Aucun alias trouvé pour '{alias}'.";
                    foreach (var value in getUserId.Select(x => x.UserId))
                    {
                        if (value == command.User.Id.ToString() || (guildUser != null && guildUser.GuildPermissions.Administrator))
                        {
                            await ReceiverAliasesCommands.DeleteReceiverAlias(guildId, channelId, alias);

                            message = value == command.User.Id.ToString()
                                ? $"Alias '{alias}' supprimé."
                                : $"ADMIN : Alias '{alias}' supprimé.";

                            await RecapListCommands.DeleteAliasAndRecapListAsync(guildId, channelId, value, alias);
                        }
                        else
                        {
                            message = $"Vous n'êtes pas le détenteur de cet alias : '{alias}'. Suppression non effectuée.";
                        }
                    }
                }
                else
                {
                    message = $"Aucun alias trouvé pour '{alias}'.";
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
            message = "Pas d'URL Enregistrée pour ce channel.";
        }
        else if (getReceiverAliases.Count == 0)
        {
            message = "Aucun Alias est enregistré.";
        }
        else
        {
            var sb = new StringBuilder("Voici le tableau des utilisateurs :\n");
            foreach (var getReceiverAliase in getReceiverAliases)
            {
                var getUserIds = await ReceiverAliasesCommands.GetReceiverUserIdsAsync(guildId, channelId, getReceiverAliase);

                foreach (var value in getUserIds)
                {
                    var user = await Declare.Client.GetUserAsync(ulong.Parse(value.UserId));
                    sb.AppendLine($"| {user.Username} | {getReceiverAliase} | Useless Item Skip: {value.IsEnabled.ToString()}");
                }
            }
            message = sb.ToString();
        }

        return message;
    }
}
