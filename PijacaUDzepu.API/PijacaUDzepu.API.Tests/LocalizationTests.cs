using System.Net;
using System.Text.RegularExpressions;
using PijacaUDzepu.API.Helpers;

namespace PijacaUDzepu.API.Tests;

// Poruke API-ja prate jezik aplikacije (Accept-Language), a svaka L.T poruka u kodu mora imati prevod
[Collection("api")]
public class LocalizationTests
{
    private readonly ApiFactory _factory;

    public LocalizationTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public void Every_message_in_the_code_is_translated()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../PijacaUDzepu.API"));
        var missing = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .SelectMany(f => Regex.Matches(File.ReadAllText(f), "L\\.T\\(\\s*\"((?:[^\"\\\\]|\\\\.)*)\"").Select(m => m.Groups[1].Value))
            .Distinct()
            .Where(m => !L.Has(m))
            .ToList();

        Assert.True(missing.Count == 0, "Bez prevoda: " + string.Join(" | ", missing));
    }

    [Theory]
    [InlineData(null, "Pogrešno korisničko ime ili lozinka.")]
    [InlineData("en", "Wrong username or password.")]
    [InlineData("ru-RU,ru;q=0.9", "Неверное имя пользователя или пароль.")]
    [InlineData("de", "Pogrešno korisničko ime ili lozinka.")]
    public async Task Errors_follow_the_app_language(string? language, string expected)
    {
        var api = await _factory.As();
        if (language != null) api.Http.DefaultRequestHeaders.Add("Accept-Language", language);
        var res = await api.Send(HttpMethod.Post, "/api/auth/login", new { userName = "nepostoji" + Guid.NewGuid().ToString("N")[..8], password = "pogresna123" });

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        Assert.Equal(expected, await Api.Message(res));
    }

    [Fact]
    public async Task Validation_errors_get_a_translated_message()
    {
        var api = await _factory.As();
        api.Http.DefaultRequestHeaders.Add("Accept-Language", "en");
        var res = await api.Send(HttpMethod.Post, "/api/auth/register", new
        {
            userName = "kratka" + Random.Shared.Next(100000), password = "abc12", firstName = "A", lastName = "B", phoneNumber = "060"
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("The password must be at least 8 characters long.", await Api.Message(res));
    }
}
