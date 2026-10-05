namespace PijacaUDzepu.API.Services.Interfaces;

public interface IEmailService
{
    Task SendVendorInvitation(string toEmail, string vendorName, string inviteToken);
}
