using ArchipelagoSphereTracker.src.Resources;
using Discord;
using Discord.WebSocket;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

public static class TrackingDataManager
{
    public static class RateLimitGuards
    {
        public static readonly SemaphoreSlim SendMessageGate = new(1, 1);
    }

    private static readonly ConcurrentDictionary<string, byte> InFlight = new();

    public static void StartTracking()
    {

        const int MaxGuildsParallel = 10;
        const int MaxChannelsParallel = 2;

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
                var programID = await DatabaseCommands.ProgramIdentifier("ProgramIdTable");

                while (!token.IsCancellationRequested)
                {
                    var getAllGuild = await DatabaseCommands.GetAllGuildsAsync("ChannelsAndUrlsTable");
                    var uniqueGuilds = getAllGuild.Distinct().ToList();

                    await Telemetry.SendDailyTelemetryAsync(programID);

                    await Parallel.ForEachAsync(
                    uniqueGuilds,
                    new ParallelOptions { MaxDegreeOfParallelism = MaxGuildsParallel },
                    async (guild, ctGuild) =>
                    {
                        var guildCheck = Declare.Client.GetGuild(ulong.Parse(guild));
                        if (guildCheck == null)
                        {
                            Console.WriteLine(string.Format(Resource.TDMServerNotFound, guild));
                            await DatabaseCommands.DeleteChannelDataByGuildIdAsync(guild);
                            await DatabaseCommands.ReclaimSpaceAsync();
                            Console.WriteLine(Resource.TDMDeletionCompleted);
                            return;
                        }

                        var channelsRaw = await DatabaseCommands.GetAllChannelsAsync(guild, "ChannelsAndUrlsTable");
                        var uniqueChannels = channelsRaw.Distinct().ToList();

                        var channelsToProcess = uniqueChannels
                            .Where(ch => !Declare.AddedChannelId.Contains(ch))
                            .ToList();

                        await Parallel.ForEachAsync(
                            channelsToProcess,
                            new ParallelOptions { MaxDegreeOfParallelism = MaxChannelsParallel },
                            async (channel, ctChan) =>
                            {
                                var key = $"{guild}:{channel}";

                                if (!InFlight.TryAdd(key, 0))
                                {
                                    return;
                                }

                                try
                                {
                                    var (urlTracker, urlSphereTracker, room, silent) = await ChannelsAndUrlsCommands.GetTrackerUrlsAsync(guild, channel);
                                    var channelCheck = guildCheck.GetChannel(ulong.Parse(channel));

                                    if (channelCheck == null)
                                    {
                                        Console.WriteLine(string.Format(Resource.TDMChannelNoLongerExists, channel));
                                        await DatabaseCommands.DeleteChannelDataAsync(guild, channel);
                                        await DatabaseCommands.ReclaimSpaceAsync();
                                        Console.WriteLine(Resource.TDMDeletionCompleted);
                                        return;
                                    }

                                    Console.WriteLine(string.Format(Resource.TDMChannelStillExists, channelCheck.Name));

                                    if (guildCheck.GetChannel(ulong.Parse(channel)) is SocketThreadChannel thread)
                                    {
                                        var messages = await thread.GetMessagesAsync(1).FlattenAsync();
                                        var lastMessage = messages.FirstOrDefault();

                                        DateTimeOffset lastActivity = lastMessage?.Timestamp ?? SnowflakeUtils.FromSnowflake(thread.Id);
                                        if (lastMessage == null)
                                            Console.WriteLine(string.Format(Resource.TDMNoMessageFound, lastActivity));

                                        double daysInactive = (DateTimeOffset.UtcNow - lastActivity).TotalDays;

                                        if (daysInactive < 6)
                                        {
                                            if (Declare.WarnedThreads.Contains(thread.Id.ToString()))
                                            {
                                                await RateLimitGuards.SendMessageGate.WaitAsync(ctChan);
                                                try
                                                {
                                                    await BotCommands.SendMessageAsync(string.Format(Resource.TDMNewMessageOnThread, thread.Name), thread.Id.ToString());
                                                }
                                                finally
                                                {
                                                    RateLimitGuards.SendMessageGate.Release();
                                                }
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

                                                await RateLimitGuards.SendMessageGate.WaitAsync(ctChan);
                                                try
                                                {
                                                    await BotCommands.SendMessageAsync(
                                                        string.Format(Resource.TDMNoMessage6Days, formattedDeletionDate, thread.Name),
                                                        thread.ParentChannel.Id.ToString());
                                                }
                                                finally
                                                {
                                                    RateLimitGuards.SendMessageGate.Release();
                                                }

                                                Declare.WarnedThreads.Add(thread.Id.ToString());
                                            }
                                        }
                                        else
                                        {
                                            Console.WriteLine(string.Format(Resource.TDMLastActivity, lastActivity));
                                            Console.WriteLine(Resource.TDMNoActivity);
                                            await DatabaseCommands.DeleteChannelDataAsync(guild, channel);
                                            await DatabaseCommands.ReclaimSpaceAsync();
                                            await thread.DeleteAsync();
                                            Console.WriteLine(Resource.TDMThreadDeleted);
                                            Declare.WarnedThreads.Remove(thread.Id.ToString());
                                            return;
                                        }
                                    }

                                    Console.WriteLine(string.Format(Resource.TDMSettingsAliasesGamesStatus, channelCheck.Name));
                                    await SetAliasAndGameStatusAsync(guild, channel, urlTracker, silent);

                                    Console.WriteLine(string.Format(Resource.TDMCheckingGameStatus, channelCheck.Name));
                                    await CheckGameStatusAsync(guild, channel, urlTracker, silent);

                                    Console.WriteLine(string.Format(Resource.TDMCheckingItems, channelCheck.Name));
                                    await GetTableDataAsync(guild, channel, urlSphereTracker, silent);
                                }
                                finally
                                {
                                    InFlight.TryRemove(key, out _);
                                }
                            });
                    });
                    Console.WriteLine(Resource.TDMWaitingCheck);
                    await Task.Delay(300000);
                }
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine(Resource.TDMTrackingCanceled);
            }
        }, token);
    }

    public static async Task GetTableDataAsync(string guild, string channel, string url, bool silent)
    {
        var checkIfChannelExistsAsync = await DatabaseCommands.CheckIfChannelExistsAsync(guild, channel, "DisplayedItemTable");

        using var stream = await Declare.HttpClient.GetStreamAsync(url);
        using var html = new StreamReader(stream);

        var cellPattern = new Regex(@"<td.*?>(.*?)</td>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        var existingKeys = await DisplayItemCommands.GetExistingKeysAsync(guild, channel);
        var newItems = new List<DisplayedItem>();

        foreach (var rowHtml in StreamHtmlRows(html))
        {
            var cells = cellPattern.Matches(rowHtml);
            if (cells.Count == 6)
            {
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
        }

        if (newItems.Any())
        {
            await DisplayItemCommands.AddItemsAsync(newItems, guild, channel);
            await RecapListCommands.AddOrEditRecapListItemsForAllAsync(guild, channel, newItems);

            if (checkIfChannelExistsAsync)
            {
                var groupedByReceiver = newItems
                    .GroupBy(item => item.Receiver ?? "Inconnu");

                foreach (var group in groupedByReceiver)
                {
                    var receiver = group.Key;

                    var messages = await Task.WhenAll(
                        group.Select(item => BuildMessageAsync(guild, channel, item, silent))
                    );

                    var nonEmpty = messages.Where(m => !string.IsNullOrWhiteSpace(m));

                    var withHeader = nonEmpty.ToList();

                    var chunks = ChunkMessages(withHeader).ToList();

                    var userIds = await ReceiverAliasesCommands.GetReceiverUserIdsAsync(guild, channel, receiver);
                    var mentions = string.Join(" ", userIds.Select(x => x.UserId).Select(id => $"<@{id}>"));

                    for (int i = 0; i < chunks.Count; i++)
                    {
                        string header = chunks.Count > 1
                            ? $"**Items pour {receiver} {mentions} ({group.Count()}) [{i + 1}/{chunks.Count}]:**"
                            : $"**Items pour {receiver} {mentions} ({group.Count()}):**";

                        string finalMessage = header + "\n" + chunks[i];

                        await RateLimitGuards.SendMessageGate.WaitAsync();
                        try
                        {
                            await BotCommands.SendMessageAsync(finalMessage, channel);
                        }
                        finally
                        {
                            RateLimitGuards.SendMessageGate.Release();
                        }

                        await Task.Delay(1100);
                    }
                }
            }
        }
    }

    private static IEnumerable<string> StreamHtmlRows(StreamReader reader)
    {
        var sb = new StringBuilder();
        string? line;
        bool insideRow = false;

        while ((line = reader.ReadLine()) != null)
        {
            if (line.Contains("<tr", StringComparison.OrdinalIgnoreCase))
            {
                insideRow = true;
                sb.Clear();
            }

            if (insideRow)
            {
                sb.AppendLine(line);
            }

            if (line.Contains("</tr>", StringComparison.OrdinalIgnoreCase))
            {
                insideRow = false;
                yield return sb.ToString();
            }
        }
    }

    private static async Task<string> BuildMessageAsync(string guild, string channel, DisplayedItem item, bool silent)
    {
        if (!silent)
        {
            if (item.Finder == item.Receiver)
                return string.Empty;
        }
        var userIds = await ReceiverAliasesCommands.GetReceiverUserIdsAsync(guild, channel, item.Receiver);
        if (userIds.Count > 0)
        {
            if (silent)
            {
                if (item.Finder == item.Receiver)
                    return string.Empty;
            }


            if (userIds.Any(x => x.IsEnabled.Equals(true)))
            {
                var getGameName = await AliasChoicesCommands.GetGameForAliasAsync(guild, channel, item.Receiver);

                if (!string.IsNullOrWhiteSpace(getGameName))
                {
                    var isFiller = await ItemsCommands.IsFillerAsync(getGameName, item.Item);

                    if (isFiller)
                    {
                        return string.Empty;
                    }
                    return string.Format(Resource.TDPMEssageItemsNoMention, item.Finder, item.Item, item.Receiver, item.Location);
                }
            }

            return string.Format(Resource.TDPMEssageItemsNoMention, item.Finder, item.Item, item.Receiver, item.Location);
        }

        if (silent)
        {
            return string.Empty;
        }
        return string.Format(Resource.TDPMEssageItemsNoMention, item.Finder, item.Item, item.Receiver, item.Location);
    }

    private static readonly Regex TableRegex = new(@"<table.*?>(.*?)</table>", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RowRegex = new(@"<tr>(.*?)</tr>", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CellRegex = new(@"<td.*?>(.*?)</td>", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static async Task SetAliasAndGameStatusAsync(string guild, string channel, string urlTracker, bool silent)
    {
        if (await DatabaseCommands.CheckIfChannelExistsAsync(guild, channel, "AliasChoicesTable"))
            return;

        using var stream = await Declare.HttpClient.GetStreamAsync(urlTracker);
        using var reader = new StreamReader(stream);

        var html = await reader.ReadToEndAsync();
        var tableMatch = TableRegex.Match(html);

        if (!tableMatch.Success)
            return;

        var firstTableHtml = tableMatch.Groups[1].Value;
        var rowMatches = RowRegex.Matches(firstTableHtml);

        var parsedRows = new List<Match[]>(rowMatches.Count);

        for (int i = 1; i < rowMatches.Count; i++)
        {
            var cells = CellRegex.Matches(rowMatches[i].Groups[1].Value);
            if (cells.Count == 7)
            {
                parsedRows.Add(cells.Cast<Match>().ToArray());
            }
        }

        if (parsedRows.Count == 0)
            return;

        var namesToCheck = new List<string>(parsedRows.Count);
        foreach (var cells in parsedRows)
        {
            var name = WebUtility.HtmlDecode(cells[1].Groups[1].Value.Trim());
            namesToCheck.Add(name);
        }

        var existingNames = await GameStatusCommands.GetExistingGameNamesAsync(guild, channel, namesToCheck);

        var newGameStatuses = new List<GameStatus>(parsedRows.Count);
        var gameNameList = new List<Dictionary<string, string>>(parsedRows.Count);

        foreach (var cells in parsedRows)
        {
            var hashtagCell = WebUtility.HtmlDecode(cells[0].Groups[1].Value.Trim());
            var hashtagMatch = Regex.Match(hashtagCell, @">([^<]+)<");
            var hashtag = hashtagMatch.Success ? hashtagMatch.Groups[1].Value.Trim() : hashtagCell;

            var name = WebUtility.HtmlDecode(cells[1].Groups[1].Value.Trim());
            var game = WebUtility.HtmlDecode(cells[2].Groups[1].Value.Trim());

            gameNameList.Add(new Dictionary<string, string> { { name, game } });

            if (!existingNames.Contains(name))
            {
                newGameStatuses.Add(new GameStatus
                {
                    Hashtag = hashtag,
                    Name = name,
                    Game = game,
                    Status = WebUtility.HtmlDecode(cells[3].Groups[1].Value.Trim()),
                    Checks = WebUtility.HtmlDecode(cells[4].Groups[1].Value.Trim()),
                    Percent = WebUtility.HtmlDecode(cells[5].Groups[1].Value.Trim()),
                    LastActivity = WebUtility.HtmlDecode(cells[6].Groups[1].Value.Trim())
                });
            }
        }

        if (gameNameList.Count > 0)
        {
            await AliasChoicesCommands.AddOrReplaceAliasChoiceAsync(guild, channel, gameNameList);
        }

        if (newGameStatuses.Count > 0)
        {
            await GameStatusCommands.AddOrReplaceGameStatusAsync(guild, channel, newGameStatuses);
            await BotCommands.SendMessageAsync(Resource.TDMAliasUpdated, channel);
        }

        if (!silent)
        {
            await ChannelsAndUrlsCommands.SendAllPatchesFileForChannelAsync(guild, channel);
        }
    }

    private static readonly Regex RowRegex2 = new(@"<tr.*?>(.*?)</tr>", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static async Task CheckGameStatusAsync(string guild, string channel, string urlTracker, bool silent)
    {
        using var stream = await Declare.HttpClient.GetStreamAsync(urlTracker);
        using var reader = new StreamReader(stream);

        var html = await reader.ReadToEndAsync();
        var tables = TableRegex.Matches(html);

        if (tables.Count < 2)
            return;

        var table1Html = tables[0].Groups[1].Value;
        var table2Html = tables[1].Groups[1].Value;

        if (!string.IsNullOrEmpty(table1Html))
            await ProcessGameStatusTableAsync(table1Html, guild, channel, silent);

        if (!string.IsNullOrEmpty(table2Html))
            await ProcessHintTableAsync(table2Html, guild, channel);
    }

    private static async Task ProcessGameStatusTableAsync(string tableHtml, string guild, string channel, bool silent)
    {
        await GameStatusCommands.DeleteDuplicateAliasAsync(guild, channel);

        var rows = RowRegex2.Matches(tableHtml);
        if (rows.Count <= 1)
            return;

        var newGameStatuses = new List<GameStatus>(rows.Count);
        var statusChanges = new List<GameStatus>(rows.Count);

        var parsedEntries = new Dictionary<string, GameStatus>(rows.Count);

        for (int i = 1; i < rows.Count; i++)
        {
            var cells = CellRegex.Matches(rows[i].Groups[1].Value);
            if (cells.Count != 7)
                continue;

            var hashtagRaw = WebUtility.HtmlDecode(cells[0].Groups[1].Value.Trim());
            var hashtagMatch = Regex.Match(hashtagRaw, @">([^<]+)<");
            var hashtag = hashtagMatch.Success ? hashtagMatch.Groups[1].Value.Trim() : hashtagRaw;

            var entry = new GameStatus
            {
                Hashtag = hashtag,
                Name = WebUtility.HtmlDecode(cells[1].Groups[1].Value.Trim()),
                Game = WebUtility.HtmlDecode(cells[2].Groups[1].Value.Trim()),
                Status = WebUtility.HtmlDecode(cells[3].Groups[1].Value.Trim()),
                Checks = WebUtility.HtmlDecode(cells[4].Groups[1].Value.Trim()),
                Percent = WebUtility.HtmlDecode(cells[5].Groups[1].Value.Trim()),
                LastActivity = WebUtility.HtmlDecode(cells[6].Groups[1].Value.Trim())
            };

            parsedEntries[entry.Name] = entry;
        }

        if (parsedEntries.Count == 0)
            return;

        var existingStatuses = await GameStatusCommands.GetStatusesByNamesAsync(guild, channel, parsedEntries.Keys.ToList());

        foreach (var (name, newEntry) in parsedEntries)
        {
            if (!existingStatuses.TryGetValue(name, out var existing))
            {
                newGameStatuses.Add(newEntry);
                continue;
            }

            if (existing.Percent == "100.00")
            {
                continue;
            }

            bool isChanged = false;

            if (existing.Status != newEntry.Status) { existing.Status = newEntry.Status; isChanged = true; }
            if (existing.Percent != newEntry.Percent) { existing.Percent = newEntry.Percent; isChanged = true; }
            if (existing.Checks != newEntry.Checks) { existing.Checks = newEntry.Checks; isChanged = true; }

            if (isChanged)
            {
                existing.Status = newEntry.Status;
                existing.Percent = newEntry.Percent;
                existing.Checks = newEntry.Checks;
                existing.LastActivity = newEntry.LastActivity;
                statusChanges.Add(existing);

                if (newEntry.Percent == "100.00" || newEntry.Status == "Goal Complete")
                {
                    var matchCustomAlias = Regex.Match(existing.Name, @"\(([^)]+)\)$");
                    var alias = matchCustomAlias.Success ? matchCustomAlias.Groups[1].Value : existing.Name;

                    var getReceivers = await ReceiverAliasesCommands.CheckIfReceiverExists(guild, channel, alias);

                    if (!silent || getReceivers)
                    {
                        if (isChanged)
                        {
                            await BotCommands.SendMessageAsync(string.Format(Resource.TDMGoalComplete, newEntry.Name, newEntry.Game), channel);
                        }
                    }
                }
            }
        }

        if (newGameStatuses.Count > 0)
            await GameStatusCommands.AddOrReplaceGameStatusAsync(guild, channel, newGameStatuses);

        if (statusChanges.Count > 0)
            await GameStatusCommands.UpdateGameStatusBatchAsync(guild, channel, statusChanges);
    }

    private static async Task ProcessHintTableAsync(string tableHtml, string guild, string channel)
    {
        await HintStatusCommands.DeleteDuplicateReceiversAliasAsync(guild, channel);
        await HintStatusCommands.DeleteDuplicateFindersAliasAsync(guild, channel);

        var rows = RowRegex2.Matches(tableHtml);
        if (rows.Count == 0)
            return;

        var hintsToAdd = new List<HintStatus>(rows.Count);
        var hintsToUpdate = new List<HintStatus>(rows.Count);

        var getHintStatusList = await HintStatusCommands.GetHintStatus(guild, channel);

        for (int i = 1; i < rows.Count; i++)
        {
            var cells = CellRegex.Matches(rows[i].Groups[1].Value);
            if (cells.Count != 7)
                continue;

            var hint = new HintStatus
            {
                Finder = WebUtility.HtmlDecode(cells[0].Groups[1].Value.Trim()),
                Receiver = WebUtility.HtmlDecode(cells[1].Groups[1].Value.Trim()),
                Item = WebUtility.HtmlDecode(cells[2].Groups[1].Value.Trim()),
                Location = WebUtility.HtmlDecode(cells[3].Groups[1].Value.Trim()),
                Game = WebUtility.HtmlDecode(cells[4].Groups[1].Value.Trim()),
                Entrance = WebUtility.HtmlDecode(cells[5].Groups[1].Value.Trim()),
                Found = WebUtility.HtmlDecode(cells[6].Groups[1].Value.Trim())
            };

            if (hint.Found == "✔")
            {
                hint.Found = "OK";
            }

            var existingKey = getHintStatusList.Where(getHintStatusList => getHintStatusList.Finder == hint.Finder
            && getHintStatusList.Receiver == hint.Receiver
            && getHintStatusList.Item == hint.Item
            && getHintStatusList.Location == hint.Location
            && getHintStatusList.Game == hint.Game
            && getHintStatusList.Entrance == hint.Entrance).FirstOrDefault();

            if (existingKey != null)
            {
                if (existingKey.Found != hint.Found)
                {
                    existingKey.Found = "OK";
                    hintsToUpdate.Add(existingKey);
                }
            }
            else
            {
                hintsToAdd.Add(hint);
            }
        }

        if (hintsToAdd.Count > 0)
        {
            await HintStatusCommands.AddHintStatusAsync(guild, channel, hintsToAdd);
        }

        if (hintsToUpdate.Count > 0)
        {
            await HintStatusCommands.UpdateHintStatusAsync(guild, channel, hintsToUpdate);
        }
    }

    private static IEnumerable<string> ChunkMessages(IEnumerable<string> messages, int maxLength = 1900)
    {
        var sb = new StringBuilder();
        foreach (var msg in messages)
        {
            if (string.IsNullOrWhiteSpace(msg)) continue;

            if (sb.Length + msg.Length + (sb.Length > 0 ? 1 : 0) > maxLength)
            {
                yield return sb.ToString();
                sb.Clear();
            }

            if (sb.Length > 0) sb.AppendLine();
            sb.Append(msg);
        }

        if (sb.Length > 0)
            yield return sb.ToString();
    }

    public static async Task<bool> CheckMaxPlayersAsync(string trackerUrl)
    {
        using var stream = await Declare.HttpClient.GetStreamAsync(trackerUrl);
        using var reader = new StreamReader(stream);

        var html = await reader.ReadToEndAsync();
        var tableMatch = TableRegex.Match(html);

        if (!tableMatch.Success)
            return true;

        var firstTableHtml = tableMatch.Groups[1].Value;
        var rowMatches = RowRegex.Matches(firstTableHtml);

        var parsedRows = new List<Match[]>(rowMatches.Count);

        for (int i = 1; i < rowMatches.Count; i++)
        {
            var cells = CellRegex.Matches(rowMatches[i].Groups[1].Value);
            if (cells.Count == 7)
            {
                parsedRows.Add(cells.Cast<Match>().ToArray());
            }
        }

        if (parsedRows.Count == 0)
            return true;

        var namesToCheck = new List<string>(parsedRows.Count);
        foreach (var cells in parsedRows)
        {
            var name = WebUtility.HtmlDecode(cells[1].Groups[1].Value.Trim());
            namesToCheck.Add(name);
        }
        Console.WriteLine($"Player {namesToCheck.Count}/{Declare.MaxPlayer}");
        if (namesToCheck.Count > Declare.MaxPlayer)
        {
            Console.WriteLine($"Players Min > Players Max, Impossible to add this archipelago.");
            return true;
        }
        return false;
    }
}
