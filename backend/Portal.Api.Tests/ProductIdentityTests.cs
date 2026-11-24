using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Portal.Api.Configuration;

namespace Portal.Api.Tests;

/// <summary>
/// product.json is the build-time product identity. The build reads it with a pattern (MSBuild has no JSON parser);
/// these tests parse it with a real JSON parser and check that the built assembly agrees, and that the
/// technical names stay product-neutral.
/// </summary>
public class ProductIdentityTests
{
    private static JsonElement ReadProductJson()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "product.json")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return JsonDocument.Parse(File.ReadAllText(Path.Combine(directory!.FullName, "product.json"))).RootElement.Clone();
    }

    private static string Value(string name) => ReadProductJson().GetProperty(name).GetString()!;

    [Fact]
    public void ProductJson_HasAllRequiredProperties_AndASafeTechnicalName()
    {
        foreach (var name in new[] { "displayName", "technicalName", "tagline", "description" })
        {
            Assert.False(string.IsNullOrWhiteSpace(Value(name)), name);
        }

        Assert.Matches("^[A-Za-z][A-Za-z0-9]{0,39}$", Value("technicalName"));
    }

    [Fact]
    public void ProductInfo_MatchesProductJson()
    {
        Assert.Equal(Value("displayName"), ProductInfo.Name);
        Assert.Equal(Value("technicalName"), ProductInfo.TechnicalName);
        Assert.Equal(Value("tagline"), ProductInfo.Tagline);
        Assert.Equal(Value("description"), ProductInfo.Description);
    }

    [Fact]
    public void Executable_IsNamedAfterTheTechnicalName_ButNamespacesAreNot()
    {
        var api = typeof(ProductInfo).Assembly;

        Assert.Equal(Value("technicalName"), api.GetName().Name);
        Assert.StartsWith("Portal.Api", typeof(ProductInfo).Namespace);
    }

    [Fact]
    public void OperationalNames_AreProductNeutral()
    {
        var product = new[] { Value("technicalName"), Value("displayName").Replace(" ", "") };
        var names = new[]
        {
            Portal.Api.Configuration.Server.DatabaseLocation.FileName,
            Portal.Api.Configuration.Server.PortalHome.EnvironmentVariable,
            Portal.Api.Configuration.Server.ServerConfiguration.EnvironmentPrefix,
            Portal.Api.Configuration.Server.ServerConfiguration.ConnectionStringKey,
        };

        foreach (var name in names)
        {
            foreach (var p in product)
            {
                Assert.DoesNotContain(p, name, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
