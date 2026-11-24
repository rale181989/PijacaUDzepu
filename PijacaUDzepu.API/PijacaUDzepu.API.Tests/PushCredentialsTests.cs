using System.Text.Json;
using PijacaUDzepu.API.Services;

namespace PijacaUDzepu.API.Tests;

// FIREBASE_CREDENTIALS_JSON zalepljen na Render bez prve i poslednje vitičaste zagrade (desilo se):
// server ih dodaje, pa push ipak radi
public class PushCredentialsTests
{
    private const string Body = "\"type\": \"service_account\",\n  \"project_id\": \"test\",\n  \"universe_domain\": \"googleapis.com\"\n";

    [Theory]
    [InlineData("{\n" + Body + "}")]
    [InlineData(Body)]
    [InlineData("  " + Body + "  ")]
    public void Credentials_json_is_valid_with_or_without_braces(string pasted)
    {
        using var doc = JsonDocument.Parse(PushService.NormalizeCredentialsJson(pasted));
        Assert.Equal("service_account", doc.RootElement.GetProperty("type").GetString());
        Assert.Equal("test", doc.RootElement.GetProperty("project_id").GetString());
    }
}
