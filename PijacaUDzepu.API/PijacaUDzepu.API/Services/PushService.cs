using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.EntityFrameworkCore;
using PijacaUDzepu.API.DataAccess;
using PijacaUDzepu.API.Models;
using PijacaUDzepu.API.Services.Interfaces;

namespace PijacaUDzepu.API.Services;

// Push preko Firebase Cloud Messaging. Bez Firebase kredencijala ne šalje ništa,
// pa aplikacija radi i lokalno; obaveštenja u otvorenoj aplikaciji idu preko SignalR-a.
public class PushService : IPushService
{
    // Kanal mora da se poklapa sa kanalom koji klijent pravi na Androidu (zvuk + visok prioritet)
    public const string AndroidChannelId = "orders";

    private static readonly object InitLock = new();
    private static FirebaseMessaging? _messaging;
    private static bool _initialized;

    private readonly DataContext _context;
    private readonly ILogger<PushService> _logger;

    public PushService(DataContext context, ILogger<PushService> logger, IConfiguration config)
    {
        _context = context;
        _logger = logger;
        EnsureInitialized(config, logger);
    }

    private static void EnsureInitialized(IConfiguration config, ILogger logger)
    {
        if (_initialized) return;
        lock (InitLock)
        {
            if (_initialized) return;
            _initialized = true;

            var json = Environment.GetEnvironmentVariable("FIREBASE_CREDENTIALS_JSON");
            var path = Environment.GetEnvironmentVariable("FIREBASE_CREDENTIALS_PATH") ?? config["Firebase:CredentialsPath"];

            GoogleCredential? credential = null;
            if (!string.IsNullOrWhiteSpace(json))
                credential = GoogleCredential.FromJson(json);
            else if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                credential = GoogleCredential.FromFile(path);

            if (credential == null)
            {
                logger.LogWarning("Firebase nije podešen (FIREBASE_CREDENTIALS_JSON / FIREBASE_CREDENTIALS_PATH) - push obaveštenja su isključena.");
                return;
            }

            var app = FirebaseApp.DefaultInstance ?? FirebaseApp.Create(new AppOptions { Credential = credential });
            _messaging = FirebaseMessaging.GetMessaging(app);
            logger.LogInformation("Firebase push obaveštenja su uključena.");
        }
    }

    public async Task RegisterDevice(int userId, string token, string? platform)
    {
        // Isti uređaj može da promeni korisnika (odjava pa prijava drugim nalogom)
        var device = await _context.DeviceTokens.FirstOrDefaultAsync(d => d.Token == token);
        if (device == null)
        {
            _context.DeviceTokens.Add(new DeviceToken { UserId = userId, Token = token, Platform = platform });
        }
        else
        {
            device.UserId = userId;
            device.Platform = platform;
            device.LastSeenAt = DateTime.UtcNow;
        }
        await _context.SaveChangesAsync();
    }

    public async Task UnregisterDevice(string token)
    {
        await _context.DeviceTokens.Where(d => d.Token == token).ExecuteDeleteAsync();
    }

    public async Task SendToUsers(IEnumerable<int> userIds, string title, string body, string? url = null)
    {
        if (_messaging == null) return;

        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0) return;

        var tokens = await _context.DeviceTokens
            .Where(d => ids.Contains(d.UserId))
            .Select(d => d.Token)
            .ToListAsync();
        if (tokens.Count == 0) return;

        var message = new MulticastMessage
        {
            Tokens = tokens,
            Notification = new Notification { Title = title, Body = body },
            Data = new Dictionary<string, string> { ["url"] = url ?? "" },
            Android = new AndroidConfig
            {
                Priority = Priority.High,
                Notification = new AndroidNotification { ChannelId = AndroidChannelId, Sound = "default" }
            },
            Apns = new ApnsConfig { Aps = new Aps { Sound = "default" } }
        };

        try
        {
            var response = await _messaging.SendEachForMulticastAsync(message);

            // Tokeni deinstaliranih aplikacija se brišu da ne bi gomilali greške
            var invalid = response.Responses
                .Select((r, i) => (r, token: tokens[i]))
                .Where(x => !x.r.IsSuccess && x.r.Exception?.MessagingErrorCode is MessagingErrorCode.Unregistered or MessagingErrorCode.InvalidArgument)
                .Select(x => x.token)
                .ToList();
            if (invalid.Count > 0)
                await _context.DeviceTokens.Where(d => invalid.Contains(d.Token)).ExecuteDeleteAsync();
        }
        catch (Exception ex)
        {
            // Neuspeo push ne sme da obori porudžbinu
            _logger.LogError(ex, "Slanje push obaveštenja nije uspelo.");
        }
    }
}
