using System.Text;

public static class MetricsCollector
{
    public static async Task<string> BuildAsync()
    {
        var sb = new StringBuilder();

        try
        {
            await using var conn = await Db.OpenReadAsync();

            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = """
                    SELECT GuildId, ChannelId, Name, Game, Total, Checks
                    FROM GameStatusTable;
                    """;
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    var guild = rd["GuildId"]?.ToString() ?? "";
                    var channel = rd["ChannelId"]?.ToString() ?? "";
                    var name = rd["Name"]?.ToString() ?? "";
                    var game = rd["Game"]?.ToString() ?? "";
                    var total = rd["Total"] is DBNull ? 0 : Convert.ToInt32(rd["Total"]);
                    var checks = rd["Checks"] is DBNull ? 0 : Convert.ToInt32(rd["Checks"]);
                    var ratio = total > 0 ? (double)checks / total : 0.0;

                    sb.Append("archipelago_player_checks_total{");
                    sb.Append($"guild=\"{Esc(guild)}\",channel=\"{Esc(channel)}\",player=\"{Esc(name)}\",game=\"{Esc(game)}\"}} ");
                    sb.AppendLine(checks.ToString(System.Globalization.CultureInfo.InvariantCulture));

                    sb.Append("archipelago_player_progress_ratio{");
                    sb.Append($"guild=\"{Esc(guild)}\",channel=\"{Esc(channel)}\",player=\"{Esc(name)}\",game=\"{Esc(game)}\"}} ");
                    sb.AppendLine(ratio.ToString(System.Globalization.CultureInfo.InvariantCulture));
                }
            }

            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = """
                    SELECT GuildId, ChannelId, Receiver, COUNT(*) AS Cnt
                    FROM DisplayedItemTable
                    GROUP BY GuildId, ChannelId, Receiver;
                    """;
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    var guild = rd["GuildId"]?.ToString() ?? "";
                    var channel = rd["ChannelId"]?.ToString() ?? "";
                    var receiver = rd["Receiver"]?.ToString() ?? "";
                    var cnt = Convert.ToInt32(rd["Cnt"]);

                    sb.Append("archipelago_items_received_total{");
                    sb.Append($"guild=\"{Esc(guild)}\",channel=\"{Esc(channel)}\",receiver=\"{Esc(receiver)}\"}} ");
                    sb.AppendLine(cnt.ToString(System.Globalization.CultureInfo.InvariantCulture));
                }
            }

            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = """
                    SELECT GuildId, ChannelId, Receiver, COUNT(*) AS Cnt
                    FROM HintStatusTable
                    GROUP BY GuildId, ChannelId, Receiver;
                    """;
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    var guild = rd["GuildId"]?.ToString() ?? "";
                    var channel = rd["ChannelId"]?.ToString() ?? "";
                    var receiver = rd["Receiver"]?.ToString() ?? "";
                    var cnt = Convert.ToInt32(rd["Cnt"]);

                    sb.Append("archipelago_hints_total{");
                    sb.Append($"guild=\"{Esc(guild)}\",channel=\"{Esc(channel)}\",receiver=\"{Esc(receiver)}\"}} ");
                    sb.AppendLine(cnt.ToString(System.Globalization.CultureInfo.InvariantCulture));
                }
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"# error: {ex.Message}");
        }

        return sb.ToString();
    }

    private static string Esc(string s) =>
        s.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
