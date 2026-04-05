using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

public class SpoilerAnalysisClassTests
{
    private static readonly string SpoilerPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "SpoilerTest", "AP_39818916018271536012_Spoiler.txt"));

    [Fact]
    public void ParseSpoiler_ExtractsPlaythroughChecksAndPaths()
    {
        var (checks, paths) = SpoilerAnalysisClass.ParseSpoiler(SpoilerPath);

        Assert.NotEmpty(checks);
        Assert.NotEmpty(paths);

        Assert.Contains(checks, c => c.Receiver == "SurdjakShop");
        Assert.Contains(paths.Keys, key => key.Finder == "aNonofai");
    }

    [Fact]
    public void BuildReport_WithNoFoundItems_ForSurdjakShop_ReturnsMissingItems()
    {
        var (checks, paths) = SpoilerAnalysisClass.ParseSpoiler(SpoilerPath);
        var found = new HashSet<(string Finder, string Receiver, string Item, string Location)>();

        var report = SpoilerAnalysisClass.BuildReport(checks, paths, found, "SurdjakShop", sphereLimit: null, showAllMissing: true);

        Assert.Contains("Items manquants du Playthrough", report);
        Assert.DoesNotContain("Aucun item manquant", report);
        Assert.Contains("SurdjakShop", report);
    }

    [Fact]
    public void BuildReport_LowestSphereOnly_OnlyContainsOneSphere()
    {
        var checks = new List<SpoilerAnalysisClass.Check>
        {
            new(1, "loc-1", "finder-1", "item-1", "SurdjakShop"),
            new(2, "loc-2", "finder-2", "item-2", "SurdjakShop"),
            new(2, "loc-3", "finder-3", "item-3", "SurdjakShop")
        };

        var paths = new Dictionary<(string Location, string Finder), List<string>>
        {
            [("loc-1", "finder-1")] = new List<string> { "Menu -> loc-1" },
            [("loc-2", "finder-2")] = new List<string> { "Menu -> loc-2" },
            [("loc-3", "finder-3")] = new List<string> { "Menu -> loc-3" }
        };

        var found = new HashSet<(string Finder, string Receiver, string Item, string Location)>();

        var report = SpoilerAnalysisClass.BuildReport(checks, paths, found, "SurdjakShop", sphereLimit: null, showAllMissing: false);

        Assert.Contains("sphère la plus basse (1)", report);
        Assert.Contains("- s1:", report);
        Assert.DoesNotContain("- s2:", report);
    }
}
