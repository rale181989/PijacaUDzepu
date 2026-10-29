using System.Net;
using MailKit.Net.Smtp;
using MimeKit;
using PijacaUDzepu.API.Helpers;
using PijacaUDzepu.API.Models;
using PijacaUDzepu.API.Models.Enums;
using PijacaUDzepu.API.Services.Interfaces;

namespace PijacaUDzepu.API.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;
    private readonly string _frontendUrl;

    public EmailService(IConfiguration config, ILogger<EmailService> logger, FrontendUrlResolver frontendUrl)
    {
        _config = config;
        _logger = logger;
        // Adresa se određuje odmah, dok traje zahtev, jer se e-mail za praćenje šalje u pozadini
        _frontendUrl = frontendUrl.Resolve();
    }

    private bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_config["Email:SmtpUser"]) && !string.IsNullOrWhiteSpace(_config["Email:SmtpPass"]);

    public async Task SendVendorInvitation(string toEmail, string vendorName, string inviteToken)
    {
        var link = $"{_frontendUrl}/setup-account?token={inviteToken}";

        var body = $@"
        <h2>Zdravo!</h2>
        <p>Pozvani ste da upravljate prodavnicom <strong>{Encode(vendorName)}</strong> na platformi Pijaca u Džepu.</p>
        <p>Kliknite na dugme ispod da kreirate svoj nalog:</p>
        {Button(link, "Kreiraj nalog")}
        <p style=""color: #666; font-size: 14px;"">Link važi 7 dana. Ako niste očekivali ovaj email, slobodno ga ignorišite.</p>";

        await Send(toEmail, "Poziv za registraciju - Pijaca u Džepu", body);
    }

    public async Task SendTrackingLink(string toEmail, string guestName, string trackingCode, IReadOnlyList<Order> orders)
    {
        // Lokalno i bez Brevo pristupa e-mail se ne šalje; porudžbina radi i bez njega
        if (!IsConfigured)
        {
            _logger.LogWarning("Email nije podešen (Email:SmtpUser/SmtpPass) - link za praćenje nije poslat na {Email}.", toEmail);
            return;
        }

        var link = $"{_frontendUrl}/prati/{trackingCode}";
        var rows = string.Join("", orders.Select(o => $@"
            <tr>
                <td style=""padding: 8px; border-bottom: 1px solid #ddd;"">
                    <strong>#{o.Id} {Encode(o.Vendor.Name)}</strong><br>
                    <span style=""color: #666; font-size: 13px;"">
                        {(o.Fulfillment == FulfillmentType.Pickup ? "Rezervacija – preuzimate kod prodavca" : "Dostava – Eko kurir")}
                        · {string.Join(", ", o.Items.Select(i => Encode(i.ProductName)))}
                    </span>
                </td>
                <td style=""padding: 8px; border-bottom: 1px solid #ddd; text-align: right; white-space: nowrap;"">
                    {o.TotalAmount + o.DeliveryFee:0} din
                </td>
            </tr>"));

        var body = $@"
        <h2>Hvala, {Encode(guestName)}!</h2>
        <p>Vaša porudžbina je primljena. Status, odgovor prodavca i podatke o kuriru pratite preko linka:</p>
        {Button(link, "Prati porudžbinu")}
        <table style=""width: 100%; border-collapse: collapse; background: white;"">{rows}</table>
        <p style=""color: #666; font-size: 13px;"">Link važi dok je porudžbina u toku i još 30 dana posle. Ne prosleđujte ga drugima – preko njega se porudžbina može i otkazati dok je prodavac ne potvrdi.</p>";

        await Send(toEmail, $"Porudžbina primljena - Pijaca u Džepu", body);
    }

    public async Task SendSupportRequest(SupportRequest request)
    {
        var supportEmail = _config["Support:Email"] ?? "zivkovic_kac@live.com";
        if (!IsConfigured)
        {
            _logger.LogWarning("Email nije podešen (Email:SmtpUser/SmtpPass) - pitanje #{Id} nije prosleđeno na {Email}; vidljivo je u admin panelu.", request.Id, supportEmail);
            return;
        }

        var categories = new Dictionary<string, string>
        {
            ["Problem"] = "Problem sa aplikacijom",
            ["Question"] = "Pitanje",
            ["Account"] = "Nalog i prijava",
            ["Other"] = "Ostalo"
        };
        var category = categories.GetValueOrDefault(request.Category, request.Category);

        var body = $@"
        <h2>Novo pitanje #{request.Id} – {Encode(category)}</h2>
        <p><strong>Od:</strong> {Encode(request.Name)} &lt;{Encode(request.Email)}&gt;{(request.UserId.HasValue ? $" · korisnik #{request.UserId}" : " · gost")}</p>
        <div style=""background: white; padding: 16px; border-radius: 6px; white-space: pre-wrap;"">{Encode(request.Message)}</div>
        <p style=""color: #666; font-size: 13px;"">Uređaj: {Encode(request.Platform)} · {Encode(request.UserAgent)}</p>
        {Button($"{_frontendUrl}/admin-support", "Otvori u admin panelu")}
        <p style=""color: #666; font-size: 13px;"">Odgovorom na ovaj e-mail pišete direktno korisniku.</p>";

        await Send(supportEmail, $"[Podrška #{request.Id}] {category} – {request.Name}", body,
            replyTo: new MailboxAddress(request.Name, request.Email));
    }

    private async Task Send(string toEmail, string subject, string bodyHtml, MailboxAddress? replyTo = null)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(
            _config["Email:SenderName"] ?? "Pijaca u Džepu",
            _config["Email:SenderEmail"] ?? "noreply@pijacaudzepu.rs"));
        message.To.Add(MailboxAddress.Parse(toEmail));
        if (replyTo != null) message.ReplyTo.Add(replyTo);
        message.Subject = subject;
        message.Body = new TextPart("html")
        {
            Text = $@"
<div style=""font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;"">
    <div style=""background-color: #2e7d32; color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0;"">
        <h1 style=""margin: 0;"">🌿 Pijaca u Džepu</h1>
    </div>
    <div style=""background-color: #f5f5f5; padding: 30px; border-radius: 0 0 8px 8px;"">
        {bodyHtml}
    </div>
</div>"
        };

        using var smtp = new SmtpClient();
        await smtp.ConnectAsync(
            _config["Email:SmtpHost"] ?? "smtp-relay.brevo.com",
            int.Parse(_config["Email:SmtpPort"] ?? "587"),
            MailKit.Security.SecureSocketOptions.StartTls);
        await smtp.AuthenticateAsync(
            _config["Email:SmtpUser"] ?? "",
            _config["Email:SmtpPass"] ?? "");
        await smtp.SendAsync(message);
        await smtp.DisconnectAsync(true);
    }

    private static string Button(string link, string text) => $@"
        <div style=""text-align: center; margin: 30px 0;"">
            <a href=""{Encode(link)}"" style=""background-color: #2e7d32; color: white; padding: 14px 28px; text-decoration: none; border-radius: 6px; font-size: 16px; font-weight: bold;"">
                {text}
            </a>
        </div>";

    // Sve što unosi korisnik (ime, nazivi) ide kroz HTML escaping
    private static string Encode(string? value) => WebUtility.HtmlEncode(value ?? "");
}
