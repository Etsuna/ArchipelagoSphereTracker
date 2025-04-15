using Newtonsoft.Json;
using System.Reflection;

public static class DataManager
{
    private static void LoadData<T>(string filePath, ref T data, Action<T>? clearAction = null)
    {
        if (clearAction != null && data != null)
        {
            clearAction(data);
        }

        if (File.Exists(filePath))
        {
            var json = File.ReadAllText(filePath);
            data = JsonConvert.DeserializeObject<T>(json) ?? throw new InvalidOperationException("Deserialization returned null");
        }
    }

    private static void LoadApWorld<T>(ref T data, Action<T>? clearAction = null)
    {
        if (clearAction != null && data != null)
        {
            clearAction(data);
        }

        string json;

        var resourceName = "ArchipelagoSphereTracker.APWorldList.json";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
                          ?? throw new FileNotFoundException($"Ressource embarquée introuvable: {resourceName}");

        using var reader = new StreamReader(stream);
        json = reader.ReadToEnd();

        data = JsonConvert.DeserializeObject<T>(json) ?? throw new InvalidOperationException("Deserialization returned null");
    }


    private static void SaveData<T>(string filePath, T data, Formatting formatting = Formatting.None)
    {
        var json = JsonConvert.SerializeObject(data, formatting);
        File.WriteAllText(filePath, json);
    }

    public static void LoadDisplayedItems() => LoadData(Declare.DisplayedItemsFile, ref Declare.DisplayedItems);

    public static void SaveDisplayedItems() => SaveData(Declare.DisplayedItemsFile, Declare.DisplayedItems);

    public static void LoadReceiverAliases() => LoadData(Declare.AliasFile, ref Declare.ReceiverAliases, data => data.Guild.Clear());

    public static void SaveReceiverAliases() => SaveData(Declare.AliasFile, Declare.ReceiverAliases, Formatting.Indented);

    public static void LoadUrlAndChannel() => LoadData(Declare.UrlChannelFile, ref Declare.ChannelAndUrl, data => data.Guild.Clear());

    public static void SaveChannelAndUrl() => SaveData(Declare.UrlChannelFile, Declare.ChannelAndUrl, Formatting.Indented);

    public static void SaveRecapList() => SaveData(Declare.RecapListFile, Declare.RecapList);

    public static void LoadRecapList() => LoadData(Declare.RecapListFile, ref Declare.RecapList, data => data.Guild.Clear());

    public static void SaveAliasChoices() => SaveData(Declare.AliasChoicesFile, Declare.AliasChoices);

    public static void LoadAliasChoices() => LoadData(Declare.AliasChoicesFile, ref Declare.AliasChoices, data => data.Guild.Clear());

    public static void SaveGameStatus() => SaveData(Declare.GameStatusFile, Declare.GameStatus);

    public static void LoadGameStatus() => LoadData(Declare.GameStatusFile, ref Declare.GameStatus, data => data.Guild.Clear());

    public static void LoadHintStatus() => LoadData(Declare.HintStatusFile, ref Declare.HintStatuses, data => data.Guild.Clear());

    public static void SaveHintStatus() => SaveData(Declare.HintStatusFile, Declare.HintStatuses);

    public static void LoadApWorldJsonList() => LoadApWorld(ref Declare.ApworldsInfo);
}
