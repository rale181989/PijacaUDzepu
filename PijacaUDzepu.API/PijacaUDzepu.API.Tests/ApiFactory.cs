using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;

namespace PijacaUDzepu.API.Tests;

// Pokreće ceo API (Development: migracije + demo podaci) nad posebnom bazom PijacaUDzepu_Test, koja se
// pre svakog pokretanja testova briše. Druga baza: env TEST_DATABASE_URL.
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("TEST_DATABASE_URL")
        ?? "Host=localhost;Port=5432;Database=PijacaUDzepu_Test;Username=postgres;Password=Postgres!";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public ApiFactory()
    {
        // Program čita bazu iz DATABASE_URL pre appsettings-a
        Environment.SetEnvironmentVariable("DATABASE_URL", ConnectionString);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Development");

    public async Task InitializeAsync()
    {
        var csb = new NpgsqlConnectionStringBuilder(ConnectionString);
        var database = csb.Database!;
        csb.Database = "postgres";
        await using var conn = new NpgsqlConnection(csb.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)", conn);
        await cmd.ExecuteNonQueryAsync();
        // Prvi klijent pokreće aplikaciju: migracije i demo podaci
        CreateClient();
    }

    Task IAsyncLifetime.DisposeAsync() => Task.CompletedTask;

    // Klijent sa (opciono) prijavljenim korisnikom. Svaki zahtev dobija drugu IP adresu (X-Forwarded-For),
    // da ograničenje broja prijava po IP adresi ne utiče na testove.
    public async Task<Api> As(string? userName = null, string? password = null)
    {
        var api = new Api(CreateClient());
        if (userName != null) await api.Login(userName, password!);
        return api;
    }
}

public class Api
{
    private static int _ip;
    public HttpClient Http { get; }

    public Api(HttpClient http) => Http = http;

    public async Task Login(string userName, string password)
    {
        var res = await Send(HttpMethod.Post, "/api/auth/login", new { userName, password });
        res.EnsureSuccessStatusCode();
        var body = await Read<JsonElement>(res);
        Http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("token").GetString());
    }

    public async Task<HttpResponseMessage> Send(HttpMethod method, string url, object? body = null)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Add("X-Forwarded-For", $"10.{Interlocked.Increment(ref _ip) % 250}.{Random.Shared.Next(250)}.1");
        if (body != null) req.Content = JsonContent.Create(body, options: ApiFactory.Json);
        return await Http.SendAsync(req);
    }

    public async Task<T> Get<T>(string url)
    {
        var res = await Send(HttpMethod.Get, url);
        res.EnsureSuccessStatusCode();
        return await Read<T>(res);
    }

    public static async Task<T> Read<T>(HttpResponseMessage res) =>
        (await res.Content.ReadFromJsonAsync<T>(ApiFactory.Json))!;

    public static async Task<string> Message(HttpResponseMessage res) =>
        (await Read<JsonElement>(res)).GetProperty("message").GetString() ?? "";
}

[CollectionDefinition("api")]
public class ApiCollection : ICollectionFixture<ApiFactory> { }
