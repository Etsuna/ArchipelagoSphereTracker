using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

public class SpoilerAnalysisClassTests
{
    [Fact]
    public void ParsePlaythrough_ExtractsChecks()
    {
        var spoilerPath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(spoilerPath, """
                Playthrough:
                1: {
                  A Location (Finder): An Item (Receiver)
                }
                Paths:
                """);

            var checks = SpoilerAnalysisClass.ParsePlaythrough(spoilerPath);

            Assert.Single(checks);
            Assert.Equal(
                new SpoilerAnalysisClass.Check(1, "A Location", "Finder", "An Item", "Receiver"),
                checks[0]);
        }
        finally
        {
            File.Delete(spoilerPath);
        }
    }

    [Fact]
    public void BuildReport_AutoCompletedLocation_IsNotReportedMissing()
    {
        var checks = new List<SpoilerAnalysisClass.Check>
        {
            new(1, "Chamber of Sages", "EtsunaZeldaOOT", "Time Travel", "EtsunaZeldaOOT"),
            new(2, "Real Check", "EtsunaZeldaOOT", "Progression Item", "EtsunaZeldaOOT")
        };
        var autoCompleted = new HashSet<string>(StringComparer.Ordinal)
        {
            "LOCATION||ETSUNAZELDAOOT||CHAMBER OF SAGES"
        };

        var report = SpoilerAnalysisClass.BuildReport(
            checks,
            new HashSet<string>(StringComparer.Ordinal),
            "EtsunaZeldaOOT",
            sphereLimit: null,
            showAllMissing: true,
            hideItems: true,
            autoCompleted);

        Assert.DoesNotContain("Chamber of Sages", report);
        Assert.Contains("Real Check", report);
        Assert.Contains("Sphère actuellement bloquante : 2", report);
    }

    [Fact]
    public void BuildReport_LowestSphereOnly_OnlyContainsOneSphere()
    {
        var checks = new List<SpoilerAnalysisClass.Check>
        {
            new(1, "loc-1", "finder-1", "item-1", "SurdjakShop"),
            new(2, "loc-2", "finder-2", "item-2", "SurdjakShop")
        };

        var report = SpoilerAnalysisClass.BuildReport(
            checks,
            new HashSet<string>(StringComparer.Ordinal),
            "SurdjakShop",
            sphereLimit: null,
            showAllMissing: false,
            hideItems: true);

        Assert.Contains("Sphère actuellement bloquante : 1", report);
        Assert.Contains("[S1]", report);
        Assert.DoesNotContain("[S2]", report);
    }
}
