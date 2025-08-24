using System.Data.SQLite;
using System.Text;

public static class ApWorldListCommands
{
    public static async Task<string?> GetItemsByTitleAsync(string title)
    {
        try
        {
            await using var connection = await Db.OpenReadAsync();

            var sb = new StringBuilder()
                .Append("**").Append(title).Append("**\n\n");

            const string queryId = "SELECT Id FROM ApWorldListTable WHERE Title = @Title;";
            using (var command = new SQLiteCommand(queryId, connection))
            {
                command.Parameters.AddWithValue("@Title", title);
                var result = await command.ExecuteScalarAsync().ConfigureAwait(false);
                if (result is null)
                    return $"Title '{title}' not found.";

                var apWorldListTableId = Convert.ToInt32(result);

                const string queryItems = @"
                    SELECT Text, Link
                    FROM ApWorldItemTable
                    WHERE ApWorldListTableId = @ApWorldListTableId;";

                using var itemCommand = new SQLiteCommand(queryItems, connection);
                itemCommand.Parameters.AddWithValue("@ApWorldListTableId", apWorldListTableId);

                using var reader = await itemCommand.ExecuteReaderAsync().ConfigureAwait(false);
                var hasAny = false;
                while (await reader.ReadAsync().ConfigureAwait(false))
                {
                    hasAny = true;
                    var text = reader.IsDBNull(0) ? "" : reader.GetString(0);
                    var link = reader.IsDBNull(1) ? null : reader.GetString(1);

                    if (!string.IsNullOrWhiteSpace(link))
                        sb.Append("• ").Append(text).Append(" — ").Append("[Link](").Append(link).Append(")\n");
                    else
                        sb.Append("• ").Append(text).Append('\n');
                }

                if (!hasAny)
                    sb.Append("_(no items)_\n");
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while retrieving items: {ex.Message}");
            return null;
        }
    }

    public static async Task<List<string>> GetAllTitles()
    {
        var titles = new List<string>();

        try
        {
            await using var connection = await Db.OpenReadAsync();

            const string query = "SELECT Title FROM ApWorldListTable;";
            using var command = new SQLiteCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);

            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                if (!reader.IsDBNull(0))
                    titles.Add(reader.GetString(0));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while retrieving titles: {ex.Message}");
        }

        return titles;
    }
}
