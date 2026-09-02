using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
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
    public void BuildReport_ItemMissingFromPlayerCatalog_IsNotReportedMissing()
    {
        var checks = new List<SpoilerAnalysisClass.Check>
        {
            new(1, "Chamber of Sages", "EtsunaZeldaOOT", "Time Travel", "EtsunaZeldaOOT"),
            new(2, "Real Check", "EtsunaZeldaOOT", "Progression Item", "EtsunaZeldaOOT")
        };
        var autoCompleted = new HashSet<string>(StringComparer.Ordinal)
        {
            "ITEM-CATALOG||ETSUNAZELDAOOT||",
            "KNOWN-ITEM||ETSUNAZELDAOOT||PROGRESSION ITEM"
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

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void BuildReport_OnlyReportsLocationsPresentInFinderCatalog(
        bool locationExistsInCatalog,
        bool shouldBeMissing)
    {
        var checks = new List<SpoilerAnalysisClass.Check>
        {
            new(1, "Zeldas Letter From Skip Option", "EtsunaZeldaOOT", "Zelda's Letter", "EtsunaZeldaOOT")
        };
        var catalog = new HashSet<string>(StringComparer.Ordinal)
        {
            "LOCATION-CATALOG||ETSUNAZELDAOOT||",
            "ITEM-CATALOG||ETSUNAZELDAOOT||",
            "KNOWN-ITEM||ETSUNAZELDAOOT||ZELDA'S LETTER"
        };
        if (locationExistsInCatalog)
        {
            catalog.Add("KNOWN-LOCATION||ETSUNAZELDAOOT||ZELDAS LETTER FROM SKIP OPTION");
        }

        var report = SpoilerAnalysisClass.BuildReport(
            checks,
            new HashSet<string>(StringComparer.Ordinal),
            "EtsunaZeldaOOT",
            sphereLimit: null,
            showAllMissing: false,
            hideItems: true,
            catalog);

        Assert.Equal(shouldBeMissing, report.Contains("Zeldas Letter From Skip Option"));
    }

    [Fact]
    public void BuildReport_SelectedAlias_ShowsOnlyPlayersItBlocksOnTheirCurrentSphere()
    {
        var checks = new List<SpoilerAnalysisClass.Check>
        {
            new(2, "my-needed-check", "Other", "My Item", "Selected"),
            new(3, "too-late-for-player-a", "Selected", "Later Item", "PlayerA"),
            new(1, "player-a-current-check", "Other", "Current Item", "PlayerA"),
            new(4, "player-b-current-check", "Selected", "Blocking Item", "PlayerB")
        };

        var report = SpoilerAnalysisClass.BuildReport(
            checks,
            new HashSet<string>(StringComparer.Ordinal),
            "Selected",
            sphereLimit: null,
            showAllMissing: false,
            hideItems: true);

        Assert.Contains("my-needed-check", report);
        Assert.Contains("Selected bloque actuellement d'autres joueurs : 1", report);
        Assert.Contains("player-b-current-check", report);
        Assert.DoesNotContain("too-late-for-player-a", report);
    }

    [Fact]
    public void BuildReport_SelectedAliasCanBlockOthersWithoutBeingBlocked()
    {
        var checks = new List<SpoilerAnalysisClass.Check>
        {
            new(1, "outbound-check", "Selected", "Their Item", "PlayerA")
        };

        var report = SpoilerAnalysisClass.BuildReport(
            checks,
            new HashSet<string>(StringComparer.Ordinal),
            "Selected",
            sphereLimit: null,
            showAllMissing: false,
            hideItems: true);

        Assert.Contains("Aucune check ne bloque actuellement Selected.", report);
        Assert.Contains("outbound-check", report);
    }

    [Fact]
    public void BuildReport_ManualValidation_AdvancesReceiverToNextSphereAndDeduplicatesChecks()
    {
        var checks = new List<SpoilerAnalysisClass.Check>
        {
            new(2, "outbound-s2", "Selected", "Other Item", "Other"),
            new(3, "local-s3", "Selected", "Local Item", "Selected"),
            new(3, "external-s3", "Other", "External Item", "Selected"),
            new(4, "local-s4", "Selected", "Later Local Item", "Selected"),
            new(4, "local-s4", "Selected", "Later Local Item", "Selected")
        };

        var report = SpoilerAnalysisClass.BuildReport(
            checks,
            new HashSet<string>(StringComparer.Ordinal),
            "Selected",
            sphereLimit: null,
            showAllMissing: true,
            hideItems: true,
            autoCompleted: null,
            manuallyValidatedSphere: 3);

        Assert.Contains("jusqu’à S3", report);
        Assert.DoesNotContain("local-s3", report);
        Assert.DoesNotContain("external-s3", report);
        Assert.Contains("Sphère actuellement bloquante : 4", report);
        Assert.Contains("local-s4", report);
        Assert.Equal(1, report.Split("local-s4", StringSplitOptions.None).Length - 1);
        Assert.Contains("outbound-s2", report);
    }

    [Fact]
    public async Task AnalyzeSpoilerLogAsync_NewValidatedSphereReplacesStoredValue()
    {
        using var scope = new TestDatabaseScope();
        var previousBasePath = Declare.BasePath;
        try
        {
            Declare.BasePath = scope.BaseDirectory;
            var spoilerFolder = SpoilerLogClass.GetSpoilerFolder("channel");
            Directory.CreateDirectory(spoilerFolder);
            await File.WriteAllTextAsync(Path.Combine(spoilerFolder, "spoiler.txt"), """
                Playthrough:
                1: {
                  loc-1 (Other): item-1 (Selected)
                }
                2: {
                  loc-2 (Other): item-2 (Selected)
                }
                3: {
                  loc-3 (Other): item-3 (Selected)
                }
                4: {
                  loc-4 (Other): item-4 (Selected)
                }
                5: {
                  loc-5 (Other): item-5 (Selected)
                }
                Paths:
                """);

            await SpoilerAnalysisClass.AnalyzeSpoilerLogAsync(
                "channel", "guild", "Selected", sphereToValidate: 5);
            var report = await SpoilerAnalysisClass.AnalyzeSpoilerLogAsync(
                "channel", "guild", "Selected", sphereToValidate: 2);

            Assert.Equal(
                1,
                await TestDatabaseScope.CountRowsAsync(
                    "SpoilerSphereValidationTable", "guild", "channel"));

            await using var connection = await Db.OpenReadAsync();
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT ValidatedSphere
                FROM SpoilerSphereValidationTable
                WHERE GuildId = 'guild' AND ChannelId = 'channel';
                """;
            Assert.Equal(2L, (long)(await command.ExecuteScalarAsync())!);
            Assert.Contains("Sphère actuellement bloquante : 3", report);
        }
        finally
        {
            Declare.BasePath = previousBasePath;
        }
    }
}
