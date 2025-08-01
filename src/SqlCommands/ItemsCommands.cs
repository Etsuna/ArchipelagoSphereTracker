﻿using System.Data.SQLite;
using System.Text.Json;

public static class ItemsCommands
{
    public static async Task<bool> IsFillerAsync(string gameName, string itemName)
    {
        try
        {
            using (var connection = new SQLiteConnection($"Data Source={Declare.DatabaseFile};Version=3;"))
            {
                await connection.OpenAsync();

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
                        Console.WriteLine($"Aucun résultat trouvé pour le jeu '{gameName}' et l'élément '{itemName}'.");
                        return false;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur lors de la vérification de la catégorie : {ex.Message}");
            return false;
        }
    }

    public static async Task SyncItemsFromJsonAsync(string jsonPath)
    {
        try
        {
            string jsonContent = await File.ReadAllTextAsync(jsonPath);
            using var doc = JsonDocument.Parse(jsonContent);

            using var connection = new SQLiteConnection($"Data Source={Declare.DatabaseFile};Version=3;");
            await connection.OpenAsync();

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
            Console.WriteLine("✅ Synchronisation terminée avec succès.");
        }
        catch (Exception ex)
        {
            string error = $"❌ Erreur lors de la synchronisation : {ex.Message}";
            Console.WriteLine(error);
        }
    }

}