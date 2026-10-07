using Discord;
using Discord.WebSocket;

internal static class SlashCommandAttachmentResolver
{
    internal static IAttachment? GetFile(SocketSlashCommand command)
        => FindFileValue(command.Data.Options?.Select(option =>
            (option.Name, Value: (object?)option.Value)) ?? [])
            as IAttachment;

    internal static object? FindFileValue(IEnumerable<(string Name, object? Value)> options)
        => options.FirstOrDefault(option =>
            string.Equals(option.Name, "file", StringComparison.Ordinal)).Value;
}
