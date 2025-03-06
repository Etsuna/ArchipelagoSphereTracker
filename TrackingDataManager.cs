using HtmlAgilityPack;
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
                    Declare.serviceRunning = true;

                    foreach (var guild in Declare.ChannelAndUrl.Guild.Keys)
                    {
                        var guildCheck = Declare.client.GetGuild(ulong.Parse(guild));
                        if (guildCheck != null)
                        {
                            foreach (var urls in Declare.ChannelAndUrl.Guild[guild].Channel)
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
                                    Console.WriteLine($"Le salon n'existe plus, Suppression des informations Channel:{channel}.");
                                    await BotCommands.DeleteChannelAndUrl(channel, guild);
                                    Console.WriteLine($"Suppression effectuée");
                                }
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Serveur introuvable {guild}, Suppression des informations.");
                            await BotCommands.DeleteChannelAndUrl(null, guild);
                            Console.WriteLine($"Suppression effectuée");
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

    public static async Task GetTableDataAsync(string guild, string channel, string url)
    {
        bool isUpdated = false;
        var haveDisplayStatus = Declare.displayedItems.Guild.TryGetValue(guild, out var guildItemSave) && guildItemSave.Channel.TryGetValue(channel, out var channelGamesSave);

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

            var newItem = new DisplayedItem
            {
                Sphere = WebUtility.HtmlDecode(cells[0].InnerText.Trim()),
                Finder = WebUtility.HtmlDecode(cells[1].InnerText.Trim()),
                Receiver = WebUtility.HtmlDecode(cells[2].InnerText.Trim()),
                Item = WebUtility.HtmlDecode(cells[3].InnerText.Trim()),
                Location = WebUtility.HtmlDecode(cells[4].InnerText.Trim()),
                Game = WebUtility.HtmlDecode(cells[5].InnerText.Trim())
            };

            if (!IsItemExists(guild, channel, newItem))
            {
                isUpdated = true;
                Declare.displayedItems.Guild[guild].Channel[channel].Add(newItem);
                string message = BuildMessage(guild, channel, newItem);

                UpdateRecapList(guild, channel, newItem.Receiver, newItem.Item);

                if (haveDisplayStatus)
                {
                    await BotCommands.SendMessageAsync(message, channel);
                }
            }
        }

        if (isUpdated || !haveDisplayStatus)
        {
            DataManager.SaveRecapList();
            DataManager.SaveDisplayedItems();
        }

        if(!haveDisplayStatus)
        {
            await BotCommands.SendMessageAsync("BOT Ready!", channel);
        }
    }

    private static void EnsureDictionaryStructureTableDataAsync(string guild, string channel)
    {
        if (!Declare.displayedItems.Guild.ContainsKey(guild))
            Declare.displayedItems.Guild[guild] = new ChannelDisplayedItem();

        if (!Declare.displayedItems.Guild[guild].Channel.ContainsKey(channel))
            Declare.displayedItems.Guild[guild].Channel[channel] = new List<DisplayedItem>();

        if (!Declare.receiverAliases.Guild.ContainsKey(guild))
            Declare.receiverAliases.Guild[guild] = new ChannelReceiverAliases();

        if (!Declare.receiverAliases.Guild[guild].Channel.ContainsKey(channel))
            Declare.receiverAliases.Guild[guild].Channel[channel] = new ReceiverAlias();
    }

    private static bool IsItemExists(string guild, string channel, DisplayedItem newItem)
    {
        return Declare.displayedItems.Guild[guild].Channel[channel].Any(x =>
            x.Sphere == newItem.Sphere &&
            x.Finder == newItem.Finder &&
            x.Receiver == newItem.Receiver &&
            x.Item == newItem.Item &&
            x.Location == newItem.Location &&
            x.Game == newItem.Game);
    }

    private static string BuildMessage(string guild, string channel, DisplayedItem item)
    {
        if (item.Finder.Equals(item.Receiver))
        {
            return $"{item.Finder} found their {item.Item} ({item.Location})";
        }

        if (Declare.receiverAliases.Guild[guild].Channel[channel].receiverAlias.TryGetValue(item.Item, out List<string> userIds))
        {
            string mentions = string.Join(" ", userIds.Select(id => $"<@{id}>"));

            return $"{item.Finder} sent {item.Item} to {mentions} {item.Receiver} ({item.Location})";
        }


        return $"{item.Finder} sent {item.Item} to {item.Receiver} ({item.Location})";
    }

    private static void UpdateRecapList(string guild, string channel, string receiver, string item)
    {
        if (!Declare.recapList.Guild.ContainsKey(guild))
            Declare.recapList.Guild[guild] = new ChannelRecapList();

        if (!Declare.recapList.Guild[guild].Channel.ContainsKey(channel))
            Declare.recapList.Guild[guild].Channel[channel] = new UserRecapList();

        if (!Declare.receiverAliases.Guild[guild].Channel[channel].receiverAlias.TryGetValue(receiver, out List<string> userIds))
            return;

        foreach (var userId in userIds)
        {
            if (!Declare.recapList.Guild[guild].Channel[channel].Aliases.TryGetValue(userId, out var userItems))
            {
                Declare.recapList.Guild[guild].Channel[channel].Aliases[userId] = new List<RecapList> {
            new RecapList { Alias = receiver, Items = new List<string> { item } }
        };
            }
            else
            {
                var itemToAdd = userItems.Find(e => e.Alias == receiver);
                if (itemToAdd == null)
                {
                    userItems.Add(new RecapList { Alias = receiver, Items = new List<string> { item } });
                }
                else
                {
                    itemToAdd.Items.Add(item);
                    itemToAdd.Items.Remove("Aucun élément");
                }
            }
        }
    }

    public static async Task setAliasAndGameStatusAsync(string guild, string channel, string url)
    {
        if (Declare.aliasChoices.Guild.TryGetValue(guild, out var guildAliases) &&
            guildAliases.Channel.ContainsKey(channel))
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

            var newGameStatus = new GameStatus
            {
                Hashtag = WebUtility.HtmlDecode(cells[0].InnerText.Trim()),
                Name = WebUtility.HtmlDecode(cells[1].InnerText.Trim()),
                Game = WebUtility.HtmlDecode(cells[2].InnerText.Trim()),
                Status = WebUtility.HtmlDecode(cells[3].InnerText.Trim()),
                Checks = WebUtility.HtmlDecode(cells[4].InnerText.Trim()),
                Percent = WebUtility.HtmlDecode(cells[5].InnerText.Trim()),
                LastActivity = WebUtility.HtmlDecode(cells[6].InnerText.Trim())
            };

            Declare.aliasChoices.Guild[guild].Channel[channel].aliasChoices.TryAdd(newGameStatus.Name, newGameStatus.Name);

            if (!Declare.gameStatus.Guild[guild].Channel[channel].Any(x => x.Name == newGameStatus.Name))
            {
                Declare.gameStatus.Guild[guild].Channel[channel].Add(newGameStatus);
            }
        }

        Declare.gameStatus.Guild[guild].Channel[channel].Sort((x, y) => x.Name.CompareTo(y.Name));

        DataManager.SaveAliasChoices();
        DataManager.SaveGameStatus();
        await BotCommands.RegisterCommandsAsync();
        await BotCommands.SendMessageAsync("Aliases Updated!", channel);
    }

    private static void EnsureDictionaryStructureAliasAndGameStatus(string guild, string channel)
    {
        if (!Declare.aliasChoices.Guild.ContainsKey(guild))
            Declare.aliasChoices.Guild[guild] = new ChannelAliasChoices();

        if (!Declare.aliasChoices.Guild[guild].Channel.ContainsKey(channel))
            Declare.aliasChoices.Guild[guild].Channel[channel] = new AliasChoice();

        if (!Declare.gameStatus.Guild.ContainsKey(guild))
            Declare.gameStatus.Guild[guild] = new ChannelGameStatus();

        if (!Declare.gameStatus.Guild[guild].Channel.ContainsKey(channel))
            Declare.gameStatus.Guild[guild].Channel[channel] = new List<GameStatus>();
    }

    public static async Task checkGameStatus(string guild, string channel, string url)
    {
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

            var newEntry = new GameStatus
            {
                Hashtag = WebUtility.HtmlDecode(cells[0].InnerText.Trim()),
                Name = WebUtility.HtmlDecode(cells[1].InnerText.Trim()),
                Game = WebUtility.HtmlDecode(cells[2].InnerText.Trim()),
                Status = WebUtility.HtmlDecode(cells[3].InnerText.Trim()),
                Checks = WebUtility.HtmlDecode(cells[4].InnerText.Trim()),
                Percent = WebUtility.HtmlDecode(cells[5].InnerText.Trim()),
                LastActivity = WebUtility.HtmlDecode(cells[6].InnerText.Trim())
            };

             var existingStatus = Declare.gameStatus.Guild[guild].Channel[channel].FirstOrDefault(x => x.Name == newEntry.Name);

            if (existingStatus == null)
            {
                Declare.gameStatus.Guild[guild].Channel[channel].Add(newEntry);
                changeFound = true;
            }
            else if (existingStatus.Status != "Goal Completed")
            {
                if (existingStatus.Percent != newEntry.Percent)
                {
                    existingStatus.Checks = newEntry.Checks;
                    existingStatus.Percent = newEntry.Percent;
                    changeFound = true;
                }

                if (newEntry.Percent == "100.00" || newEntry.Status == "Goal Complete")
                {
                    BotCommands.SendMessageAsync($"@everyone\n{newEntry.Name} has completed their goal for this game: {newEntry.Game}!", channel).Wait();
                    existingStatus.Status = "Goal Completed";
                    existingStatus.Percent = "100.00";
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

            var newHint = new HintStatus
            {
                Finder = WebUtility.HtmlDecode(cells[0].InnerText.Trim()),
                Receiver = WebUtility.HtmlDecode(cells[1].InnerText.Trim()),
                Item = WebUtility.HtmlDecode(cells[2].InnerText.Trim()),
                Location = WebUtility.HtmlDecode(cells[3].InnerText.Trim()),
                Game = WebUtility.HtmlDecode(cells[4].InnerText.Trim()),
                Entrance = WebUtility.HtmlDecode(cells[5].InnerText.Trim()),
                Found = WebUtility.HtmlDecode(cells[6].InnerText.Trim())
            };

            var existingHint = Declare.hintStatuses.Guild[guild].Channel[channel]
                .FirstOrDefault(x => x.Finder == newHint.Finder && x.Receiver == newHint.Receiver &&
                                     x.Item == newHint.Item && x.Location == newHint.Location &&
                                     x.Game == newHint.Game && x.Entrance == newHint.Entrance);

            if (existingHint == null && string.IsNullOrEmpty(newHint.Found))
            {
                Declare.hintStatuses.Guild[guild].Channel[channel].Add(newHint);
                changeFound = true;
            }
            else if (existingHint != null && !string.IsNullOrEmpty(newHint.Found))
            {
                Declare.hintStatuses.Guild[guild].Channel[channel].Remove(existingHint);
                changeFound = true;
            }
        }

        return changeFound;
    }

    private static void EnsureDictionaryStructureHintTable(string guild, string channel)
    {
        if (!Declare.gameStatus.Guild.ContainsKey(guild))
            Declare.gameStatus.Guild[guild] = new ChannelGameStatus();

        if (!Declare.gameStatus.Guild[guild].Channel.ContainsKey(channel))
            Declare.gameStatus.Guild[guild].Channel[channel] = new List<GameStatus>();

        if (!Declare.hintStatuses.Guild.ContainsKey(guild))
            Declare.hintStatuses.Guild[guild] = new ChannelHintStatus();

        if (!Declare.hintStatuses.Guild[guild].Channel.ContainsKey(channel))
            Declare.hintStatuses.Guild[guild].Channel[channel] = new List<HintStatus>();
    }

}
