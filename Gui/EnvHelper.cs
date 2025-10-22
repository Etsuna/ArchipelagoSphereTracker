using System.IO;
using System.Text;

namespace AST.GUI
{
    public static class EnvHelper
    {
        public static string BasePath = Path.GetDirectoryName(Environment.ProcessPath) ?? throw new InvalidOperationException("Environment.ProcessPath is null.");

        private static readonly string EnvPath = Path.Combine(BasePath, ".env");

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
