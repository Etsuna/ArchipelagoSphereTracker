using System.IO;
using System.Text;

namespace AST.GUI
{
    public static class EnvHelper
    {
        private static readonly string EnvPath = Path.Combine(AppContext.BaseDirectory, ".env");

        public static bool EnvExists() => File.Exists(EnvPath);

        public static void WriteEnv(string token, string lang)
        {
            var sb = new StringBuilder()
                .AppendLine($"DISCORD_TOKEN={token}")
                .AppendLine($"LANGUAGE={lang}");
            File.WriteAllText(EnvPath, sb.ToString(), Encoding.UTF8);
        }
    }
}
