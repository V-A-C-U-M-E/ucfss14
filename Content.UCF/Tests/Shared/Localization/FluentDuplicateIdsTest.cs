using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Content.Tests.Shared.UCF14.Localization;

[TestFixture]
public sealed class FluentDuplicateIdsTest
{
    private static readonly Regex MessageId = new(@"^(-?[A-Za-z][A-Za-z0-9_-]*)\s*=", RegexOptions.Compiled);

    private static readonly string[] LocaleDirectories =
    {
        "Resources/Locale/en-US",
        "Content.CMU/Resources/Locale/en-US",
        "Content.UCF/Resources/Locale/en-US",
    };

    [Test]
    public void EnglishLocaleFilesDefineEachMessageOnce()
    {
        var root = FindRepositoryRoot();
        var definitions = new Dictionary<string, List<string>>();

        foreach (var directory in LocaleDirectories)
        {
            var path = Path.Combine(root, directory);
            if (!Directory.Exists(path))
                continue;

            foreach (var file in Directory.EnumerateFiles(path, "*.ftl", SearchOption.AllDirectories))
            {
                var lines = File.ReadAllLines(file);
                for (var i = 0; i < lines.Length; i++)
                {
                    var match = MessageId.Match(lines[i]);
                    if (!match.Success)
                        continue;

                    var id = match.Groups[1].Value;
                    if (!definitions.TryGetValue(id, out var locations))
                        definitions[id] = locations = new List<string>();

                    locations.Add($"{Path.GetRelativePath(root, file)}:{i + 1}");
                }
            }
        }

        var duplicates = definitions
            .Where(pair => pair.Value.Count > 1)
            .Select(pair => $"{pair.Key}: {string.Join(", ", pair.Value)}")
            .ToList();

        Assert.That(duplicates, Is.Empty, string.Join(Environment.NewLine, duplicates));
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "Resources")))
                return directory.FullName;
        }

        Assert.Fail("Repository root was not found from the test output directory.");
        return string.Empty;
    }
}
