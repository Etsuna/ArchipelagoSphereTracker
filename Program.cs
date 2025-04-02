using Discord;
using Discord.WebSocket;
using Discord.Commands;
using DotNetEnv;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Starting bot... Achipelago Version: Archipelago_0.6.0");

        Env.Load();

        var config = new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent,
            UseInteractionSnowflakeDate = false,
            ResponseInternalTimeCheck = false
        };

        Declare.Client = new DiscordSocketClient(config);
        Declare.CommandService = new CommandService();

        Declare.Client.Log += LogAsync;
        Declare.Client.Ready += ReadyAsync;
        Declare.Client.MessageReceived += BotCommands.MessageReceivedAsync;

        await BotCommands.InstallCommandsAsync();

        DataManager.LoadReceiverAliases();
        DataManager.LoadAliasChoices();
        DataManager.LoadGameStatus();
        DataManager.LoadUrlAndChannel();
        DataManager.LoadRecapList();
        DataManager.LoadHintStatus();
        DataManager.LoadDisplayedItems();

        await Declare.Client.LoginAsync(TokenType.Bot, Declare.DiscordToken);
        await Declare.Client.StartAsync();

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