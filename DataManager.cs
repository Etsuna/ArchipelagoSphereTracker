using Newtonsoft.Json;
using System.Reflection;

public static class DataManager
{
    private static void LoadData<T>(string filePath, ref T data, Action<T>? clearAction = null)
    {
        if (clearAction != null && data != null)
        {
            clearAction(data);
        }

        if (File.Exists(filePath))
        {
            var json = File.ReadAllText(filePath);
            data = JsonConvert.DeserializeObject<T>(json) ?? throw new InvalidOperationException("Deserialization returned null");
        }
    }

    private static void LoadApWorld<T>(ref T data, Action<T>? clearAction = null)
    {
        if (clearAction != null && data != null)
        {
            clearAction(data);
        }

        string json;

        var resourceName = "ArchipelagoSphereTracker.APWorldList.json";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
                          ?? throw new FileNotFoundException($"Ressource embarquée introuvable: {resourceName}");

        using var reader = new StreamReader(stream);
        json = reader.ReadToEnd();

        data = JsonConvert.DeserializeObject<T>(json) ?? throw new InvalidOperationException("Deserialization returned null");
    }


    private static void SaveData<T>(string filePath, T data, Formatting formatting = Formatting.None)
    {
        var json = JsonConvert.SerializeObject(data, formatting);
        File.WriteAllText(filePath, json);
    }

    public static void LoadItemsTable() => LoadData(Declare.ItemsTableFile, ref Declare.ItemsTable);

    public static void LoadApWorldJsonList() => LoadApWorld(ref Declare.ApworldsInfo);
}
