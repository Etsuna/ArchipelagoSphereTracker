using System.Data.SQLite;

public static class HintStatusCommands
{
    public static async Task<List<HintStatus>> GetHintStatusForGuildAndChannelAsync(string guildId, string channelId)
    {
        var hintStatuses = new List<HintStatus>();
        using (var connection = new SQLiteConnection($"Data Source={Declare.DatabaseFile};Version=3;"))
        {
            await connection.OpenAsync();
            using (var command = new SQLiteCommand(@"
                SELECT Finder, Receiver, Item, Location, Game, Entrance, Found
                FROM HintStatusTable
                WHERE GuildId = @GuildId AND ChannelId = @ChannelId;", connection))
            {
                command.Parameters.AddWithValue("@GuildId", guildId);
                command.Parameters.AddWithValue("@ChannelId", channelId);
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var hintStatus = new HintStatus
                        {
                            Finder = reader["Finder"]?.ToString() ?? string.Empty,
                            Receiver = reader["Receiver"]?.ToString() ?? string.Empty,
                            Item = reader["Item"]?.ToString() ?? string.Empty,
                            Location = reader["Location"]?.ToString() ?? string.Empty,
                            Game = reader["Game"]?.ToString() ?? string.Empty,
                            Entrance = reader["Entrance"]?.ToString() ?? string.Empty,
                            Found = reader["Found"]?.ToString() ?? string.Empty
                        };
                        hintStatuses.Add(hintStatus);
                    }
                }
            }
        }
        return hintStatuses;
    }

    public static async Task AddOrReplaceHintStatusAsync(string guild, string channel, List<HintStatus> hintStatus)
    {
        try
        {
            using (var connection = new SQLiteConnection($"Data Source={Declare.DatabaseFile};Version=3;"))
            {
                await connection.OpenAsync();

                using (var transaction = (SQLiteTransaction)await connection.BeginTransactionAsync())
                {
                    using (var command = new SQLiteCommand(@"
                        INSERT OR REPLACE INTO HintStatusTable (GuildId, ChannelId, Finder, Receiver, Item, Location, Game, Entrance, Found)
                        VALUES (@GuildId, @ChannelId, @Finder, @Receiver, @Item, @Location, @Game, @Entrance, @Found);", connection, transaction))
                    {
                        command.Parameters.Add("@GuildId", System.Data.DbType.String);
                        command.Parameters.Add("@ChannelId", System.Data.DbType.String);
                        command.Parameters.Add("@Finder", System.Data.DbType.String);
                        command.Parameters.Add("@Receiver", System.Data.DbType.String);
                        command.Parameters.Add("@Item", System.Data.DbType.String);
                        command.Parameters.Add("@Location", System.Data.DbType.String);
                        command.Parameters.Add("@Game", System.Data.DbType.String);
                        command.Parameters.Add("@Entrance", System.Data.DbType.String);
                        command.Parameters.Add("@Found", System.Data.DbType.String);

                        foreach (var status in hintStatus)
                        {
                            command.Parameters["@GuildId"].Value = guild;
                            command.Parameters["@ChannelId"].Value = channel;
                            command.Parameters["@Finder"].Value = status.Finder;
                            command.Parameters["@Receiver"].Value = status.Receiver;
                            command.Parameters["@Item"].Value = status.Item;
                            command.Parameters["@Location"].Value = status.Location;
                            command.Parameters["@Game"].Value = status.Game;
                            command.Parameters["@Entrance"].Value = status.Entrance;
                            command.Parameters["@Found"].Value = status.Found;

                            await command.ExecuteNonQueryAsync();
                        }
                    }

                    await transaction.CommitAsync();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur lors de l'ajout ou remplacement du statut d'indice : {ex.Message}");
        }
    }

    public static async Task<bool> CheckIfExists(string guildId, string channelId, HintStatus hintStatus)
    {
        try
        {
            using (var connection = new SQLiteConnection($"Data Source={Declare.DatabaseFile};Version=3;"))
            {
                await connection.OpenAsync();
                using (var command = new SQLiteCommand(@"
                    SELECT COUNT(*) 
                    FROM HintStatusTable 
                    WHERE GuildId = @GuildId 
                      AND ChannelId = @ChannelId 
                      AND Finder = @Finder 
                      AND Receiver = @Receiver 
                      AND Item = @Item 
                      AND Location = @Location 
                      AND Game = @Game
                      AND Entrance = @Entrance;", connection))
                {
                    command.Parameters.AddWithValue("@GuildId", guildId);
                    command.Parameters.AddWithValue("@ChannelId", channelId);
                    command.Parameters.AddWithValue("@Finder", hintStatus.Finder);
                    command.Parameters.AddWithValue("@Receiver", hintStatus.Receiver);
                    command.Parameters.AddWithValue("@Item", hintStatus.Item);
                    command.Parameters.AddWithValue("@Location", hintStatus.Location);
                    command.Parameters.AddWithValue("@Game", hintStatus.Game);
                    command.Parameters.AddWithValue("@Entrance", hintStatus.Entrance);
                    var count = (long)(await command.ExecuteScalarAsync() ?? 0);
                    if (count > 0)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur lors de la vérification de l'existence du statut d'indice : {ex.Message}");
            return false;
        }
    }
}
