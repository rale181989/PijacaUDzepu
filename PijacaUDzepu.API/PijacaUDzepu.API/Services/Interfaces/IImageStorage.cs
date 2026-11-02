namespace PijacaUDzepu.API.Services.Interfaces;

public interface IImageStorage
{
    // Snima sliku pod ključem (npr. "products/product_5_<guid>.jpg") i vraća URL koji ide u bazu
    Task<string> Save(byte[] content, string key, string contentType);

    // Briše sliku koju je snimio ovaj storage; tuđe URL-ove (seed slike, stari lokalni fajlovi) preskače
    Task Delete(string? imageUrl);
}
