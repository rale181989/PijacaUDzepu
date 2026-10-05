using MailKit.Net.Smtp;
using MimeKit;
using PijacaUDzepu.API.Services.Interfaces;

namespace PijacaUDzepu.API.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;

    public EmailService(IConfiguration config)
    {
        _config = config;
    }

    public async Task SendVendorInvitation(string toEmail, string vendorName, string inviteToken)
    {
        var frontendUrl = _config["FrontendUrl"] ?? "http://localhost:8100";
        var link = $"{frontendUrl}/setup-account?token={inviteToken}";

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(
            _config["Email:SenderName"] ?? "Pijaca u Džepu",
            _config["Email:SenderEmail"] ?? "noreply@pijacaudzepu.rs"));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = "Poziv za registraciju - Pijaca u Džepu";

        message.Body = new TextPart("html")
        {
            Text = $@"
<div style=""font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;"">
    <div style=""background-color: #2e7d32; color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0;"">
        <h1 style=""margin: 0;"">🌿 Pijaca u Džepu</h1>
    </div>
    <div style=""background-color: #f5f5f5; padding: 30px; border-radius: 0 0 8px 8px;"">
        <h2>Zdravo!</h2>
        <p>Pozvani ste da upravljate prodavnicom <strong>{vendorName}</strong> na platformi Pijaca u Džepu.</p>
        <p>Kliknite na dugme ispod da kreirate svoj nalog:</p>
        <div style=""text-align: center; margin: 30px 0;"">
            <a href=""{link}"" style=""background-color: #2e7d32; color: white; padding: 14px 28px; text-decoration: none; border-radius: 6px; font-size: 16px; font-weight: bold;"">
                Kreiraj nalog
            </a>
        </div>
        <p style=""color: #666; font-size: 14px;"">Link važi 7 dana. Ako niste očekivali ovaj email, slobodno ga ignorišite.</p>
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
}
