using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using ArchipelagoSphereTracker.src.Resources;
using Discord;
using Discord.WebSocket;
using HtmlAgilityPack;
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
                    await DatabaseCommands.ReclaimSpaceAsync();
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
        var channelExists = await DatabaseCommands.CheckIfChannelExistsAsync(guild, channel, "DisplayedItemTable");

        using var resp = await Declare.HttpClient.GetAsync(url, HttpCompletionOption.ResponseContentRead);
        resp.EnsureSuccessStatusCode();
        var html = await resp.Content.ReadAsStringAsync();

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var table = doc.DocumentNode.SelectSingleNode("//table");
        if (table is null) return;

        var headers = table.SelectNodes(".//thead//th")
                   ?? table.SelectNodes(".//tr[1]/th")
                   ?? table.SelectNodes(".//tr[1]/td");

        int IndexOf(string key)
        {
            if (headers == null) return -1;
            for (int i = 0; i < headers.Count; i++)
            {
                var t = Normalize(headers[i].InnerText);
                if (t == key || t.Contains(key)) return i;
            }
            return -1;
        }

        int iSphere = IndexOf("sphere");
        int iFinder = IndexOf("finder");
        int iRecv = IndexOf("receiver");
        int iItem = IndexOf("item");
        int iLoc = IndexOf("location");
        int iGame = IndexOf("game");

        var existingKeys = new HashSet<string>(await DisplayItemCommands.GetExistingKeysAsync(guild, channel));

        var rows = table.SelectNodes(".//tbody/tr") ?? table.SelectNodes(".//tr[position()>1]");
        var newItems = new List<DisplayedItem>();

        if (rows != null)
        {
            foreach (var tr in rows)
            {
                var tds = tr.SelectNodes("./td");
                if (tds == null || tds.Count == 0) continue;

                string Cell(int i)
                {
                    if (i < 0 || i >= tds.Count) return "";
                    var text = WebUtility.HtmlDecode(tds[i].InnerText)?.Trim();
                    return string.IsNullOrEmpty(text) ? "" : text.Replace('\u00A0', ' ');
                }

                var item = new DisplayedItem
                {
                    Sphere = Cell(iSphere),
                    Finder = Cell(iFinder),
                    Receiver = Cell(iRecv),
                    Item = Cell(iItem),
                    Location = Cell(iLoc),
                    Game = Cell(iGame)
                };

                var key = $"{item.Sphere}|{item.Finder}|{item.Receiver}|{item.Item}|{item.Location}|{item.Game}";
                if (!existingKeys.Contains(key))
                {
                    newItems.Add(item);
                }
            }
        }

        if (newItems.Any())
        {
            await DisplayItemCommands.AddItemsAsync(newItems, guild, channel);
            await RecapListCommands.AddOrEditRecapListItemsForAllAsync(guild, channel, newItems);

            if (channelExists)
            {
                var groupedByReceiver = newItems.GroupBy(item => item.Receiver ?? "Inconnu");

                foreach (var group in groupedByReceiver)
                {
                    var receiver = group.Key;

                    var messages = await Task.WhenAll(
                        group.Select(item => BuildMessageAsync(guild, channel, item, silent))
                    );

                    var withHeader = messages.Where(m => !string.IsNullOrWhiteSpace(m)).ToList();
                    var chunks = ChunkMessages(withHeader).ToList();

                    var userIds = await ReceiverAliasesCommands.GetReceiverUserIdsAsync(guild, channel, receiver);
                    var mentions = string.Join(" ", userIds.Select(x => x.UserId).Select(id => $"<@{id}>"));

                    for (int i = 0; i < chunks.Count; i++)
                    {
                        string header = chunks.Count > 1
                            ? $"**{Resource.ItemFor} {receiver} {mentions} ({group.Count()}) [{i + 1}/{chunks.Count}]:**"
                            : $"**{Resource.ItemFor} {receiver} {mentions} ({group.Count()}):**";

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

    public static async Task SetAliasAndGameStatusAsync(string guild, string channel, string urlTracker, bool silent)
    {
        if (await DatabaseCommands.CheckIfChannelExistsAsync(guild, channel, "AliasChoicesTable"))
            return;

        using var resp = await Declare.HttpClient.GetAsync(urlTracker, HttpCompletionOption.ResponseContentRead);
        resp.EnsureSuccessStatusCode();
        var html = await resp.Content.ReadAsStringAsync();

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var table = doc.DocumentNode.SelectSingleNode("//table");
        if (table is null) return;

        var headers = table.SelectNodes(".//thead//th")
                   ?? table.SelectNodes(".//tr[1]/th")
                   ?? table.SelectNodes(".//tr[1]/td");
        if (headers is null || headers.Count == 0) return;

        int IndexOf(string key)
        {
            for (int i = 0; i < headers.Count; i++)
            {
                var t = Normalize(headers[i].InnerText);
                if (t == key || t.Contains(key)) return i;
            }
            return -1;
        }

        int iHashtag = IndexOf("hashtag");
        if (iHashtag < 0) { iHashtag = IndexOf("#"); }
        int iName = IndexOf("name");
        int iGame = IndexOf("game");
        int iStatus = IndexOf("status");
        int iChecks = IndexOf("checks");
        int iPercent = IndexOf("percent");
        int iLastAct = IndexOf("last activity");

        var rows = table.SelectNodes(".//tbody/tr") ?? table.SelectNodes(".//tr[position()>1]");
        if (rows is null || rows.Count == 0) return;

        var tmpRows = new List<HtmlNode[]>(rows.Count);
        var namesToCheck = new List<string>(rows.Count);

        foreach (var tr in rows)
        {
            var tds = tr.SelectNodes("./td");
            if (tds is null || tds.Count == 0) continue;

            if (iName < 0 || iGame < 0) continue;

            tmpRows.Add(tds.ToArray());

            var name = Clean(iName < tds.Count ? tds[iName] : null);
            if (!string.IsNullOrWhiteSpace(name))
                namesToCheck.Add(name);
        }

        if (tmpRows.Count == 0) return;

        var existingNames = await GameStatusCommands.GetExistingGameNamesAsync(guild, channel, namesToCheck);

        var newGameStatuses = new List<GameStatus>(tmpRows.Count);
        var gameNameList = new List<Dictionary<string, string>>(tmpRows.Count);

        foreach (var tds in tmpRows)
        {
            string hashtag = Clean(iHashtag >= 0 && iHashtag < tds.Length ? tds[iHashtag] : null);
            string name = Clean(iName >= 0 && iName < tds.Length ? tds[iName] : null);
            string game = Clean(iGame >= 0 && iGame < tds.Length ? tds[iGame] : null);

            if (string.IsNullOrWhiteSpace(name)) continue;

            gameNameList.Add(new Dictionary<string, string> { { name, game } });

            if (!existingNames.Contains(name))
            {
                string status = Clean(iStatus >= 0 && iStatus < tds.Length ? tds[iStatus] : null);
                string checks = Clean(iChecks >= 0 && iChecks < tds.Length ? tds[iChecks] : null);
                string pct = Clean(iPercent >= 0 && iPercent < tds.Length ? tds[iPercent] : null);
                string last = Clean(iLastAct >= 0 && iLastAct < tds.Length ? tds[iLastAct] : null);

                newGameStatuses.Add(new GameStatus
                {
                    Hashtag = hashtag,
                    Name = name,
                    Game = game,
                    Status = status,
                    Checks = checks,
                    Percent = pct,
                    LastActivity = last
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

    public static async Task CheckGameStatusAsync(string guild, string channel, string urlTracker, bool silent)
    {
        using var resp = await Declare.HttpClient.GetAsync(urlTracker, HttpCompletionOption.ResponseContentRead);
        resp.EnsureSuccessStatusCode();
        var html = await resp.Content.ReadAsStringAsync();

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var tables = doc.DocumentNode.SelectNodes("//table");
        if (tables is null || tables.Count < 2)
            return;

        var table1Html = tables[0].OuterHtml;
        var table2Html = tables[1].OuterHtml;

        if (!string.IsNullOrWhiteSpace(table1Html))
            await ProcessGameStatusTableAsync(table1Html, guild, channel, silent);

        if (!string.IsNullOrWhiteSpace(table2Html))
            await ProcessHintTableAsync(table2Html, guild, channel);
    }

    private static async Task ProcessGameStatusTableAsync(string tableHtml, string guild, string channel, bool silent)
    {
        await GameStatusCommands.DeleteDuplicateAliasAsync(guild, channel);

        var doc = new HtmlDocument();
        doc.LoadHtml(tableHtml);

        var table = doc.DocumentNode.SelectSingleNode("//table") ?? doc.DocumentNode;
        var headers = table.SelectNodes(".//thead//th")
                   ?? table.SelectNodes(".//tr[1]/th")
                   ?? table.SelectNodes(".//tr[1]/td");
        if (headers is null || headers.Count == 0) return;

        int iHashtag = IndexOfHeader(headers, "hashtag"); if (iHashtag < 0) iHashtag = IndexOfHeader(headers, "#");
        int iName = IndexOfHeader(headers, "name");
        int iGame = IndexOfHeader(headers, "game");
        int iStatus = IndexOfHeader(headers, "status");
        int iChecks = IndexOfHeader(headers, "checks");
        int iPercent = IndexOfHeader(headers, "percent");
        int iLast = IndexOfHeader(headers, "last activity");

        var req = new[] { iHashtag, iName, iGame, iStatus, iChecks, iPercent, iLast };
        if (req.Any(i => i < 0)) return;

        var rows = table.SelectNodes(".//tbody/tr") ?? table.SelectNodes(".//tr[position()>1]");
        if (rows is null || rows.Count == 0) return;

        var parsedEntries = new Dictionary<string, GameStatus>(rows.Count);

        foreach (var tr in rows)
        {
            var tds = tr.SelectNodes("./td");
            if (tds is null || tds.Count == 0) continue;

            string hashtag = ExtractHashtag(iHashtag < tds.Count ? tds[iHashtag] : null);
            string name = Clean(iName < tds.Count ? tds[iName] : null);
            string game = Clean(iGame < tds.Count ? tds[iGame] : null);
            string status = Clean(iStatus < tds.Count ? tds[iStatus] : null);
            string checks = Clean(iChecks < tds.Count ? tds[iChecks] : null);
            string percent = Clean(iPercent < tds.Count ? tds[iPercent] : null);
            string last = Clean(iLast < tds.Count ? tds[iLast] : null);

            if (string.IsNullOrWhiteSpace(name)) continue;

            var entry = new GameStatus
            {
                Hashtag = hashtag,
                Name = name,
                Game = game,
                Status = status,
                Checks = checks,
                Percent = percent,
                LastActivity = last
            };

            parsedEntries[entry.Name] = entry;
        }

        if (parsedEntries.Count == 0) return;

        var existingStatuses = await GameStatusCommands.GetStatusesByNamesAsync(
            guild, channel, parsedEntries.Keys.ToList());

        var newGameStatuses = new List<GameStatus>(parsedEntries.Count);
        var statusChanges = new List<GameStatus>(parsedEntries.Count);

        foreach (var (name, newEntry) in parsedEntries)
        {
            if (!existingStatuses.TryGetValue(name, out var existing))
            {
                newGameStatuses.Add(newEntry);
                continue;
            }

            if (existing.Percent == "100.00")
                continue;

            bool isChanged = false;
            if (existing.Status != newEntry.Status) { existing.Status = newEntry.Status; isChanged = true; }
            if (existing.Percent != newEntry.Percent) { existing.Percent = newEntry.Percent; isChanged = true; }
            if (existing.Checks != newEntry.Checks) { existing.Checks = newEntry.Checks; isChanged = true; }

            if (isChanged)
            {
                existing.LastActivity = newEntry.LastActivity;
                statusChanges.Add(existing);

                if (newEntry.Percent == "100.00" || newEntry.Status == "Goal Complete")
                {
                    var matchCustomAlias = System.Text.RegularExpressions.Regex.Match(existing.Name, @"\(([^)]+)\)$");
                    var alias = matchCustomAlias.Success ? matchCustomAlias.Groups[1].Value : existing.Name;

                    var getReceivers = await ReceiverAliasesCommands.CheckIfReceiverExists(guild, channel, alias);

                    if (!silent || getReceivers)
                    {
                        await BotCommands.SendMessageAsync(
                            string.Format(Resource.TDMGoalComplete, newEntry.Name, newEntry.Game),
                            channel
                        );
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

        var doc = new HtmlDocument();
        doc.LoadHtml(tableHtml);

        var table = doc.DocumentNode.SelectSingleNode("//table") ?? doc.DocumentNode;
        var headers = table.SelectNodes(".//thead//th")
                   ?? table.SelectNodes(".//tr[1]/th")
                   ?? table.SelectNodes(".//tr[1]/td");
        if (headers is null || headers.Count == 0) return;

        int iFinder = IndexOfHeader(headers, "finder");
        int iReceiver = IndexOfHeader(headers, "receiver");
        int iItem = IndexOfHeader(headers, "item");
        int iLocation = IndexOfHeader(headers, "location");
        int iGame = IndexOfHeader(headers, "game");
        int iEntrance = IndexOfHeader(headers, "entrance");
        int iFound = IndexOfHeader(headers, "found");

        var required = new[] { iFinder, iReceiver, iItem, iLocation, iGame, iEntrance, iFound };
        if (required.Any(i => i < 0)) return;

        var rows = table.SelectNodes(".//tbody/tr") ?? table.SelectNodes(".//tr[position()>1]");
        if (rows is null || rows.Count == 0) return;

        var existingList = await HintStatusCommands.GetHintStatus(guild, channel);
        var existingByKey = existingList.ToDictionary(MakeKey);

        var hintsToAdd = new List<HintStatus>(rows.Count);
        var hintsToUpdate = new List<HintStatus>(rows.Count);

        foreach (var tr in rows)
        {
            var tds = tr.SelectNodes("./td");
            if (tds is null || tds.Count == 0) continue;

            string finder = Clean(iFinder < tds.Count ? tds[iFinder] : null);
            string receiver = Clean(iReceiver < tds.Count ? tds[iReceiver] : null);
            string item = Clean(iItem < tds.Count ? tds[iItem] : null);
            string location = Clean(iLocation < tds.Count ? tds[iLocation] : null);
            string game = Clean(iGame < tds.Count ? tds[iGame] : null);
            string entrance = Clean(iEntrance < tds.Count ? tds[iEntrance] : null);
            string found = Clean(iFound < tds.Count ? tds[iFound] : null);

            if (string.IsNullOrWhiteSpace(finder) && string.IsNullOrWhiteSpace(receiver))
                continue;

            if (found == "✔") found = "OK";

            var hint = new HintStatus
            {
                Finder = finder,
                Receiver = receiver,
                Item = item,
                Location = location,
                Game = game,
                Entrance = entrance,
                Found = found
            };

            var key = MakeKey(hint);

            if (existingByKey.TryGetValue(key, out var existing))
            {
                if (!string.Equals(existing.Found, found, StringComparison.Ordinal))
                {
                    existing.Found = "OK";
                    hintsToUpdate.Add(existing);
                }
            }
            else
            {
                hintsToAdd.Add(hint);
            }
        }

        if (hintsToAdd.Count > 0)
            await HintStatusCommands.AddHintStatusAsync(guild, channel, hintsToAdd);

        if (hintsToUpdate.Count > 0)
            await HintStatusCommands.UpdateHintStatusAsync(guild, channel, hintsToUpdate);
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
        using var resp = await Declare.HttpClient.GetAsync(trackerUrl, HttpCompletionOption.ResponseContentRead);
        resp.EnsureSuccessStatusCode();
        var html = await resp.Content.ReadAsStringAsync();

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var table = doc.DocumentNode.SelectSingleNode("//table");
        if (table is null)
            return true;

        var headers = table.SelectNodes(".//thead//th")
                   ?? table.SelectNodes(".//tr[1]/th")
                   ?? table.SelectNodes(".//tr[1]/td");
        if (headers is null || headers.Count == 0)
            return true;

        int iName = -1;
        for (int i = 0; i < headers.Count; i++)
        {
            var t = Normalize(headers[i].InnerText);
            if (t == "name" || t.Contains("name"))
            {
                iName = i;
                break;
            }
        }
        if (iName < 0)
            return true;

        var rows = table.SelectNodes(".//tbody/tr") ?? table.SelectNodes(".//tr[position()>1]");
        if (rows is null || rows.Count == 0)
            return true;

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tr in rows)
        {
            var tds = tr.SelectNodes("./td");
            if (tds is null || tds.Count <= iName) continue;

            var name = Clean(tds[iName]);
            if (!string.IsNullOrWhiteSpace(name))
                names.Add(name);
        }

        var count = names.Count;
        Console.WriteLine($"Player {count}/{Declare.MaxPlayer}");

        if (count > Declare.MaxPlayer)
        {
            Console.WriteLine(string.Format(Resource.CheckPlayerMinMax, Declare.MaxPlayer));
            return true;
        }

        return false;
    }

    private static string Normalize(string s) =>
    new string((s ?? "").Trim().ToLowerInvariant()
        .Where(ch => char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch))
        .ToArray());

    private static string Clean(HtmlNode? td)
    {
        if (td is null) return "";
        var t = WebUtility.HtmlDecode(td.InnerText)?.Trim();
        return string.IsNullOrEmpty(t) ? "" : t.Replace('\u00A0', ' ');
    }

    private static string MakeKey(HintStatus h) =>
        $"{h.Finder}|{h.Receiver}|{h.Item}|{h.Location}|{h.Game}|{h.Entrance}";

    private static int IndexOfHeader(IList<HtmlNode> headers, string key)
    {
        for (int i = 0; i < headers.Count; i++)
        {
            var t = Normalize(headers[i].InnerText);
            if (t == key || t.Contains(key)) return i;
        }
        return -1;
    }

    private static string ExtractHashtag(HtmlNode? td)
    {
        if (td is null) return "";
        var a = td.SelectSingleNode(".//a");
        var text = a != null ? a.InnerText : td.InnerText;
        text = WebUtility.HtmlDecode(text)?.Trim() ?? "";
        return string.IsNullOrEmpty(text) ? "" : text.Replace('\u00A0', ' ');
    }
}
