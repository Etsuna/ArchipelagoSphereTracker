using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

public sealed class ResourceLocalizationTests
{
    [Fact]
    public void EnglishFrenchAndDesignerResourceKeysStayInSync()
    {
        var root = FindRepositoryRoot();
        var english = LoadResources(Path.Combine(root, "src", "Resources", "Resource.resx"));
        var french = LoadResources(Path.Combine(root, "src", "Resources", "Resource.fr.resx"));
        var designer = File.ReadAllText(Path.Combine(root, "src", "Resources", "Resource.Designer.cs"));
        var designerKeys = Regex.Matches(
                designer,
                @"internal static string (?<key>[A-Za-z_][A-Za-z0-9_]*)\s*\{")
            .Select(match => match.Groups["key"].Value)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Empty(english.Keys.Except(french.Keys, StringComparer.Ordinal));
        Assert.Empty(french.Keys.Except(english.Keys, StringComparer.Ordinal));
        Assert.Empty(english.Keys.Except(designerKeys, StringComparer.Ordinal));

        foreach (var key in english.Keys)
            Assert.Equal(Placeholders(english[key]), Placeholders(french[key]));
    }

    [Fact]
    public void SourceDoesNotSelectLocalizedMessagesWithLanguageConditionals()
    {
        var sourceRoot = Path.Combine(FindRepositoryRoot(), "src");
        foreach (var path in Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(path);
            Assert.DoesNotContain("IsFrench", source, StringComparison.Ordinal);
            Assert.DoesNotMatch("Declare\\.Language\\s*==\\s*\"fr\"", source);
            Assert.DoesNotMatch("string\\.Equals\\(Declare\\.Language\\s*,\\s*\"fr\"", source);
        }
    }

    private static Dictionary<string, string> LoadResources(string path)
        => XDocument.Load(path).Root!.Elements("data")
            .Where(element => element.Attribute("name") != null)
            .ToDictionary(
                element => element.Attribute("name")!.Value,
                element => element.Element("value")?.Value ?? string.Empty,
                StringComparer.Ordinal);

    private static string[] Placeholders(string value)
        => Regex.Matches(value, @"(?<!\{)\{(?<index>\d+)(?:[^}]*)\}(?!\})")
            .Select(match => match.Groups["index"].Value)
            .OrderBy(index => index, StringComparer.Ordinal)
            .ToArray();

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory != null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "src", "Resources", "Resource.resx")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Repository root containing src/Resources/Resource.resx was not found.");
    }
}
