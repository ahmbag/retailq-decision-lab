namespace RetailQ.DecisionLab.Core;

public sealed class DemoRetailDataProvider : IRetailDataProvider
{
    private static readonly IReadOnlyList<ProductSnapshot> Products = new List<ProductSnapshot>
    {
        P("100012", "Vollmilch 1 L", "Molkerei", 28, 5, 3, new[] {7,8,9,8,7,8,9,8,8,7,9,8,8,8}),
        P("100024", "Butter 250 g", "Molkerei", 44, 5, 3, new[] {5,6,5,6,5,5,6,5,6,5,5,6,5,6}),
        P("100031", "Joghurt Natur 500 g", "Molkerei", 19, 3, 2, new[] {5,5,4,5,6,5,5,4,5,6,5,5,4,5}),
        P("100045", "Mineralwasser 1 L", "Getränke", 180, 4, 3, new[] {11,12,10,12,11,13,12,11,12,10,11,12,13,12}),
        P("100056", "Orangensaft 1 L", "Getränke", 34, 4, 3, new[] {6,5,6,7,6,6,5,6,7,6,6,5,6,7}),
        P("100067", "Espresso Bohnen 1 kg", "Heißgetränke", 62, 7, 4, new[] {3,4,3,4,3,4,3,3,4,3,4,3,4,3}),
        P("100073", "Pasta Penne 500 g", "Grundnahrung", 83, 5, 3, new[] {5,4,5,5,6,5,4,5,5,6,5,4,5,5}),
        P("100081", "Tomaten passiert", "Grundnahrung", 26, 4, 3, new[] {4,5,4,5,4,5,5,4,5,4,5,4,5,4}),
        P("100094", "Olivenöl 500 ml", "Grundnahrung", 38, 6, 3, new[] {2,3,2,3,2,3,3,2,3,2,3,2,3,2}),
        P("100108", "Bananen lose", "Obst & Gemüse", 21, 2, 2, new[] {10,11,9,10,11,10,9,11,10,10,11,9,10,11}),
        P("100119", "Äpfel Elstar 1 kg", "Obst & Gemüse", 48, 3, 2, new[] {6,7,6,7,6,7,6,6,7,6,7,6,7,6}),
        P("100127", "Toastbrot Vollkorn", "Backwaren", 13, 2, 2, new[] {5,4,5,5,4,5,4,5,5,4,5,4,5,5}),
        P("100136", "Schokolade Dunkel 100 g", "Süßwaren", 73, 8, 4, new[] {1,2,1,2,2,1,2,1,2,1,2,2,1,2}),
        P("100142", "Küchenrolle 4er", "Haushalt", 35, 6, 3, new[] {2,2,3,2,3,2,2,3,2,2,3,2,3,2}),
        P("100155", "Saisontee Winter", "Heißgetränke", 25, 7, 3, new[] {0,0,0,0,0,0,0,0,0,0,0,0,0,0})
    };

    public RetailDataSourceStatus Status { get; } = new(
        "Demo", "SYNTHETISCHE DEMODATEN",
        "Produkt-, Bestands- und POS-Daten stammen aus dem lokalen Demo-Datensatz.", false, true, 15,
        RefreshedAtUtc: DateTimeOffset.UtcNow);

    public IReadOnlyList<ProductSnapshot> GetProducts() => Products;
    public ProductSnapshot? GetProduct(string id) => Products.SingleOrDefault(x => x.Id == id);
    private static ProductSnapshot P(string sku, string name, string category, int stock, int lead, int safety, int[] sales) =>
        new(sku, sku, name, category, "ST", stock, sales, lead, safety);
}
