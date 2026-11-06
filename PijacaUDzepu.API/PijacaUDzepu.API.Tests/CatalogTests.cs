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

    // Android galerija šalje sliku bez ekstenzije u imenu; prihvata se ako je sadržaj slika
    [Fact]
    public async Task Product_image_without_extension_is_accepted_but_text_is_not()
    {
        var vendor = await _factory.As("ivana", "vendor1234");
        var product = (await vendor.Get<List<MyProduct>>("/api/products/my-products"))[0];
        // 1x1 PNG
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

        async Task<HttpResponseMessage> Upload(byte[] bytes, string fileName, string contentType)
        {
            var file = new ByteArrayContent(bytes);
            file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
            var form = new MultipartFormDataContent { { file, "file", fileName } };
            return await vendor.Http.PostAsync($"/api/products/{product.Id}/image", form);
        }

        var ok = await Upload(png, "image:1000012345", "image/png");
        Assert.Equal(System.Net.HttpStatusCode.OK, ok.StatusCode);
        var updated = await vendor.Get<Product>($"/api/products/{product.Id}");
        Assert.EndsWith(".jpg", updated.ImageUrl);

        var bad = await Upload("nije slika"u8.ToArray(), "slika", "image/jpeg");
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Equal("Neispravan format slike.", await Api.Message(bad));
    }

    private record MyProduct(int Id);
}
