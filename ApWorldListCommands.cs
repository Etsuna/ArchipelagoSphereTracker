﻿using System.Data.SQLite;

public static class ApWorldListCommands
{
    public static async Task<string?> GetItemsByTitleAsync(string title)
    {
        try
        {
            using (var connection = new SQLiteConnection($"Data Source={Declare.DatabaseFile};Version=3;"))
            {
                await connection.OpenAsync();

                var message = $"**{title}**\n\n";

                var query = "SELECT Id FROM ApWorldListTable WHERE Title = @Title";
                using (var command = new SQLiteCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Title", title);
                    var result = await command.ExecuteScalarAsync();
                    if (result == null)
                    {
                        return $"Title '{title}' non trouvé.";
                    }

                    int apWorldListTableId = Convert.ToInt32(result);

                    query = "SELECT Text, Link FROM ApWorldItemTable WHERE ApWorldListTableId = @ApWorldListTableId";
                    using (var itemCommand = new SQLiteCommand(query, connection))
                    {
                        itemCommand.Parameters.AddWithValue("@ApWorldListTableId", apWorldListTableId);
                        using (var reader = await itemCommand.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                string text = reader.GetString(0);
                                string link = reader.IsDBNull(1) ? "NULL" : reader.GetString(1);
                                if (link != "NULL")
                                {
                                    message += $"• {text} — [Link]({link})\n";
                                }
                                else
                                {
                                    message += $"• {text}\n";
                                }
                            }
                        }
                    }
                }

                return message;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur lors de la récupération des items : {ex.Message}");
            return null;
        }
    }

    public static async Task<List<string>> GetAllTitles()
    {
        try
        {
            using (var connection = new SQLiteConnection($"Data Source={Declare.DatabaseFile};Version=3;"))
            {
                await connection.OpenAsync();
                var titles = new List<string>();
                var query = "SELECT Title FROM ApWorldListTable";
                using (var command = new SQLiteCommand(query, connection))
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        titles.Add(reader.GetString(0));
                    }
                }
                return titles;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur lors de la récupération des titres : {ex.Message}");
            return new List<string>();
        }
    }
}
