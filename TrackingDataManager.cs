using HtmlAgilityPack;
using System.Net;

public static class TrackingDataManager
{
    public static void StartTracking(bool isLauchedProcess = false)
    {
        if (Declare.cts != null)
        {
            Declare.cts.Cancel();
        }

        Declare.cts = new CancellationTokenSource();
        var token = Declare.cts.Token;

        Task.Run(async () =>
        {
            var oldData = new Dictionary<string, string>();
            DataManager.LoadDisplayedItems();
            var clientHttp = new HttpClient();

            try
            {
                while (!token.IsCancellationRequested)
                {
                    if (string.IsNullOrEmpty(Declare.url))
                    {
                        Console.WriteLine("Aucune URL définie. Arrêt du suivi.");
                        break;
                    }

                    if (isLauchedProcess)
                    {
                        var trackerUrl = Declare.url;
                        trackerUrl.Replace("sphere_", "");

                        var html = await clientHttp.GetStringAsync(trackerUrl);
                        var doc = new HtmlDocument();
                        doc.LoadHtml(html);

                        var data = new Dictionary<string, string>();
                        var rows = doc.DocumentNode.SelectNodes("//table//tr");

                        foreach (var row in rows)
                        {
                            var cells = row.SelectNodes("td");

                            if (cells?.Count == 6)
                            {
                                var Name = cells[1].InnerText.Trim();
                                if (!Declare.aliasChoices.ContainsKey(Name))
                                {
                                    Declare.aliasChoices.Add(Name, Name);
                                }
                            }
                        }

                        await BotCommands.RegisterCommandsAsync();
                        await BotCommands.SendMessageAsync("Bot Ready ! GLHF !");

                        if (isLauchedProcess == true)
                        {
                            isLauchedProcess = false;
                        }
                    }

                    await GetTableDataAsync(Declare.url, clientHttp);

                    await Task.Delay(60000, token);
                }
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine("Suivi annulé.");
            }
        }, token);
    }

    static async Task GetTableDataAsync(string url, HttpClient client)
    {
        DataManager.LoadReceiverAliases();
        var html = await client.GetStringAsync(url);
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
        DataManager.SaveRecapList();
        DataManager.SaveDisplayedItems();
    }
}
