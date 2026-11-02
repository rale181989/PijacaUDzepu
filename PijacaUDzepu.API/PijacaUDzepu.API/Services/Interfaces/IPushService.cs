namespace PijacaUDzepu.API.Services.Interfaces;

public interface IPushService
{
    Task RegisterDevice(int userId, string token, string? platform);
    Task UnregisterDevice(string token);
    Task SendToUsers(IEnumerable<int> userIds, string title, string body, string? url = null);

    // Obaveštenje koje zvoni i vibrira u krug dok ga korisnik ne otvori ili dok se ne pošalje CancelAlarm
    // sa istim tag-om (npr. kada drugi kurir prihvati porudžbinu). Prikazuje ga nativni kod Android aplikacije.
    Task SendAlarm(IEnumerable<int> userIds, string tag, string title, string body, string url);
    Task CancelAlarm(IEnumerable<int> userIds, string tag);
}
