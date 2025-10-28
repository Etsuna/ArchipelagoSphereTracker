using System.Data;
using System.Data.SQLite;
using System.IO;

namespace AST.GUI
{
    public static class DbExplorer
    {
        private static SQLiteConnection Open(string dbPath)
        {
            if (!File.Exists(dbPath))
                throw new FileNotFoundException("DB introuvable", dbPath);

            var cs = $"Data Source={dbPath};Version=3;Pooling=True;Journal Mode=WAL;Synchronous=NORMAL;BusyTimeout=5000;Read Only=True;";
            var conn = new SQLiteConnection(cs);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA foreign_keys=ON;";
            cmd.ExecuteNonQuery();
            return conn;
        }

        public static Task<List<string>> GetGuildsAsync(string dbPath)
        {
            return Task.Run(() =>
            {
                using var conn = Open(dbPath);
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT DISTINCT GuildId FROM ChannelsAndUrlsTable ORDER BY GuildId;";
                using var rd = cmd.ExecuteReader();
                var list = new List<string>();
                while (rd.Read()) list.Add(rd.GetString(0));
                return list;
            });
        }

        public static List<string> GetChannels(string dbPath, string guildId)
        {
            using var conn = Open(dbPath);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT DISTINCT ChannelId FROM ChannelsAndUrlsTable WHERE GuildId=@g ORDER BY ChannelId;";
            cmd.Parameters.AddWithValue("@g", guildId);
            using var rd = cmd.ExecuteReader();
            var list = new List<string>();
            while (rd.Read()) list.Add(rd.GetString(0));
            return list;
        }

        public static List<string> GetAlias(string dbPath, string guildId, string channelId)
        {
            using var conn = Open(dbPath);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT DISTINCT Alias FROM AliasChoicesTable WHERE GuildId=@g AND  ChannelId=@c;";
            cmd.Parameters.AddWithValue("@g", guildId);
            cmd.Parameters.AddWithValue("@c", channelId);
            using var rd = cmd.ExecuteReader();
            var list = new List<string>();
            while (rd.Read()) list.Add(rd.GetString(0));
            return list;
        }

        public static DataTable LoadTable(string dbPath, string table, string guildId, string channelId)
        {
            using var conn = Open(dbPath);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT * FROM {table} WHERE GuildId=@g AND ChannelId=@c";
            cmd.Parameters.AddWithValue("@g", guildId);
            cmd.Parameters.AddWithValue("@c", channelId);

            using var ad = new SQLiteDataAdapter(cmd);
            var dt = new DataTable();
            ad.Fill(dt);

            foreach (var col in new[] { "Id", "GuildId", "ChannelId" })
                if (dt.Columns.Contains(col)) dt.Columns.Remove(col);

            return dt;
        }

        public static DataTable LoadRecap(string dbPath, string guildId, string channelId)
        {
            using var conn = Open(dbPath);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT R.Id, R.GuildId, R.ChannelId, R.UserId, R.Alias, I.Item
                FROM RecapListTable R
                LEFT JOIN RecapListItemsTable I ON R.Id = I.RecapListTableId
                WHERE R.GuildId=@g AND R.ChannelId=@c
                ORDER BY R.Id";
            cmd.Parameters.AddWithValue("@g", guildId);
            cmd.Parameters.AddWithValue("@c", channelId);
            using var ad = new SQLiteDataAdapter(cmd);
            var dt = new DataTable();
            ad.Fill(dt);
            return dt;
        }

        public static DataTable RunReadOnly(string dbPath, string sql)
        {
            using var conn = Open(dbPath);
            using var cmd = conn.CreateCommand();
            // safety: only allow SELECT and PRAGMA
            var norm = sql.TrimStart().ToUpperInvariant();
            if (!(norm.StartsWith("SELECT") || norm.StartsWith("PRAGMA")))
                throw new InvalidOperationException("Lecture seule: SELECT/PRAGMA uniquement.");
            cmd.CommandText = sql;
            using var ad = new SQLiteDataAdapter(cmd);
            var dt = new DataTable();
            ad.Fill(dt);
            return dt;
        }
    }
}
