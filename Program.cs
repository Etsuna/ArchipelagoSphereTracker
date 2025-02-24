using Discord;
using Discord.WebSocket;
using Discord.Commands;
using DotNetEnv;

class Program
{
    static async Task Main(string[] args)
    {
        Env.Load();

        var config = new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent,
            UseInteractionSnowflakeDate = false,
            ResponseInternalTimeCheck = false
        };

        Declare.client = new DiscordSocketClient(config);
        Declare.commandService = new CommandService();

        Declare.client.Log += LogAsync;
        Declare.client.Ready += ReadyAsync;
        Declare.client.MessageReceived += BotCommands.MessageReceivedAsync;

        await BotCommands.InstallCommandsAsync();

        DataManager.LoadReceiverAliases();
        DataManager.LoadAliasChoices();
        DataManager.LoadGameStatus();
        DataManager.LoadUrlAndChannel();
        DataManager.LoadRecapList();
        DataManager.LoadHintStatus();
        DataManager.LoadDisplayedItems();

        await Declare.client.LoginAsync(TokenType.Bot, Declare.discordToken);
        await Declare.client.StartAsync();

        await Task.Delay(-1);
    }

    static async Task LogAsync(LogMessage log)
    {
        Console.WriteLine(log);
    }

    static async Task ReadyAsync()
    {
        await BotCommands.RegisterCommandsAsync();
        Console.WriteLine("Bot is connected!");
        TrackingDataManager.StartTracking();
    }
}