using System.Reflection;

namespace RetailQ.DecisionLab.Infrastructure.SAP;

/// <summary>Loads versioned, documented OData response samples packaged with this assembly.</summary>
public static class SapFixtureData
{
    public static string ProductsV4() => Read("products-v4.json");
    public static string MaterialStockV2() => Read("material-stock-v2.json");

    private static string Read(string fileName)
    {
        var assembly = typeof(SapFixtureData).Assembly;
        var resource = assembly.GetManifestResourceNames()
            .SingleOrDefault(name => name.EndsWith($".SAP.Fixtures.{fileName}", StringComparison.Ordinal));
        if (resource is null) throw new InvalidOperationException($"Embedded SAP fixture '{fileName}' was not found.");
        using var stream = assembly.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
