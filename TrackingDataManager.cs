using Discord;
using Discord.WebSocket;
using System.Globalization;
using System.Net;
using System.Web;
using System.Text.RegularExpressions;

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

                    var getAllGuild = await DatabaseCommands.GetAllGuildsAsync("ChannelsAndUrlsTable");

                    foreach (var guild in getAllGuild)
                    {
                        var guildCheck = Declare.Client.GetGuild(ulong.Parse(guild));
                        if (guildCheck != null)
                        {
                            var getAllChannel = await DatabaseCommands.GetAllChannelsAsync(guild, "ChannelsAndUrlsTable");

                            foreach (var channel in getAllChannel)
                            {
                                var (urlTracker, urlSphereTracker, room, silent) = await ChannelsAndUrlsCommands.GetTrackerUrlsAsync(guild, channel);

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
                                            await DatabaseCommands.DeleteChannelDataAsync(guild, channel);
                                            await thread.DeleteAsync();
                                            Console.WriteLine("Thread supprimé.");

                                            Declare.WarnedThreads.Remove(thread.Id.ToString());
                                            continue;
                                        }
                                    }

                                    await setAliasAndGameStatusAsync(guild, channel, urlTracker, silent);
                                    await checkGameStatus(guild, channel, urlTracker, silent);
                                    await GetTableDataAsync(guild, channel, urlSphereTracker, silent);
                                }
                                else
                                {
                                    Console.WriteLine($"Le salon n'existe plus, Suppression des informations Channel:{channel}.");
                                    await DatabaseCommands.DeleteChannelDataAsync(guild, channel);
                                    Console.WriteLine($"Suppression effectuée");
                                }
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Serveur introuvable {guild}, Suppression des informations.");
                            await DatabaseCommands.DeleteChannelDataByGuildIdAsync(guild);
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

    public static async Task GetTableDataAsync(string guild, string channel, string url, bool silent)
    {
        var checkIfChannelExistsAsync = await DatabaseCommands.CheckIfChannelExistsAsync(guild, channel, "DisplayedItemTable");

        using var clientHttp = new HttpClient();
        var html = await clientHttp.GetStringAsync(url);

        var rowPattern = new Regex(@"<tr>(.*?)</tr>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        var cellPattern = new Regex(@"<td.*?>(.*?)</td>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        var rows = rowPattern.Matches(html);
        if (rows.Count == 0)
        {
            if (!checkIfChannelExistsAsync)
            {
                await BotCommands.SendMessageAsync("BOT Ready!", channel);
            }
            return;
        }

        var existingItems = await DisplayItemCommands.GetAllItemsAsync(guild, channel);
        var existingKeys = new HashSet<string>(
            existingItems.Select(x => $"{x.Sphere}|{x.Finder}|{x.Receiver}|{x.Item}|{x.Location}|{x.Game}")
        );

        var newItems = new List<DisplayedItem>();

        foreach (Match rowMatch in rows)
        {
            var cells = cellPattern.Matches(rowMatch.Groups[1].Value);
            if (cells.Count != 6) continue;

            var newItem = new DisplayedItem
            {
                Sphere = HttpUtility.HtmlDecode(cells[0].Groups[1].Value.Trim()),
                Finder = HttpUtility.HtmlDecode(cells[1].Groups[1].Value.Trim()),
                Receiver = HttpUtility.HtmlDecode(cells[2].Groups[1].Value.Trim()),
                Item = HttpUtility.HtmlDecode(cells[3].Groups[1].Value.Trim()),
                Location = HttpUtility.HtmlDecode(cells[4].Groups[1].Value.Trim()),
                Game = HttpUtility.HtmlDecode(cells[5].Groups[1].Value.Trim())
            };

            var key = $"{newItem.Sphere}|{newItem.Finder}|{newItem.Receiver}|{newItem.Item}|{newItem.Location}|{newItem.Game}";

            if (!existingKeys.Contains(key))
            {
                newItems.Add(newItem);
            }
        }

        if (newItems.Any())
        {
            await DisplayItemCommands.AddItemsAsync(newItems, guild, channel);
            await UpdateRecapList(guild, channel, newItems);

            if (checkIfChannelExistsAsync)
            {
                foreach (var item in newItems)
                {
                    var message = await BuildMessageAsync(guild, channel, item, silent);
                    if (!string.IsNullOrWhiteSpace(message))
                    {
                        await BotCommands.SendMessageAsync(message, channel);
                    }
                }
            }
        }

        if (!checkIfChannelExistsAsync)
        {
            await BotCommands.SendMessageAsync("BOT Ready!", channel);
        }
    }

    private static async Task<string> BuildMessageAsync(string guild, string channel, DisplayedItem item, bool silent)
    {
        if(!silent)
        {
            if (item.Finder == item.Receiver)
                return $"{item.Finder} found their {item.Item} ({item.Location})";
        }
        var userIds = await ReceiverAliasesCommands.GetReceiverUserIdsAsync(guild, channel, item.Receiver);
        if (userIds.Count > 0)
        {
            if(silent)
            {
                if (item.Finder == item.Receiver)
                    return $"{item.Finder} found their {item.Item} ({item.Location})";
            }

            var mentions = string.Join(" ", userIds.Keys.Select(id => $"<@{id}>"));

            var getGameName = await AliasChoicesCommands.GetGameForAliasAsync(guild, channel, item.Receiver);

            if (userIds.ContainsValue(true))
            {
                if(string.IsNullOrWhiteSpace(getGameName))
                {
                    return $"{item.Finder} sent {item.Item} to {mentions} {item.Receiver} ({item.Location})";
                }

                Declare.ItemsTable.TryGetValue(getGameName, out var itemList);

                if (itemList != null && itemList.filler != null && itemList.filler.Contains(item.Item))
                {
                    return $"{item.Finder} sent {item.Item} to {item.Receiver} ({item.Location})";
                }
            }
            
            return $"{item.Finder} sent {item.Item} to {mentions} {item.Receiver} ({item.Location})";
        }

        if(silent)
        {
            return string.Empty;
        }
        return $"{item.Finder} sent {item.Item} to {item.Receiver} ({item.Location})";
    }

    private static async Task UpdateRecapList(string guild, string channel, List<DisplayedItem> items)
    {
        var newItems = new List<DisplayedItem>();

        foreach (var item in items)
        {
            var usersIds = await ReceiverAliasesCommands.GetAllUsersIds(guild, channel, item.Receiver);

            if (usersIds.Count == 0)
            {
                return;
            }

            foreach (var userId in usersIds)
            {
                var checkRecapList = await RecapListCommands.CheckIfExists(guild, channel, userId, item.Receiver);
                if (!checkRecapList)
                {
                    await RecapListCommands.AddOrEditRecapListAsync(guild, channel, userId, item.Receiver);
                }

                newItems.Add(item);
            }
        }

        await RecapListCommands.AddOrEditRecapListItemsForAllAsync(guild, channel, newItems);
    }

    public static async Task setAliasAndGameStatusAsync(string guild, string channel, string urlTracker, bool silent)
    {
        var checkIfChannelExistsAsync = await DatabaseCommands.CheckIfChannelExistsAsync(guild, channel, "AliasChoicesTable");
        if (checkIfChannelExistsAsync)
            return;

        using var client = new HttpClient();
        using var stream = await client.GetStreamAsync(urlTracker);
        using var reader = new StreamReader(stream);

        var tablePattern = new Regex(@"<table.*?>(.*?)</table>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        var rowPattern = new Regex(@"<tr>(.*?)</tr>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        var cellPattern = new Regex(@"<td.*?>(.*?)</td>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        var newGameStatuses = new List<GameStatus>();
        var gameNameList = new List<Dictionary<string, string>>();

        var html = await reader.ReadToEndAsync();
        var tableMatches = tablePattern.Matches(html);

        if (tableMatches.Count > 0)
        {
            var firstTableHtml = tableMatches[0].Groups[1].Value; // Récupère uniquement le contenu du premier tableau
            var rowMatches = rowPattern.Matches(firstTableHtml);

            for (int i = 1; i < rowMatches.Count; i++)
            {
                var rowHtml = rowMatches[i].Groups[1].Value;
                var cellMatches = cellPattern.Matches(rowHtml);

                if (cellMatches.Count != 7)
                    continue;

                // Extraction du Hashtag
                var hashtagCellHtml = WebUtility.HtmlDecode(cellMatches[0].Groups[1].Value.Trim());
                var hashtagMatch = Regex.Match(hashtagCellHtml, @">([^<]+)<");
                var hashtag = hashtagMatch.Success ? hashtagMatch.Groups[1].Value.Trim() : hashtagCellHtml.Trim();

                // Création du nouvel objet GameStatus
                var newGameStatus = new GameStatus
                {
                    Hashtag = hashtag,
                    Name = WebUtility.HtmlDecode(cellMatches[1].Groups[1].Value.Trim()),
                    Game = WebUtility.HtmlDecode(cellMatches[2].Groups[1].Value.Trim()),
                    Status = WebUtility.HtmlDecode(cellMatches[3].Groups[1].Value.Trim()),
                    Checks = WebUtility.HtmlDecode(cellMatches[4].Groups[1].Value.Trim()),
                    Percent = WebUtility.HtmlDecode(cellMatches[5].Groups[1].Value.Trim()),
                    LastActivity = WebUtility.HtmlDecode(cellMatches[6].Groups[1].Value.Trim())
                };

                gameNameList.Add(new Dictionary<string, string> { { newGameStatus.Name, newGameStatus.Game } });

                var isGameExist = await GameStatusCommands.IsGameExistForGuildAndChannelAsync(guild, channel, newGameStatus.Name);
                if (!isGameExist)
                {
                    newGameStatuses.Add(newGameStatus);
                }
            }
        }

        await AliasChoicesCommands.AddOrReplaceAliasChoiceAsync(guild, channel, gameNameList);

        if (newGameStatuses.Count > 0)
        {
            await GameStatusCommands.AddOrReplaceGameStatusAsync(guild, channel, newGameStatuses);
            await BotCommands.SendMessageAsync("Aliases Updated!", channel);
        }

        if (!silent)
        {
            await ChannelsAndUrlsCommands.SendAllPatchesForChannelAsync(guild, channel);
        }
    }

    public static async Task checkGameStatus(string guild, string channel, string urlTracker, bool silent)
    {
        using var clientHttp = new HttpClient();
        using var stream = await clientHttp.GetStreamAsync(urlTracker);
        using var reader = new StreamReader(stream);

        var html = await reader.ReadToEndAsync();

        // Trouver toutes les tables
        var tableMatches = Regex.Matches(html, @"<table.*?>(.*?)</table>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (tableMatches.Count < 2)
            return;

        var table1Html = tableMatches[0].Groups[1].Value;
        var table2Html = tableMatches[1].Groups[1].Value;

        if (!string.IsNullOrEmpty(table1Html))
        {
            bool changeFound = await ProcessGameStatusTableAsync(table1Html, guild, channel, silent);
        }

        if (!string.IsNullOrEmpty(table2Html))
        {
            bool changeFound = await ProcessHintTableAsync(table2Html, guild, channel);
        }
    }

    private static async Task<bool> ProcessGameStatusTableAsync(string gameStatusTableHtml, string guild, string channel, bool silent)
    {
        var rowPattern = new Regex(@"<tr>(.*?)</tr>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        var cellPattern = new Regex(@"<td.*?>(.*?)</td>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        var rows = rowPattern.Matches(gameStatusTableHtml).Skip(1); // On skippe l'en-tête

        if (!rows.Any()) return false;

        bool changeFound = false;
        var newGameStatuses = new List<GameStatus>();
        var statusChanges = new List<GameStatus>();

        foreach (Match row in rows)
        {
            var cells = cellPattern.Matches(row.Groups[1].Value).ToList();
            if (cells.Count != 7) continue;

            var newEntry = new GameStatus
            {
                Hashtag = WebUtility.HtmlDecode(cells[0].Groups[1].Value.Trim()),
                Name = WebUtility.HtmlDecode(cells[1].Groups[1].Value.Trim()),
                Game = WebUtility.HtmlDecode(cells[2].Groups[1].Value.Trim()),
                Status = WebUtility.HtmlDecode(cells[3].Groups[1].Value.Trim()),
                Checks = WebUtility.HtmlDecode(cells[4].Groups[1].Value.Trim()),
                Percent = WebUtility.HtmlDecode(cells[5].Groups[1].Value.Trim()),
                LastActivity = WebUtility.HtmlDecode(cells[6].Groups[1].Value.Trim())
            };

            var existingStatus = await GameStatusCommands.GetGameStatusByName(guild, channel, newEntry.Name);

            if (existingStatus == null)
            {
                newGameStatuses.Add(newEntry);
                changeFound = true;
            }
            else
            {
                bool isChanged = false;

                if (existingStatus.Status != newEntry.Status)
                {
                    existingStatus.Status = newEntry.Status;
                    isChanged = true;
                }
                if (existingStatus.Percent != newEntry.Percent)
                {
                    existingStatus.Percent = newEntry.Percent;
                    isChanged = true;
                }
                if (existingStatus.Checks != newEntry.Checks)
                {
                    existingStatus.Checks = newEntry.Checks;
                    isChanged = true;
                }
                if (existingStatus.LastActivity != newEntry.LastActivity)
                {
                    existingStatus.LastActivity = newEntry.LastActivity;
                    isChanged = true;
                }

                if (isChanged)
                {
                    statusChanges.Add(existingStatus);
                    changeFound = true;
                }

                if (newEntry.Percent == "100.00" || newEntry.Status == "Goal Complete")
                {
                    existingStatus.Status = newEntry.Status;
                    existingStatus.Percent = newEntry.Percent;
                    existingStatus.Checks = newEntry.Checks;
                    existingStatus.LastActivity = newEntry.LastActivity;

                    statusChanges.Add(existingStatus);
                    if (!silent)
                    {
                        await BotCommands.SendMessageAsync($"@everyone\n{newEntry.Name} has completed their goal for this game: {newEntry.Game}!", channel);
                    }
                }
            }
        }

        if (newGameStatuses.Any())
        {
            await GameStatusCommands.AddOrReplaceGameStatusAsync(guild, channel, newGameStatuses);
        }

        if (statusChanges.Any())
        {
            await GameStatusCommands.UpdateGameStatusBatchAsync(guild, channel, statusChanges);
        }

        return changeFound;
    }

    private static async Task<bool> ProcessHintTableAsync(string hintTableHtml, string guild, string channel)
    {
        // Utilisation de Regex pour extraire les lignes de la table <tr> après la première ligne d'en-tête.
        var rowsMatch = Regex.Matches(hintTableHtml, @"<tr[^>]*>.*?</tr>", RegexOptions.Singleline);
        if (rowsMatch.Count == 0) return false;

        bool changeFound = false;

        var getHintStatusForGuildAndChannelAsync = new List<HintStatus>();

        foreach (Match rowMatch in rowsMatch)
        {
            // Extraction des cellules (td) de chaque ligne
            var cellsMatch = Regex.Matches(rowMatch.Value, @"<td[^>]*>(.*?)</td>", RegexOptions.Singleline);
            if (cellsMatch.Count != 7) continue; // On vérifie qu'il y a bien 7 cellules

            var newHint = new HintStatus
            {
                Finder = WebUtility.HtmlDecode(cellsMatch[0].Groups[1].Value.Trim()),
                Receiver = WebUtility.HtmlDecode(cellsMatch[1].Groups[1].Value.Trim()),
                Item = WebUtility.HtmlDecode(cellsMatch[2].Groups[1].Value.Trim()),
                Location = WebUtility.HtmlDecode(cellsMatch[3].Groups[1].Value.Trim()),
                Game = WebUtility.HtmlDecode(cellsMatch[4].Groups[1].Value.Trim()),
                Entrance = WebUtility.HtmlDecode(cellsMatch[5].Groups[1].Value.Trim()),
                Found = WebUtility.HtmlDecode(cellsMatch[6].Groups[1].Value.Trim())
            };

            // Vérifier si l'indice existe déjà dans la base de données
            var existingHint = await HintStatusCommands.CheckIfExists(guild, channel, newHint);

            if (!existingHint && string.IsNullOrEmpty(newHint.Found))
            {
                getHintStatusForGuildAndChannelAsync.Add(newHint);
                changeFound = true;
            }
            else if (existingHint && !string.IsNullOrEmpty(newHint.Found))
            {
                // Suppression de l'ancien indice si une modification est détectée
                getHintStatusForGuildAndChannelAsync.Remove(newHint);
                changeFound = true;
            }
        }

        if (changeFound)
        {
            // Ajouter ou mettre à jour le statut des indices
            await HintStatusCommands.AddOrReplaceHintStatusAsync(guild, channel, getHintStatusForGuildAndChannelAsync);
        }

        return changeFound;
    }

}
