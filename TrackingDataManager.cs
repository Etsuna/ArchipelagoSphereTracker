using Discord;
using Discord.WebSocket;
using HtmlAgilityPack;
using System.Globalization;
using System.Net;

public static class TrackingDataManager
{
    public static void StartTracking()
    {
        if (Declare.Cts != null)
        {
            Declare.Cts.Cancel();
        }

        Declare.Cts = new CancellationTokenSource();
        var token = Declare.Cts.Token;

        Task.Run(async () =>
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    Declare.ServiceRunning = true;

                    foreach (var guild in Declare.ChannelAndUrl.Guild)
                    {
                        var guildCheck = Declare.Client.GetGuild(ulong.Parse(guild.Key));
                        if (guildCheck != null)
                        {
                            foreach (var urls in guild.Value.Channel)
                            {
                                var channel = urls.Key;
                                var urlSphereTracker = urls.Value.SphereTracker;
                                var urlTracker = urls.Value.Tracker;

                                var channelCheck = guildCheck.GetChannel(ulong.Parse(channel));

                                if (channelCheck != null)
                                {
                                    Console.WriteLine($"Le salon existe toujours : {channelCheck.Name}");

                                    if (guildCheck.GetChannel(ulong.Parse(channel)) is SocketThreadChannel thread)
                                    {
                                        var messages = await thread.GetMessagesAsync(1).FlattenAsync();
                                        var lastMessage = messages.FirstOrDefault();

                                        DateTimeOffset lastActivity = lastMessage?.Timestamp ?? SnowflakeUtils.FromSnowflake(thread.Id);
                                        if (lastMessage == null)
                                        {
                                            Console.WriteLine($"Aucun message trouvé, on utilise la date de création du fil : {lastActivity}");
                                        }

                                        double daysInactive = (DateTimeOffset.UtcNow - lastActivity).TotalDays;

                                        if (daysInactive < 6)
                                        {
                                            if (Declare.WarnedThreads.Contains(thread.Id.ToString()))
                                            {
                                                await BotCommands.SendMessageAsync($"Nouveau message sur le thread {thread.Name}, suppression automatique annulée.", thread.Id.ToString());
                                                Declare.WarnedThreads.Remove(thread.Id.ToString());
                                            }
                                        }
                                        else if (daysInactive < 7)
                                        {
                                            if (!Declare.WarnedThreads.Contains(thread.Id.ToString()))
                                            {
                                                DateTimeOffset deletionDate = lastActivity.AddTicks(TimeSpan.TicksPerDay * 7);

                                                TimeZoneInfo frenchTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris");
                                                DateTimeOffset localDeletionDate = TimeZoneInfo.ConvertTime(deletionDate, frenchTimeZone);

                                                string formattedDeletionDate = localDeletionDate.ToString("dddd d MMMM yyyy à HH'h'mm", CultureInfo.GetCultureInfo("fr-FR"));

                                                await BotCommands.SendMessageAsync(
                                                    $"Aucun message depuis 6 jours. Si aucun message n'est posté avant le {formattedDeletionDate} sur le thread {thread.Name}, il sera supprimé.\nPensez à supprimer le thread quand vous n'en avez plus besoin !",
                                                    thread.ParentChannel.Id.ToString());

                                                Declare.WarnedThreads.Add(thread.Id.ToString());
                                            }
                                        }
                                        else
                                        {
                                            Console.WriteLine($"Dernière activité : {lastActivity}");
                                            Console.WriteLine("Aucune activité depuis 7 jours, suppression du thread...");
                                            await BotCommands.DeleteChannelAndUrl(channel, guild.Key);
                                            await thread.DeleteAsync();
                                            Console.WriteLine("Thread supprimé.");

                                            Declare.WarnedThreads.Remove(thread.Id.ToString());
                                            continue;
                                        }
                                    }

                                    await setAliasAndGameStatusAsync(guild.Key, channel, urlTracker);
                                    await checkGameStatus(guild.Key, channel, urlTracker);
                                    await GetTableDataAsync(guild.Key, channel, urlSphereTracker);
                                }
                                else
                                {
                                    Console.WriteLine($"Le salon n'existe plus, Suppression des informations Channel:{channel}.");
                                    await BotCommands.DeleteChannelAndUrl(channel, guild.Key);
                                    Console.WriteLine($"Suppression effectuée");
                                }
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Serveur introuvable {guild.Key}, Suppression des informations.");
                            await BotCommands.DeleteChannelAndUrl(null, guild.Key);
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
        var haveDisplayStatus = Declare.DisplayedItems.Guild.TryGetValue(guild, out var guildItemSave) && guildItemSave.Channel.TryGetValue(channel, out var channelGamesSave);

        using var clientHttp = new HttpClient();
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
                Declare.DisplayedItems.Guild[guild].Channel[channel].Add(newItem);
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

        if (!haveDisplayStatus)
        {
            await BotCommands.SendMessageAsync("BOT Ready!", channel);
        }
    }


    private static void EnsureDictionaryStructureTableDataAsync(string guild, string channel)
    {
        if (!Declare.DisplayedItems.Guild.TryGetValue(guild, out var channelDisplayedItem))
        {
            channelDisplayedItem = new ChannelDisplayedItem();
            Declare.DisplayedItems.Guild[guild] = channelDisplayedItem;
        }

        if (!channelDisplayedItem.Channel.ContainsKey(channel))
        {
            channelDisplayedItem.Channel[channel] = new List<DisplayedItem>();
        }

        if (!Declare.ReceiverAliases.Guild.TryGetValue(guild, out var channelReceiverAliases))
        {
            channelReceiverAliases = new ChannelReceiverAliases();
            Declare.ReceiverAliases.Guild[guild] = channelReceiverAliases;
        }

        if (!channelReceiverAliases.Channel.ContainsKey(channel))
        {
            channelReceiverAliases.Channel[channel] = new ReceiverAlias();
        }
    }


    private static bool IsItemExists(string guild, string channel, DisplayedItem newItem)
    {
        if (!Declare.DisplayedItems.Guild.TryGetValue(guild, out var channelItems) ||
            !channelItems.Channel.TryGetValue(channel, out var items))
        {
            return false;
        }

        foreach (var item in items)
        {
            if (item.Sphere == newItem.Sphere &&
                item.Finder == newItem.Finder &&
                item.Receiver == newItem.Receiver &&
                item.Item == newItem.Item &&
                item.Location == newItem.Location &&
                item.Game == newItem.Game)
            {
                return true;
            }
        }

        return false;
    }


    private static string BuildMessage(string guild, string channel, DisplayedItem item)
    {
        if (item.Finder == item.Receiver)
        {
            return $"{item.Finder} found their {item.Item} ({item.Location})";
        }

        if (Declare.ReceiverAliases.Guild[guild].Channel[channel].receiverAlias.TryGetValue(item.Receiver, out var userIds))
        {
            var mentions = string.Join(" ", userIds.Select(id => $"<@{id}>"));
            return $"{item.Finder} sent {item.Item} to {mentions} {item.Receiver} ({item.Location})";
        }

        return $"{item.Finder} sent {item.Item} to {item.Receiver} ({item.Location})";
    }


    private static void UpdateRecapList(string guild, string channel, string receiver, string item)
    {
        if (!Declare.RecapList.Guild.TryGetValue(guild, out var channelRecapList))
        {
            channelRecapList = new ChannelRecapList();
            Declare.RecapList.Guild[guild] = channelRecapList;
        }

        if (!channelRecapList.Channel.TryGetValue(channel, out var userRecapList))
        {
            userRecapList = new UserRecapList();
            channelRecapList.Channel[channel] = userRecapList;
        }

        if (!Declare.ReceiverAliases.Guild.TryGetValue(guild, out var channelReceiverAliases) ||
            !channelReceiverAliases.Channel.TryGetValue(channel, out var receiverAlias) ||
            !receiverAlias.receiverAlias.TryGetValue(receiver, out List<string> userIds))
        {
            return;
        }

        foreach (var userId in userIds)
        {
            if (!userRecapList.Aliases.TryGetValue(userId, out var userItems))
            {
                userItems = new List<RecapList> { new RecapList { Alias = receiver, Items = new List<string> { item } } };
                userRecapList.Aliases[userId] = userItems;
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

    public static async Task setAliasAndGameStatusAsync(string guild, string channel, string urlTracker)
    {
        if (Declare.AliasChoices.Guild.TryGetValue(guild, out var guildAliases) &&
            guildAliases.Channel.ContainsKey(channel))
        {
            return;
        }

        using var clientHttp = new HttpClient();
        var html = await clientHttp.GetStringAsync(urlTracker);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var rows = doc.DocumentNode.SelectNodes("//table//tr")?.Skip(1);
        if (rows == null) return;

        EnsureDictionaryStructureAliasAndGameStatus(guild, channel);

        var newGameStatuses = new List<GameStatus>();
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

            Declare.AliasChoices.Guild[guild].Channel[channel].aliasChoices.TryAdd(newGameStatus.Name, newGameStatus.Name);

            if (!Declare.GameStatus.Guild[guild].Channel[channel].Any(x => x.Name == newGameStatus.Name))
            {
                newGameStatuses.Add(newGameStatus);
            }
        }

        if (newGameStatuses.Count > 0)
        {
            Declare.GameStatus.Guild[guild].Channel[channel].AddRange(newGameStatuses);
            Declare.GameStatus.Guild[guild].Channel[channel].Sort((x, y) => x.Name.CompareTo(y.Name));
            DataManager.SaveAliasChoices();
            DataManager.SaveGameStatus();
            await BotCommands.RegisterCommandsAsync();
            await BotCommands.SendMessageAsync("Aliases Updated!", channel);
        }

        if (Declare.ChannelAndUrl.Guild[guild].Channel[channel].Aliases.Any())
        {
            foreach (var alias in Declare.ChannelAndUrl.Guild[guild].Channel[channel].Aliases.Keys)
            {
                var gameName = Declare.ChannelAndUrl.Guild[guild].Channel[channel].Aliases[alias].GameName;
                var patch = Declare.ChannelAndUrl.Guild[guild].Channel[channel].Aliases[alias].Patch;
                await BotCommands.SendMessageAsync($"Patch Pour {alias}, {gameName} : {patch}", channel);
            }
        }
    }
    private static void EnsureDictionaryStructureAliasAndGameStatus(string guild, string channel)
    {
        if (!Declare.AliasChoices.Guild.TryGetValue(guild, out var aliasChoices))
        {
            aliasChoices = new ChannelAliasChoices();
            Declare.AliasChoices.Guild[guild] = aliasChoices;
        }

        if (!aliasChoices.Channel.ContainsKey(channel))
        {
            aliasChoices.Channel[channel] = new AliasChoice();
        }

        if (!Declare.GameStatus.Guild.TryGetValue(guild, out var gameStatus))
        {
            gameStatus = new ChannelGameStatus();
            Declare.GameStatus.Guild[guild] = gameStatus;
        }

        if (!gameStatus.Channel.ContainsKey(channel))
        {
            gameStatus.Channel[channel] = new List<GameStatus>();
        }
    }


    public static async Task checkGameStatus(string guild, string channel, string urlTracker)
    {
        using var clientHttp = new HttpClient();
        var html = await clientHttp.GetStringAsync(urlTracker);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var tables = doc.DocumentNode.SelectNodes("//table");
        if (tables == null || tables.Count < 2) return;

        EnsureDictionaryStructureHintTable(guild, channel);

        bool changeFound = ProcessGameStatusTable(tables[0], guild, channel);
        if (changeFound) DataManager.SaveGameStatus();

        changeFound = ProcessHintTable(tables[1], guild, channel);
        if (changeFound) DataManager.SaveHintStatus();
    }

    private static bool ProcessGameStatusTable(HtmlNode gameStatusTable, string guild, string channel)
    {
        var rows = gameStatusTable.SelectNodes(".//tr")?.Skip(1);
        if (rows == null) return false;

        bool changeFound = false;
        var gameStatusList = Declare.GameStatus.Guild[guild].Channel[channel];

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

            var existingStatus = gameStatusList.FirstOrDefault(x => x.Name == newEntry.Name);

            if (existingStatus == null)
            {
                gameStatusList.Add(newEntry);
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
        var hintStatuses = Declare.HintStatuses.Guild[guild].Channel[channel];

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

            var existingHint = hintStatuses.FirstOrDefault(x => x.Finder == newHint.Finder && x.Receiver == newHint.Receiver &&
                                                                x.Item == newHint.Item && x.Location == newHint.Location &&
                                                                x.Game == newHint.Game && x.Entrance == newHint.Entrance);

            if (existingHint == null && string.IsNullOrEmpty(newHint.Found))
            {
                hintStatuses.Add(newHint);
                changeFound = true;
            }
            else if (existingHint != null && !string.IsNullOrEmpty(newHint.Found))
            {
                hintStatuses.Remove(existingHint);
                changeFound = true;
            }
        }

        return changeFound;
    }


    private static void EnsureDictionaryStructureHintTable(string guild, string channel)
    {
        if (!Declare.GameStatus.Guild.TryGetValue(guild, out var gameStatus))
        {
            gameStatus = new ChannelGameStatus();
            Declare.GameStatus.Guild[guild] = gameStatus;
        }

        if (!gameStatus.Channel.ContainsKey(channel))
        {
            gameStatus.Channel[channel] = new List<GameStatus>();
        }

        if (!Declare.HintStatuses.Guild.TryGetValue(guild, out var hintStatus))
        {
            hintStatus = new ChannelHintStatus();
            Declare.HintStatuses.Guild[guild] = hintStatus;
        }

        if (!hintStatus.Channel.ContainsKey(channel))
        {
            hintStatus.Channel[channel] = new List<HintStatus>();
        }
    }

}
