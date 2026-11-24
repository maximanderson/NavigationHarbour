using System.Reflection;

namespace Portal.Api.Configuration;

/// <summary>
/// Product identity for user-facing text (startup messages, diagnostics). The values are written into the
/// assembly at build time from product.json; nothing is read from disk at runtime. Code and namespaces stay
/// product-neutral: never hard-code the product name in messages, use <see cref="Name"/>.
/// </summary>
public static class ProductInfo
{
    private static readonly Assembly Source = typeof(ProductInfo).Assembly;

    /// <summary>Display name, for example "Navigation Harbour".</summary>
    public static string Name { get; } = Source.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? "Portal";

    /// <summary>Technical name used for the executable and distribution folder, for example "NavigationHarbour".</summary>
    public static string TechnicalName { get; } = Metadata("ProductTechnicalName") ?? Source.GetName().Name ?? "Portal";

    public static string Tagline { get; } = Metadata("ProductTagline") ?? string.Empty;

    public static string Description { get; } = Source.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description ?? string.Empty;

    private static string? Metadata(string key) =>
        Source.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == key)?.Value;
}
