using PijacaUDzepu.API.Models;

namespace PijacaUDzepu.API.Services.Interfaces;

public interface IEmailService
{
    Task SendVendorInvitation(string toEmail, string vendorName, string inviteToken);
    Task SendTrackingLink(string toEmail, string guestName, string trackingCode, IReadOnlyList<Order> orders);
    Task SendSupportRequest(SupportRequest request);
}
