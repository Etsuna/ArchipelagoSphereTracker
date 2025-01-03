using Newtonsoft.Json;

public static class DataManager
{
    public static void LoadDisplayedItems()
    {
        if (File.Exists(Declare.displayedItemsFile))
        {
            var json = File.ReadAllText(Declare.displayedItemsFile);
            Declare.displayedItems = JsonConvert.DeserializeObject<List<displayedItemsElement>>(json);
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
            Declare.receiverAliases = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
        }
    }

    public static void SaveReceiverAliases()
    {
        var json = JsonConvert.SerializeObject(Declare.receiverAliases, Formatting.Indented);
        File.WriteAllText(Declare.aliasFile, json);
    }

    public static void LoadUrlAndChannel()
    {
        Declare.url = string.Empty;
        if (File.Exists(Declare.urlChannelFile))
        {
            var json = File.ReadAllText(Declare.urlChannelFile);
            var data = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
            Declare.url = data.GetValueOrDefault("url", string.Empty);
            Declare.channelId = ulong.TryParse(data.GetValueOrDefault("channelId", "0"), out var id) ? id : 0;
        }
    }

    public static void SaveUrlAndChannel()
    {
        var data = new Dictionary<string, string>
        {
            { "url", Declare.url },
            { "channelId", Declare.channelId.ToString() }
        };
        var json = JsonConvert.SerializeObject(data);
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
            Declare.recapList = JsonConvert.DeserializeObject<Dictionary<string, List<SubElement>>>(json);
        }
    }

    public static void AddMissingRecapUser()
    {
        LoadRecapList();

        foreach (var alias in Declare.receiverAliases)
        {
            var receiverId = alias.Value;
            if (!Declare.recapList.ContainsKey(receiverId))
            {
                Declare.recapList[receiverId] = new List<SubElement>();
            }

            var recapUser = Declare.recapList[receiverId].Find(e => e.SubKey == alias.Key);
            if (recapUser == null)
            {
                Declare.recapList[receiverId].Add(new SubElement
                {
                    SubKey = alias.Key,
                    Values = new List<string> { "Aucun élément" }
                });
            }
            SaveRecapList();
        }
    }
}
