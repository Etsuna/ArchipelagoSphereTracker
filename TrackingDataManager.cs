using HtmlAgilityPack;
using System;
using System.Net;
using System.Threading.Channels;

public static class TrackingDataManager
{
    public static void StartTracking()
    {
        if (Declare.cts != null)
        {
            Declare.cts.Cancel();
        }

        Declare.cts = new CancellationTokenSource();
        var token = Declare.cts.Token;

        Task.Run(async () =>
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    if (Declare.ChannelAndUrl.Count == 0)
                    {
                        Console.WriteLine("Aucune URL définie. Arrêt du suivi.");
                        Declare.serviceRunning = false;
                        break;
                    }

                    Declare.serviceRunning = true;

                    foreach (var guild in Declare.ChannelAndUrl.Keys)
                    {
                        var guildCheck = Declare.client.GetGuild(ulong.Parse(guild));
                        if (guildCheck != null)
                        {
                            foreach (var urls in Declare.ChannelAndUrl[guild])
                            {
                                var channel = urls.Key;
                                var url = urls.Value;
                                var channelCheck = guildCheck.GetChannel(ulong.Parse(channel));
                                if (channelCheck != null)
                                {
                                    Console.WriteLine($"Le salon existe toujours : {channelCheck.Name}");
                                    
                                    await setAliasAndGameStatusAsync(guild, channel, url);
                                    await checkGameStatus(guild, channel, url);
                                    await GetTableDataAsync(guild, channel, url);
                                }
                                else
                                {
                                    Console.WriteLine("Le salon n'existe plus, Suppression des informations.");
                                    await BotCommands.DeleteChannelAndUrl(channel, guild);
                                }
                            }
                        }
                        else
                        {
                            Console.WriteLine("Serveur introuvable, Suppression des informations.");
                            await BotCommands.DeleteChannelAndUrl(null, guild);
                        }
                    }
                    
                    await Task.Delay(60000, token);
                }
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine("Suivi annulé.");
            }
        }, token);
    }

    public static async Task GetTableDataAsync(string guild, string channel, string url, bool displayItem = true)
    {
        bool isUpdated = false;

        var clientHttp = new HttpClient();
        var html = await clientHttp.GetStringAsync(url);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var rows = doc.DocumentNode.SelectNodes("//table//tr");
        if (rows == null) return;

        EnsureDictionaryStructureTableDataAsync(guild, channel);

        foreach (var row in rows)
        {
            var cells = row.SelectNodes("td");
            if (cells?.Count != 6) continue;

            var newItem = new displayedItemsElement
            {
                sphere = WebUtility.HtmlDecode(cells[0].InnerText.Trim()),
                finder = WebUtility.HtmlDecode(cells[1].InnerText.Trim()),
                receiver = WebUtility.HtmlDecode(cells[2].InnerText.Trim()),
                item = WebUtility.HtmlDecode(cells[3].InnerText.Trim()),
                location = WebUtility.HtmlDecode(cells[4].InnerText.Trim()),
                game = WebUtility.HtmlDecode(cells[5].InnerText.Trim())
            };

            if (!IsItemExists(guild, channel, newItem))
            {
                isUpdated = true;
                Declare.displayedItems[guild][channel].Add(newItem);
                string message = BuildMessage(guild, channel, newItem);

                if (File.Exists(Declare.displayedItemsFile))
                {
                    UpdateRecapList(guild, channel, newItem.receiver, newItem.item);
                }

                if (displayItem)
                {
                    await BotCommands.SendMessageAsync(message, channel);
                }
            }
        }

        if (!File.Exists(Declare.displayedItemsFile) || isUpdated || displayItem)
        {
            DataManager.SaveRecapList();
            DataManager.SaveDisplayedItems();
        }
    }

    private static void EnsureDictionaryStructureTableDataAsync(string guild, string channel)
    {
        if (!Declare.displayedItems.ContainsKey(guild))
            Declare.displayedItems[guild] = new Dictionary<string, List<displayedItemsElement>>();

        if (!Declare.displayedItems[guild].ContainsKey(channel))
            Declare.displayedItems[guild][channel] = new List<displayedItemsElement>();

        if (!Declare.receiverAliases.ContainsKey(guild))
            Declare.receiverAliases[guild] = new Dictionary<string, Dictionary<string, string>>();

        if (!Declare.receiverAliases[guild].ContainsKey(channel))
            Declare.receiverAliases[guild][channel] = new Dictionary<string, string>();
    }

    private static bool IsItemExists(string guild, string channel, displayedItemsElement newItem)
    {
        return Declare.displayedItems[guild][channel].Any(x =>
            x.sphere == newItem.sphere &&
            x.finder == newItem.finder &&
            x.receiver == newItem.receiver &&
            x.item == newItem.item &&
            x.location == newItem.location &&
            x.game == newItem.game);
    }

    private static string BuildMessage(string guild, string channel, displayedItemsElement item)
    {
        if (item.finder.Equals(item.receiver))
        {
            return $"{item.finder} found their {item.item} ({item.location})";
        }

        if (Declare.receiverAliases[guild][channel].TryGetValue(item.receiver, out string userId))
        {
            return $"{item.finder} sent {item.item} to <@{userId}> {item.receiver} ({item.location})";
        }

        return $"{item.finder} sent {item.item} to {item.receiver} ({item.location})";
    }

    private static void UpdateRecapList(string guild, string channel, string receiver, string item)
    {
        if (!Declare.recapList.ContainsKey(guild))
            Declare.recapList[guild] = new Dictionary<string, Dictionary<string, List<SubElement>>>();

        if (!Declare.recapList[guild].ContainsKey(channel))
            Declare.recapList[guild][channel] = new Dictionary<string, List<SubElement>>();

        if (!Declare.receiverAliases[guild][channel].TryGetValue(receiver, out string userId))
            return;

        if (!Declare.recapList[guild][channel].TryGetValue(userId, out var userItems))
        {
            Declare.recapList[guild][channel][userId] = new List<SubElement> {
            new SubElement { SubKey = receiver, Values = new List<string> { item } }
        };
        }
        else
        {
            var itemToAdd = userItems.Find(e => e.SubKey == receiver);
            if (itemToAdd == null)
            {
                userItems.Add(new SubElement { SubKey = receiver, Values = new List<string> { item } });
            }
            else
            {
                itemToAdd.Values.Add(item);
                itemToAdd.Values.Remove("Aucun élément");
            }
        }
    }

    public static async Task setAliasAndGameStatusAsync(string guild, string channel, string url)
    {
        if (Declare.aliasChoices.TryGetValue(guild, out var guildAliases) &&
            guildAliases.ContainsKey(channel))
        {
            return;
        }

        var clientHttp = new HttpClient();
        Declare.urlTracker = url.Replace("sphere_", "");

        var html = await clientHttp.GetStringAsync(Declare.urlTracker);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var table = doc.DocumentNode.SelectNodes("//table")?.FirstOrDefault();
        var rows = table?.SelectNodes(".//tr")?.Skip(1); 

        if (rows == null) return;

        EnsureDictionaryStructureAliasAndGameStatus(guild, channel);

        foreach (var row in rows)
        {
            var cells = row.SelectNodes("td") ?? row.SelectNodes("th");
            if (cells == null || cells.Count != 7) continue;

            var newGameStatus = new gameStatus
            {
                hachtag = WebUtility.HtmlDecode(cells[0].InnerText.Trim()),
                name = WebUtility.HtmlDecode(cells[1].InnerText.Trim()),
                game = WebUtility.HtmlDecode(cells[2].InnerText.Trim()),
                status = WebUtility.HtmlDecode(cells[3].InnerText.Trim()),
                checks = WebUtility.HtmlDecode(cells[4].InnerText.Trim()),
                pourcent = WebUtility.HtmlDecode(cells[5].InnerText.Trim()),
                lastActivity = WebUtility.HtmlDecode(cells[6].InnerText.Trim())
            };

            Declare.aliasChoices[guild][channel].TryAdd(newGameStatus.name, newGameStatus.name);

            if (!Declare.gameStatus[guild][channel].Any(x => x.name == newGameStatus.name))
            {
                Declare.gameStatus[guild][channel].Add(newGameStatus);
            }
        }

        Declare.gameStatus[guild][channel].Sort((x, y) => x.name.CompareTo(y.name));

        DataManager.SaveAliasChoices();
        DataManager.SaveGameStatus();
        await BotCommands.RegisterCommandsAsync();
        await BotCommands.SendMessageAsync("Aliases Updated!", channel);
    }

    private static void EnsureDictionaryStructureAliasAndGameStatus(string guild, string channel)
    {
        if (!Declare.aliasChoices.ContainsKey(guild))
            Declare.aliasChoices[guild] = new Dictionary<string, Dictionary<string, string>>();

        if (!Declare.aliasChoices[guild].ContainsKey(channel))
            Declare.aliasChoices[guild][channel] = new Dictionary<string, string>();

        if (!Declare.gameStatus.ContainsKey(guild))
            Declare.gameStatus[guild] = new Dictionary<string, List<gameStatus>>();

        if (!Declare.gameStatus[guild].ContainsKey(channel))
            Declare.gameStatus[guild][channel] = new List<gameStatus>();
    }

    public static async Task checkGameStatus(string guild, string channel, string url)
    {
        DataManager.LoadGameStatus();
        bool changeFound = false;

        var clientHttp = new HttpClient();
        Declare.urlTracker = url.Replace("sphere_", "");

        var html = await clientHttp.GetStringAsync(Declare.urlTracker);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var tables = doc.DocumentNode.SelectNodes("//table");
        if (tables == null || tables.Count < 2) return;

        EnsureDictionaryStructureHintTable(guild, channel);

        changeFound |= ProcessGameStatusTable(tables[0], guild, channel);
        if (changeFound) DataManager.SaveGameStatus();

        changeFound = ProcessHintTable(tables[1], guild, channel);
        if (changeFound) DataManager.SaveHintStatus();
    }

    private static bool ProcessGameStatusTable(HtmlNode gameStatusTable, string guild, string channel)
    {
        var rows = gameStatusTable.SelectNodes(".//tr")?.Skip(1);
        if (rows == null) return false;

        bool changeFound = false;

        foreach (var row in rows)
        {
            var cells = row.SelectNodes("td") ?? row.SelectNodes("th");
            if (cells == null || cells.Count != 7) continue;

            var newEntry = new gameStatus
            {
                hachtag = WebUtility.HtmlDecode(cells[0].InnerText.Trim()),
                name = WebUtility.HtmlDecode(cells[1].InnerText.Trim()),
                game = WebUtility.HtmlDecode(cells[2].InnerText.Trim()),
                status = WebUtility.HtmlDecode(cells[3].InnerText.Trim()),
                checks = WebUtility.HtmlDecode(cells[4].InnerText.Trim()),
                pourcent = WebUtility.HtmlDecode(cells[5].InnerText.Trim()),
                lastActivity = WebUtility.HtmlDecode(cells[6].InnerText.Trim())
            };

            var existingStatus = Declare.gameStatus[guild][channel].FirstOrDefault(x => x.name == newEntry.name);

            if (existingStatus == null)
            {
                Declare.gameStatus[guild][channel].Add(newEntry);
                changeFound = true;
            }
            else if (existingStatus.status != "Goal Completed")
            {
                if (existingStatus.pourcent != newEntry.pourcent)
                {
                    existingStatus.checks = newEntry.checks;
                    existingStatus.pourcent = newEntry.pourcent;
                    changeFound = true;
                }

                if (newEntry.pourcent == "100.00" || newEntry.status == "Goal Complete")
                {
                    string allAliases = string.Join(", ", Declare.receiverAliases.Values.Distinct().Select(alias => $"<@{alias}>"));
                    BotCommands.SendMessageAsync($"{allAliases}\n{newEntry.name} has completed their goal for this game: {newEntry.game}!", channel).Wait();

                    existingStatus.status = "Goal Completed";
                    existingStatus.pourcent = "100.00";
                    changeFound = true;
                }
            }
        }

        return changeFound;
    }

    private static bool ProcessHintTable(HtmlNode hintTable, string guild, string channel)
    {
        var rows = hintTable.SelectNodes(".//tr")?.Skip(1);
        if (rows == null) return false;

        bool changeFound = false;

        foreach (var row in rows)
        {
            var cells = row.SelectNodes("td") ?? row.SelectNodes("th");
            if (cells == null || cells.Count != 7) continue;

            var newHint = new hintStatus
            {
                finder = WebUtility.HtmlDecode(cells[0].InnerText.Trim()),
                receiver = WebUtility.HtmlDecode(cells[1].InnerText.Trim()),
                item = WebUtility.HtmlDecode(cells[2].InnerText.Trim()),
                location = WebUtility.HtmlDecode(cells[3].InnerText.Trim()),
                game = WebUtility.HtmlDecode(cells[4].InnerText.Trim()),
                entrance = WebUtility.HtmlDecode(cells[5].InnerText.Trim()),
                found = WebUtility.HtmlDecode(cells[6].InnerText.Trim())
            };

            var existingHint = Declare.hintStatuses[guild][channel]
                .FirstOrDefault(x => x.finder == newHint.finder && x.receiver == newHint.receiver &&
                                     x.item == newHint.item && x.location == newHint.location &&
                                     x.game == newHint.game && x.entrance == newHint.entrance);

            if (existingHint == null && string.IsNullOrEmpty(newHint.found))
            {
                Declare.hintStatuses[guild][channel].Add(newHint);
                changeFound = true;
            }
            else if (existingHint != null && !string.IsNullOrEmpty(newHint.found))
            {
                Declare.hintStatuses[guild][channel].Remove(existingHint);
                changeFound = true;
            }
        }

        return changeFound;
    }

    private static void EnsureDictionaryStructureHintTable(string guild, string channel)
    {
        if (!Declare.gameStatus.ContainsKey(guild))
            Declare.gameStatus[guild] = new Dictionary<string, List<gameStatus>>();

        if (!Declare.gameStatus[guild].ContainsKey(channel))
            Declare.gameStatus[guild][channel] = new List<gameStatus>();

        if (!Declare.hintStatuses.ContainsKey(guild))
            Declare.hintStatuses[guild] = new Dictionary<string, List<hintStatus>>();

        if (!Declare.hintStatuses[guild].ContainsKey(channel))
            Declare.hintStatuses[guild][channel] = new List<hintStatus>();
    }

}
