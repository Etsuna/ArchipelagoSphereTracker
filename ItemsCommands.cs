using System.Data.SQLite;

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

                    if (result != null)
                    {
                        return result.ToString().Equals("filler", StringComparison.OrdinalIgnoreCase);
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
}

