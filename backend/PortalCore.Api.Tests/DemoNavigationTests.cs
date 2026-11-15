using System.Text.Json;

namespace PortalCore.Api.Tests;

/// <summary>Stage 7: demo/ navigation and Branch Screen files follow the existing schemas and are consistent.</summary>
public class DemoNavigationTests
{
    internal static string Root { get; } = FindRoot();

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PortalCore.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("PortalCore.sln not found above the test binaries.");
    }

    internal static string Demo(params string[] parts) => Path.Combine(new[] { Root, "demo" }.Concat(parts).ToArray());

    private static JsonElement Load(string file)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(file));
        return document.RootElement.Clone();
    }

    private static List<JsonElement> Nodes(JsonElement array)
    {
        var all = new List<JsonElement>();
        void Walk(JsonElement list)
        {
            foreach (var node in list.EnumerateArray())
            {
                all.Add(node);
                if (node.TryGetProperty("children", out var children)) Walk(children);
            }
        }

        Walk(array);
        return all;
    }

    private static JsonElement Navigation() => Load(Demo("navigation", "demo-navigation.json"));

    [Fact]
    public void Navigation_FollowsTheSchema_WithUniqueNonEmptyStringIds()
    {
        var nodes = Nodes(Navigation());

        Assert.True(nodes.Count >= 100, $"only {nodes.Count} nodes");
        foreach (var node in nodes)
        {
            Assert.Equal(JsonValueKind.String, node.GetProperty("id").ValueKind);
            Assert.False(string.IsNullOrWhiteSpace(node.GetProperty("id").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(node.GetProperty("title").GetString()));
        }

        var ids = nodes.Select(n => n.GetProperty("id").GetString()!).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void Navigation_IsDeepAndBroad_ForSearchAndExpansion()
    {
        var root = Navigation();
        int Depth(JsonElement node) =>
            1 + (node.TryGetProperty("children", out var c) && c.GetArrayLength() > 0 ? c.EnumerateArray().Max(Depth) : 0);

        Assert.True(root.EnumerateArray().Max(Depth) >= 4);

        var titles = Nodes(root).Select(n => n.GetProperty("title").GetString()!).ToList();
        Assert.True(titles.Count(t => t.Contains("pump", StringComparison.OrdinalIgnoreCase)) >= 5);
        Assert.True(titles.Count(t => t.Contains("flow", StringComparison.OrdinalIgnoreCase)) >= 5);
    }

    [Fact]
    public void OnlyLeafNodes_AreClickable_AndNoUrlPointsAtAServer()
    {
        foreach (var node in Nodes(Navigation()))
        {
            var hasChildren = node.TryGetProperty("children", out var c) && c.GetArrayLength() > 0;
            var hasUrl = node.TryGetProperty("url", out var url);
            Assert.Equal(!hasChildren, hasUrl);
            if (hasUrl) Assert.StartsWith("about:blank", url.GetString());
        }
    }

    [Fact]
    public void BranchScreen_EveryNavIdResolves_WithSeveralColumnsAndARestoreEntry()
    {
        var ids = Nodes(Navigation()).Select(n => n.GetProperty("id").GetString()!).ToHashSet();
        var columns = Load(Demo("navigation", "demo-branches.json")).EnumerateArray().ToList();

        Assert.True(columns.Count >= 4);
        foreach (var column in columns)
        {
            Assert.False(string.IsNullOrWhiteSpace(column.GetProperty("title").GetString()));
            foreach (var branch in column.GetProperty("branches").EnumerateArray())
            {
                Assert.False(string.IsNullOrWhiteSpace(branch.GetProperty("title").GetString()));
                Assert.Contains(branch.GetProperty("navID").GetString()!, ids);
            }
        }

        var restore = columns.SelectMany(c => c.GetProperty("branches").EnumerateArray())
            .Single(b => b.GetProperty("title").GetString() == "Restore menu");
        Assert.Equal(Navigation()[0].GetProperty("id").GetString(), restore.GetProperty("navID").GetString());
    }
}
