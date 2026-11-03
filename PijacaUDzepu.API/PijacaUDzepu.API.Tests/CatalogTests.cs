namespace PijacaUDzepu.API.Tests;

// Katalog: demo proizvodi su u pravim kategorijama (začinsko bilje, mahunarke, pčelinji proizvodi)
[Collection("api")]
public class CatalogTests
{
    private readonly ApiFactory _factory;

    public CatalogTests(ApiFactory factory) => _factory = factory;

    private record Product(string Name, string Category, string? ImageUrl);
    private record Paged<T>(List<T> Items, int TotalCount);

    [Theory]
    [InlineData("ZacinskoBilje", "Peršun")]
    [InlineData("Mahunarke", "Pasulj Tetovac")]
    [InlineData("PcelinjiProizvodi", "Domaći Med Lipov")]
    public async Task Demo_products_are_in_the_right_category(string category, string product)
    {
        var api = await _factory.As();
        var page = await api.Get<Paged<Product>>($"/api/products?category={category}&take=50");

        Assert.Contains(page.Items, p => p.Name == product);
        Assert.All(page.Items, p => Assert.Equal(category, p.Category));
    }

    [Fact]
    public async Task Herbs_are_no_longer_vegetables()
    {
        var api = await _factory.As();
        var page = await api.Get<Paged<Product>>("/api/products?category=Povrce&take=100");

        Assert.DoesNotContain(page.Items, p => p.Name is "Peršun" or "Bosiljak Sveži" or "Mirođija" or "Nana Sveža");
    }
}
