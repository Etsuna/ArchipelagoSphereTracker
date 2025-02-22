using Newtonsoft.Json;

public static class DataManager
{
    public static void LoadDisplayedItems()
    {
        if (File.Exists(Declare.displayedItemsFile))
        {
            var json = File.ReadAllText(Declare.displayedItemsFile);
            Declare.displayedItems = JsonConvert.DeserializeObject< Dictionary<string, Dictionary<string, List<displayedItemsElement>>>>(json);
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
            Declare.receiverAliases.Clear();
        }
        if (File.Exists(Declare.aliasFile))
        {
            var json = File.ReadAllText(Declare.aliasFile);
            Declare.receiverAliases = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, Dictionary<string, string>>>>(json);
        }
    }

    public static void SaveReceiverAliases()
    {
        var json = JsonConvert.SerializeObject(Declare.receiverAliases, Formatting.Indented);
        File.WriteAllText(Declare.aliasFile, json);
    }

    public static void LoadUrlAndChannel()
    {
        if (Declare.urlChannelFile != null)
        {
            Declare.ChannelAndUrl.Clear();
        }
        if (File.Exists(Declare.urlChannelFile))
        {
            var json = File.ReadAllText(Declare.urlChannelFile);
            Declare.ChannelAndUrl = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(json);
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
            Declare.recapList.Clear();
        }
        if (File.Exists(Declare.recapListFile))
        {
            var json = File.ReadAllText(Declare.recapListFile);
            Declare.recapList = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, Dictionary<string, List<SubElement>>>>>(json);
        }
    }

    public static void AddMissingRecapUser()
    {
        LoadRecapList();

        foreach (var guild in Declare.receiverAliases.Keys)
        {
            foreach (var alias in Declare.receiverAliases[guild])
            {
                var receiverId = alias.Key;

                if (!Declare.recapList.ContainsKey(guild))
                {
                    Declare.recapList[guild] = new Dictionary<string, Dictionary<string, List<SubElement>>>();
                }

                if (!Declare.recapList.ContainsKey(receiverId))
                {
                    Declare.recapList[guild][receiverId] = new Dictionary<string, List<SubElement>>();
                }

                if (!Declare.recapList[guild][receiverId].ContainsKey(alias.Key))
                {
                    Declare.recapList[guild][receiverId][alias.Key] = new List<SubElement>
                    {
                        new SubElement
                        {
                            SubKey = alias.Key,
                            Values = new List<string> { "Aucun élément" }
                        }
                    };
                }
            }
            SaveRecapList();
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
            Declare.aliasChoices.Clear();
        }
        if (File.Exists(Declare.aliasChoicesFile))
        {
            var json = File.ReadAllText(Declare.aliasChoicesFile);
            Declare.aliasChoices = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, Dictionary<string, string>>>>(json);
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
            Declare.gameStatus.Clear();
        }
        if (File.Exists(Declare.gameStatusFile))
        {
            var json = File.ReadAllText(Declare.gameStatusFile);
            Declare.gameStatus = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, List<gameStatus>>>>(json);
        }
    }

    public static void LoadHintStatus()
    {
        if (Declare.hintStatuses != null)
        {
            Declare.hintStatuses.Clear();
        }
        if (File.Exists(Declare.hintStatusFile))
        {
            var json = File.ReadAllText(Declare.hintStatusFile);
            Declare.hintStatuses = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, List<hintStatus>>>>(json);
        }
    }

    public static void SaveHintStatus()
    {
        var json = JsonConvert.SerializeObject(Declare.hintStatuses);
        File.WriteAllText(Declare.hintStatusFile, json);
    }
}
