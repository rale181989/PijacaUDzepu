using System.Threading.RateLimiting;
using Microsoft.Extensions.FileProviders;
using PijacaUDzepu.API.DataAccess.SeedData;
using PijacaUDzepu.API.Extensions;
using PijacaUDzepu.API.Helpers;
using PijacaUDzepu.API.Controllers;
using PijacaUDzepu.API.Hubs;
using PijacaUDzepu.API.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Unesite JWT token"
    });
    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddIdentityServices(builder.Configuration);

// Lokalni klijenti i Render rade paralelno; AllowedOrigins (zarezima odvojeno) dodaje još adresa
var allowedOrigins = FrontendUrlResolver.KnownUrls(builder.Configuration)
    .Concat(builder.Configuration["AllowedOrigins"]?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? Array.Empty<string>())
    .ToArray();

// Capacitor mobilna aplikacija (Android: https://localhost, iOS: capacitor://localhost)
allowedOrigins = allowedOrigins
    .Concat(new[] { "https://localhost", "capacitor://localhost" })
    .Distinct()
    .ToArray();

builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// Zaštita forme za podršku od spama: najviše 5 pitanja na 10 minuta po IP adresi.
// Iza Render proxy-ja prava adresa je u X-Forwarded-For.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(SupportController.RateLimitPolicy, context =>
    {
        var ip = context.Request.Headers["X-Forwarded-For"].ToString().Split(',')[0].Trim();
        if (string.IsNullOrEmpty(ip)) ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(10)
        });
    });
});

var app = builder.Build();

app.UseMiddleware<ExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("CorsPolicy");

app.UseDefaultFiles();
app.UseStaticFiles();

var resourcesPath = Path.Combine(builder.Environment.WebRootPath, "Resources");
if (Directory.Exists(resourcesPath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(resourcesPath),
        RequestPath = "/Resources"
    });
}

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();
app.MapHub<OrderHub>("/hubs/orders");
app.MapHub<TrackingHub>("/hubs/tracking");
// Nepostojeće API rute vraćaju 404, a ne index.html (inače klijent dobije HTML umesto JSON-a)
app.MapFallback("/api/{**path}", context =>
{
    context.Response.StatusCode = StatusCodes.Status404NotFound;
    return Task.CompletedTask;
});
app.MapFallbackToFile("index.html");

await Seed.SeedData(app.Services);

app.Run();
