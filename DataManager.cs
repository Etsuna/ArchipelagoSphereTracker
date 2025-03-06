using Newtonsoft.Json;

public static class DataManager
{
    public static void LoadDisplayedItems()
    {
        if (File.Exists(Declare.displayedItemsFile))
        {
            var json = File.ReadAllText(Declare.displayedItemsFile);
            Declare.displayedItems = JsonConvert.DeserializeObject<GuildDisplayedItem>(json);
        }
    }

    public static void SaveDisplayedItems()
    {
        var json = JsonConvert.SerializeObject(Declare.displayedItems);
        File.WriteAllText(Declare.displayedItemsFile, json);
    }

    public static void LoadReceiverAliases()
    {
        if (Declare.receiverAliases != null)
        {
            Declare.receiverAliases.Guild.Clear();
        }
        if (File.Exists(Declare.aliasFile))
        {
            var json = File.ReadAllText(Declare.aliasFile);
            Declare.receiverAliases = JsonConvert.DeserializeObject<GuildReceiverAliases>(json);
        }
    }

    public static void SaveReceiverAliases()
    {
        var json = JsonConvert.SerializeObject(Declare.receiverAliases, Formatting.Indented);
        File.WriteAllText(Declare.aliasFile, json);
    }

    public static void LoadUrlAndChannel()
    {
        if (Declare.ChannelAndUrl != null)
        {
            Declare.ChannelAndUrl.Guild.Clear();
        }
        if (File.Exists(Declare.urlChannelFile))
        {
            var json = File.ReadAllText(Declare.urlChannelFile);
            Declare.ChannelAndUrl = JsonConvert.DeserializeObject<GuildChannelsAndUrls>(json);
        }
    }

    public static void SaveChannelAndUrl()
    {
        var json = JsonConvert.SerializeObject(Declare.ChannelAndUrl, Formatting.Indented);
        File.WriteAllText(Declare.urlChannelFile, json);
    }

    public static void SaveRecapList()
    {
        string json = JsonConvert.SerializeObject(Declare.recapList);
        File.WriteAllText(Declare.recapListFile, json);
    }

    public static void LoadRecapList()
    {
        if (Declare.recapList != null)
        {
            Declare.recapList.Guild.Clear();
        }
        if (File.Exists(Declare.recapListFile))
        {
            var json = File.ReadAllText(Declare.recapListFile);
            Declare.recapList = JsonConvert.DeserializeObject<GuildRecapList>(json);
        }
    }

    public static void SaveAliasChoices()
    {
        var json = JsonConvert.SerializeObject(Declare.aliasChoices);
        File.WriteAllText(Declare.aliasChoicesFile, json);
    }

    public static void LoadAliasChoices()
    {
        if (Declare.aliasChoices != null)
        {
            Declare.aliasChoices.Guild.Clear();
        }
        if (File.Exists(Declare.aliasChoicesFile))
        {
            var json = File.ReadAllText(Declare.aliasChoicesFile);
            Declare.aliasChoices = JsonConvert.DeserializeObject<GuildAliasChoices>(json);
        }
    }

    public static void SaveGameStatus()
    {
        var json = JsonConvert.SerializeObject(Declare.gameStatus);
        File.WriteAllText(Declare.gameStatusFile, json);
    }

    public static void LoadGameStatus()
    {
        if (Declare.gameStatus != null)
        {
            Declare.gameStatus.Guild.Clear();
        }
        if (File.Exists(Declare.gameStatusFile))
        {
            var json = File.ReadAllText(Declare.gameStatusFile);
            Declare.gameStatus = JsonConvert.DeserializeObject<GuildGameStatus>(json);
        }
    }

    public static void LoadHintStatus()
    {
        if (Declare.hintStatuses != null)
        {
            Declare.hintStatuses.Guild.Clear();
        }
        if (File.Exists(Declare.hintStatusFile))
        {
            var json = File.ReadAllText(Declare.hintStatusFile);
            Declare.hintStatuses = JsonConvert.DeserializeObject<GuildHintStatus>(json);
        }
    }

    public static void SaveHintStatus()
    {
        var json = JsonConvert.SerializeObject(Declare.hintStatuses);
        File.WriteAllText(Declare.hintStatusFile, json);
    }
}
