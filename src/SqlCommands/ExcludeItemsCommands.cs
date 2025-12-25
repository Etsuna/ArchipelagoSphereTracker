using ArchipelagoSphereTracker.src.Resources;
using Discord;
using Discord.WebSocket;
using System.Data.SQLite;

public static class ExcludedItemsCommands
{
    public static string message { get; set; } = string.Empty;
    public static async Task<List<string>> GetItemNamesForAliasAsync(string guildId, string channelId, string alias)
    {
        var results = new List<string>();

        try
        {
            await using var connection = await Db.OpenReadAsync();

            const string query = @"
            SELECT dpi.Name
            FROM AliasChoicesTable a
            JOIN DatapackageGameMap gm
              ON gm.GuildId = a.GuildId
             AND gm.ChannelId = a.ChannelId
             AND gm.GameName = a.Game
            JOIN DatapackageItems dpi
              ON dpi.GuildId   = gm.GuildId
             AND dpi.ChannelId = gm.ChannelId
             AND dpi.DatasetKey = gm.DatasetKey
            WHERE a.GuildId = @GuildId
              AND a.ChannelId = @ChannelId
              AND a.Alias LIKE @Alias;";

            using var command = new SQLiteCommand(query, connection);
            command.Parameters.AddWithValue("@GuildId", guildId);
            command.Parameters.AddWithValue("@ChannelId", channelId);
            command.Parameters.AddWithValue("@Alias", alias);

            using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                results.Add(reader.GetString(0));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while retrieving items for alias: {ex.Message}");
        }

        return results;
    }

    public static async Task<List<string>> GetExcludedItemsByAliasAsync(string guildId, string channelId, string alias)
    {
        var items = new List<string>();

        try
        {
            await using var conn = await Db.OpenReadAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
            SELECT Item
            FROM ExcludedItemTable
            WHERE GuildId = @GuildId
              AND ChannelId = @ChannelId
              AND Alias = @Alias
            ORDER BY Item;";
            cmd.Parameters.AddWithValue("@GuildId", guildId);
            cmd.Parameters.AddWithValue("@ChannelId", channelId);
            cmd.Parameters.AddWithValue("@Alias", alias);

            await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                items.Add(reader.GetString(0));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while reading excluded items: {ex.Message}");
        }

        return items;
    }

    public static async Task<string> AddExcludedItemAsync(SocketSlashCommand command, string? alias, string channelId, string guildId)
    {
        var userId = command.User.Id.ToString();
        var item = command.Data.Options.ElementAtOrDefault(1)?.Value as string;

        try
        {
            await Db.WriteAsync(async conn =>
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT OR IGNORE INTO ExcludedItemTable (GuildId, ChannelId, UserId, Alias, Item)
                    VALUES (@GuildId, @ChannelId, @UserId, @Alias, @Item);";
                cmd.Parameters.AddWithValue("@GuildId", guildId);
                cmd.Parameters.AddWithValue("@ChannelId", channelId);
                cmd.Parameters.AddWithValue("@UserId", userId);
                cmd.Parameters.AddWithValue("@Alias", alias);
                cmd.Parameters.AddWithValue("@Item", item);
                await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while adding excluded item: {ex.Message}");
        }

        message = string.Format(Resource.ExcludeItemAdded, item, userId);
        return message;
    }

    public static async Task<string> GetExcludedItemsByAliasAsync(SocketSlashCommand command,string channelId,string guildId)
    {
        var userId = command.User.Id.ToString();
        var sb = new System.Text.StringBuilder();

        try
        {
            await using var conn = await Db.OpenReadAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
            SELECT Alias, Item
            FROM ExcludedItemTable
            WHERE GuildId = @GuildId
              AND ChannelId = @ChannelId
              AND UserId = @UserId
            ORDER BY Alias, Item;";
            cmd.Parameters.AddWithValue("@GuildId", guildId);
            cmd.Parameters.AddWithValue("@ChannelId", channelId);
            cmd.Parameters.AddWithValue("@UserId", userId);

            string? currentAlias = null;
            var currentItems = new List<string>();

            await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                var alias = reader.GetString(0);
                var item = reader.GetString(1);

                if (currentAlias != null && !string.Equals(currentAlias, alias, StringComparison.Ordinal))
                {
                    sb.AppendLine($"**{currentAlias}** :");
                    foreach (var it in currentItems)
                        sb.AppendLine(it);
                    sb.AppendLine();

                    currentItems.Clear();
                }

                currentAlias = alias;
                currentItems.Add(item);
            }

            if (currentAlias != null && currentItems.Count > 0)
            {
                sb.AppendLine($"**{currentAlias}** :");
                foreach (var it in currentItems)
                    sb.AppendLine(it);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while reading excluded items: {ex.Message}");
        }

        return sb.Length == 0 ? Resource.HelperNoItems : sb.ToString();
    }


    public static async Task<string> DeleteExcludedItemAsync(SocketSlashCommand command, string channelId, string guildId, string? alias)
    {
        var userId = command.User.Id.ToString();
        var item = command.Data.Options.ElementAtOrDefault(1)?.Value as string;

        if (string.IsNullOrWhiteSpace(item))
            return Resource.HelperNoItems;

        try
        {
            int rows = 0;
            await Db.WriteAsync(async conn =>
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                DELETE FROM ExcludedItemTable
                WHERE GuildId = @GuildId
                  AND ChannelId = @ChannelId
                  AND UserId = @UserId
                  AND Alias = @Alias
                  AND Item = @Item;";
                cmd.Parameters.AddWithValue("@GuildId", guildId);
                cmd.Parameters.AddWithValue("@ChannelId", channelId);
                cmd.Parameters.AddWithValue("@UserId", userId);
                cmd.Parameters.AddWithValue("@Alias", alias);
                cmd.Parameters.AddWithValue("@Item", item);
                rows = await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            });

            return rows > 0
                ? string.Format(Resource.ExcludeItemDeleted, item, alias)
                : string.Format(Resource.ExcludeItemNotFound, item, alias);

        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while deleting excluded item: {ex.Message}");
            return "erreur lors de la suppression.";
        }
    }

    public static async Task<bool> IsItemExcludedForAnyUserAsync(string guildId, string channelId, string alias, string item, List<ReceiverUserInfo> users)
    {
        await using var conn = await Db.OpenReadAsync();

        foreach (var u in users)
        {
            if (string.IsNullOrWhiteSpace(u.UserId))
                continue;

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
            SELECT 1
            FROM ExcludedItemTable
            WHERE GuildId = @GuildId
              AND ChannelId = @ChannelId
              AND UserId = @UserId
              AND Alias = @Alias
              AND Item = @Item
            LIMIT 1;";
            cmd.Parameters.AddWithValue("@GuildId", guildId);
            cmd.Parameters.AddWithValue("@ChannelId", channelId);
            cmd.Parameters.AddWithValue("@UserId", u.UserId);
            cmd.Parameters.AddWithValue("@Alias", alias);
            cmd.Parameters.AddWithValue("@Item", item);

            var res = await cmd.ExecuteScalarAsync().ConfigureAwait(false);
            if (res != null)
                return true;
        }

        return false;
    }
}
