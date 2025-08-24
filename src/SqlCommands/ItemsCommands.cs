using ArchipelagoSphereTracker.src.Resources;
using System.Data.SQLite;
using System.Text.Json;

public static class ItemsCommands
{
    public static async Task<bool> IsFillerAsync(
        string gameName,
        string itemName
        )
    {
        try
        {
            await using var connection = await Db.OpenReadAsync();

            const string query = @"
                SELECT Category
                FROM ItemsTable
                WHERE GameName = @GameName AND ItemName = @ItemName;";

            using var command = new SQLiteCommand(query, connection);
            command.Parameters.AddWithValue("@GameName", gameName);
            command.Parameters.AddWithValue("@ItemName", itemName);

            var result = await command.ExecuteScalarAsync().ConfigureAwait(false);

            if (result is string category)
                return category.Equals("filler", StringComparison.OrdinalIgnoreCase);

            Console.WriteLine(string.Format(Resource.IsFillerAsyncNoResultFound, gameName, itemName));
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while checking the category: {ex.Message}");
            return false;
        }
    }

    public static async Task SyncItemsFromJsonAsync(string jsonPath)
    {
        try
        {
            var jsonContent = await File.ReadAllTextAsync(jsonPath).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(jsonContent);

            await Db.WriteAsync(async conn =>
            {
                using (var dropCmd = new SQLiteCommand("DROP TABLE IF EXISTS ItemsTable;", conn))
                    await dropCmd.ExecuteNonQueryAsync().ConfigureAwait(false);

                using (var createCmd = new SQLiteCommand(@"
                    CREATE TABLE ItemsTable (
                        GameName TEXT NOT NULL,
                        Category TEXT NOT NULL,
                        ItemName TEXT NOT NULL,
                        PRIMARY KEY (GameName, Category, ItemName)
                    );", conn))
                {
                    await createCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                }

                using var insertCmd = new SQLiteCommand(@"
                    INSERT INTO ItemsTable (GameName, Category, ItemName)
                    VALUES (@GameName, @Category, @ItemName);", conn);

                var pGame = insertCmd.Parameters.Add("@GameName", System.Data.DbType.String);
                var pCategory = insertCmd.Parameters.Add("@Category", System.Data.DbType.String);
                var pItem = insertCmd.Parameters.Add("@ItemName", System.Data.DbType.String);

                insertCmd.Prepare();

                foreach (var game in doc.RootElement.EnumerateObject())
                {
                    var gameName = game.Name;

                    foreach (var catProp in game.Value.EnumerateObject())
                    {
                        var category = catProp.Name;

                        foreach (var itemEl in catProp.Value.EnumerateArray())
                        {
                            if (itemEl.ValueKind != JsonValueKind.String) continue;

                            var itemName = itemEl.GetString();
                            if (string.IsNullOrWhiteSpace(itemName)) continue;

                            pGame.Value = gameName;
                            pCategory.Value = category;
                            pItem.Value = itemName;

                            await insertCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                        }
                    }
                }
            });

            Console.WriteLine(Resource.SyncItemsFromJsonAsyncSyncComplete);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error during synchronization: {ex.Message}");
        }
    }
}
