using HtmlAgilityPack;
using System.Net;

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
            DataManager.LoadDisplayedItems();
            try
            {
                while (!token.IsCancellationRequested)
                {
                    if (string.IsNullOrEmpty(Declare.urlSphereTracker))
                    {
                        Console.WriteLine("Aucune URL définie. Arrêt du suivi.");
                        break;
                    }

                    await setAliasAndGameStatusAsync();
                    await checkGameStatus();
                    await GetTableDataAsync();

                    await Task.Delay(60000, token);
                }
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine("Suivi annulé.");
            }
        }, token);
    }

    static async Task GetTableDataAsync()
    {
        bool isUpdated = false;
        var clientHttp = new HttpClient();
        var html = await clientHttp.GetStringAsync(Declare.urlSphereTracker);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var data = new Dictionary<string, string>();
        var rows = doc.DocumentNode.SelectNodes("//table//tr");

        foreach (var row in rows)
        {
            var cells = row.SelectNodes("td");

            if (cells?.Count == 6)
            {
                var sphere = cells[0].InnerText.Trim();
                var finder = cells[1].InnerText.Trim();
                var receiver = cells[2].InnerText.Trim();
                var item = WebUtility.HtmlDecode(cells[3].InnerText.Trim());
                var location = WebUtility.HtmlDecode(cells[4].InnerText.Trim());
                var game = WebUtility.HtmlDecode(cells[5].InnerText.Trim());

                var newItem = new displayedItemsElement
                {
                    sphere = sphere,
                    finder = finder,
                    receiver = receiver,
                    item = item,
                    location = location,
                    game = game
                };

                bool exists = Declare.displayedItems.Any(x =>
                x.sphere == newItem.sphere &&
                x.finder == newItem.finder &&
                x.receiver == newItem.receiver &&
                x.item == newItem.item &&
                x.location == newItem.location &&
                x.game == newItem.game);

                if (!exists)
                {
                    isUpdated = true;
                    Declare.displayedItems.Add(newItem);

                    string value;
                    string userId = null;

                    if (finder.Equals(receiver))
                    {
                        value = $"{finder} found their {item} ({location})";
                    }
                    else if (Declare.receiverAliases.TryGetValue(receiver, out userId))
                    {
                        value = $"{finder} sent {item} to <@{userId}> {receiver} ({location})";
                    }
                    else
                    {
                        value = $"{finder} sent {item} to {receiver} ({location})";
                    }

                    if (File.Exists(Declare.displayedItemsFile))
                    {
                        if (!string.IsNullOrEmpty(userId))
                        {
                            if (!Declare.recapList.ContainsKey(userId))
                            {
                                Declare.recapList[userId] = new List<SubElement>();
                            }

                            var itemToAdd = Declare.recapList[userId].Find(e => e.SubKey == receiver);
                            if (itemToAdd != null)
                            {
                                itemToAdd.Values.Add(item);
                                itemToAdd.Values.Remove("Aucun élément");
                            }
                            else
                            {
                                Declare.recapList[userId].Add(new SubElement
                                {
                                    SubKey = receiver,
                                    Values = new List<string> { item }
                                });

                                if (itemToAdd != null)
                                {
                                    itemToAdd.Values.Remove("Aucun élément");
                                };
                            }
                        }
                    }

                    if (File.Exists(Declare.displayedItemsFile))
                    {
                        await BotCommands.SendMessageAsync(value);
                    }
                }
            }
        }

        if (!File.Exists(Declare.displayedItemsFile) || isUpdated)
        {
            DataManager.SaveRecapList();
            DataManager.SaveDisplayedItems();
        }
    }

    static async Task setAliasAndGameStatusAsync()
    {
        if (!File.Exists(Declare.aliasChoicesFile))
        {
            var clientHttp = new HttpClient();
            var trackerUrl = Declare.urlSphereTracker;
            Declare.urlTracker = trackerUrl.Replace("sphere_", "");

            var html = await clientHttp.GetStringAsync(Declare.urlTracker);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var tables = doc.DocumentNode.SelectNodes("//table");

            if (tables != null && tables.Any())
            {
                var firstTable = tables.FirstOrDefault();

                if (firstTable != null)
                {
                    var rows = firstTable.SelectNodes(".//tr");

                    if (rows != null)
                    {
                        bool isFirstRow = true;
                        foreach (var row in rows)
                        {
                            var cells = row.SelectNodes("td");

                            if (cells == null)
                            {
                                cells = row.SelectNodes("th");
                            }

                            if (cells != null && cells.Count == 7)
                            {
                                var hachtag = WebUtility.HtmlDecode(cells[0].InnerText.Trim());
                                var Name = WebUtility.HtmlDecode(cells[1].InnerText.Trim());
                                var Game = WebUtility.HtmlDecode(cells[2].InnerText.Trim());
                                var GameStatus = WebUtility.HtmlDecode(cells[3].InnerText.Trim());
                                var checks = WebUtility.HtmlDecode(cells[4].InnerText.Trim());
                                var pourcent = WebUtility.HtmlDecode(cells[5].InnerText.Trim());
                                var lastActivity = WebUtility.HtmlDecode(cells[6].InnerText.Trim());

                                if (isFirstRow)
                                {
                                    isFirstRow = false;
                                    continue;
                                }

                                if (!Declare.aliasChoices.ContainsKey(Name))
                                {
                                    Declare.aliasChoices.Add(Name, Name);
                                }

                                if (!Declare.gameStatus.Any(x => x.name == Name))
                                {
                                    Declare.gameStatus.Add(new gameStatus
                                    {
                                        hachtag = hachtag,
                                        name = Name,
                                        game = Game,
                                        status = GameStatus,
                                        checks = checks,
                                        pourcent = pourcent,
                                        lastActivity = lastActivity
                                    });
                                }
                            }
                        }
                    }
                }
            }
            Declare.gameStatus.Sort((x, y) => x.name.CompareTo(y.name));
            DataManager.SaveAliasChoices();
            DataManager.SaveGameStatus();
            await BotCommands.RegisterCommandsAsync();
            await BotCommands.SendMessageAsync("Bot ready ! GLHF !");
        }
    }

    static async Task checkGameStatus()
    {
        if (File.Exists(Declare.aliasChoicesFile))
        {
            DataManager.LoadGameStatus();
            bool changeFound = false;
            var clientHttp = new HttpClient();
            var trackerUrl = Declare.urlSphereTracker;
            Declare.urlTracker = trackerUrl.Replace("sphere_", "");

            var html = await clientHttp.GetStringAsync(Declare.urlTracker);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var tables = doc.DocumentNode.SelectNodes("//table");

            if (tables != null && tables.Any())
            {
                var gameStatusTable = tables[0];

                if (gameStatusTable != null)
                {
                    var rows = gameStatusTable.SelectNodes(".//tr");

                    if (rows != null)
                    {
                        bool isFirstRow = true;
                        foreach (var row in rows)
                        {
                            var cells = row.SelectNodes("td");

                            if (cells == null)
                            {
                                cells = row.SelectNodes("th");
                            }

                            if (cells != null && cells.Count == 7)
                            {
                                var Name = WebUtility.HtmlDecode(cells[1].InnerText.Trim());
                                var Game = WebUtility.HtmlDecode(cells[2].InnerText.Trim());
                                var GameStatus = WebUtility.HtmlDecode(cells[3].InnerText.Trim());

                                if (isFirstRow)
                                {
                                    isFirstRow = false;
                                    continue;
                                }

                                if (Declare.gameStatus.Any(x => x.name == Name && x.status == "Goal Completed"))
                                {
                                    continue;
                                }

                                if (Declare.gameStatus.Any(x => x.name == Name && (x.status != "Goal Complete")))
                                {
                                    if (GameStatus == "Goal Completed")
                                    {
                                        await BotCommands.SendMessageAsync($"@everyone {Name} has completed their goal for this game: {Game}!");
                                        var editStatus = Declare.gameStatus.FirstOrDefault(x => x.name == Name);
                                        if (editStatus != null)
                                        {
                                            editStatus.status = "Goal Completed";
                                            changeFound = true;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                if (changeFound)
                {
                    DataManager.SaveGameStatus();
                }

                changeFound = false;

                var hintTable = tables[1];
                if (hintTable != null)
                {
                    var rows = hintTable.SelectNodes(".//tr");

                    if (rows != null)
                    {
                        bool isFirstRow = true;
                        foreach (var row in rows)
                        {
                            var cells = row.SelectNodes("td");

                            if (cells == null)
                            {
                                cells = row.SelectNodes("th");
                            }

                            if (cells != null && cells.Count == 7)
                            {
                                var finder = WebUtility.HtmlDecode(cells[0].InnerText.Trim());
                                var receiver = WebUtility.HtmlDecode(cells[1].InnerText.Trim());
                                var item = WebUtility.HtmlDecode(cells[2].InnerText.Trim());
                                var location = WebUtility.HtmlDecode(cells[3].InnerText.Trim());
                                var game = WebUtility.HtmlDecode(cells[4].InnerText.Trim());
                                var entrance = WebUtility.HtmlDecode(cells[5].InnerText.Trim());
                                var found = WebUtility.HtmlDecode(cells[6].InnerText.Trim()); 

                                if (isFirstRow)
                                {
                                    isFirstRow = false;
                                    continue;
                                }

                                bool exists = Declare.hintStatuses.Any(x => x.finder == finder && x.receiver == receiver && x.item == item && x.location == location && x.game == game && x.entrance == entrance);

                                if (!exists)
                                {
                                    if (string.IsNullOrEmpty(found))
                                    {
                                        Declare.hintStatuses.Add(new hintStatus
                                        {
                                            finder = finder,
                                            receiver = receiver,
                                            item = item,
                                            location = location,
                                            game = game,
                                            entrance = entrance,
                                            found = ""
                                        });
                                        changeFound = true;
                                    }
                                }
                                else
                                {
                                    if (!string.IsNullOrEmpty(found))
                                    {
                                        var needToRemove = Declare.hintStatuses.FirstOrDefault(x => x.finder == finder && x.receiver == receiver && x.item == item && x.location == location && x.game == game && x.entrance == entrance);
                                        Declare.hintStatuses.Remove(needToRemove);
                                        changeFound = true;
                                    }
                                }
                            }
                        }
                    }
                    if (changeFound)
                    {
                        DataManager.SaveHintStatus();
                    }
                }
            }
        }
    }
}
