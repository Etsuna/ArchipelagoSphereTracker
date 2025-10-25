using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

static void Notify(string msg) => Console.WriteLine(msg);

try
{
    var skipByEnv = string.Equals(
        Environment.GetEnvironmentVariable("UPDATE_CHECK"),
        "false",
        StringComparison.OrdinalIgnoreCase);

    var skipByArg = args.Any(a =>
        string.Equals(a, "--no-update", StringComparison.OrdinalIgnoreCase));

    if (!skipByEnv && !skipByArg)
    {
        await CheckUpdate.CheckAsync(
            owner: "Etsuna",
            repo: "ArchipelagoSphereTracker",
            notify: Notify
        );
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Échec vérification MAJ : {ex.GetType().Name}: {ex.Message}");
}

var ver = Assembly.GetEntryAssembly()?
              .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
              .InformationalVersion
          ?? Assembly.GetEntryAssembly()?.GetName().Version?.ToString()
          ?? "inconnue";

await Task.CompletedTask;
