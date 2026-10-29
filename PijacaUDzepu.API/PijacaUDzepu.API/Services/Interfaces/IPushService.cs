namespace PijacaUDzepu.API.Services.Interfaces;

public interface IPushService
{
    Task RegisterDevice(int userId, string token, string? platform);
    Task UnregisterDevice(string token);
    Task SendToUsers(IEnumerable<int> userIds, string title, string body, string? url = null);
}
