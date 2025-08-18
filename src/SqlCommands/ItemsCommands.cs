using ArchipelagoSphereTracker.src.Resources;
using System.Data.SQLite;
using System.Text.Json;

public static class ItemsCommands
{
    public static async Task<bool> IsFillerAsync(string gameName, string itemName)
    {
        try
        {
                using var connection = await Db.OpenAsync(Declare.CT);

                var query = "SELECT Category FROM ItemsTable WHERE GameName = @GameName AND ItemName = @ItemName";

                using (var command = new SQLiteCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@GameName", gameName);
                    command.Parameters.AddWithValue("@ItemName", itemName);

                    var result = await command.ExecuteScalarAsync();

                    if (result is string category)
                    {
                        return category.Equals("filler", StringComparison.OrdinalIgnoreCase);
                    }
                    else
                    {
                        Console.WriteLine(string.Format(Resource.IsFillerAsyncNoResultFound, gameName, itemName));
                        return false;
                    }
                }
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
            string jsonContent = await File.ReadAllTextAsync(jsonPath);
            using var doc = JsonDocument.Parse(jsonContent);
            
            using var connection = await Db.OpenAsync(Declare.CT);
            using var transaction = connection.BeginTransaction();

            var logLines = new List<string>();

            var dropCmd = new SQLiteCommand("DROP TABLE IF EXISTS ItemsTable;", connection, transaction);
            await dropCmd.ExecuteNonQueryAsync();

            var createCmd = new SQLiteCommand(@"
            CREATE TABLE ItemsTable (
                GameName TEXT NOT NULL,
                Category TEXT NOT NULL,
                ItemName TEXT NOT NULL,
                PRIMARY KEY (GameName, Category, ItemName)
            );", connection, transaction);
            await createCmd.ExecuteNonQueryAsync();

            var insertCmd = new SQLiteCommand(@"
            INSERT INTO ItemsTable (GameName, Category, ItemName)
            VALUES (@GameName, @Category, @ItemName);", connection, transaction);

            insertCmd.Parameters.Add(new SQLiteParameter("@GameName"));
            insertCmd.Parameters.Add(new SQLiteParameter("@Category"));
            insertCmd.Parameters.Add(new SQLiteParameter("@ItemName"));

            foreach (var game in doc.RootElement.EnumerateObject())
            {
                string gameName = game.Name;

                foreach (var categoryProperty in game.Value.EnumerateObject())
                {
                    string category = categoryProperty.Name;

                    foreach (var item in categoryProperty.Value.EnumerateArray())
                    {
                        if (item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()))
                            continue;

                        string itemName = item.GetString()!;

                        insertCmd.Parameters["@GameName"].Value = gameName;
                        insertCmd.Parameters["@Category"].Value = category;
                        insertCmd.Parameters["@ItemName"].Value = itemName;

                        await insertCmd.ExecuteNonQueryAsync();
                    }
                }
            }

            transaction.Commit();
            Console.WriteLine(Resource.SyncItemsFromJsonAsyncSyncComplete);
        }
        catch (Exception ex)
        {
            string error = $"❌ Error during synchronization: {ex.Message}";
            Console.WriteLine(error);
        }
    }
}