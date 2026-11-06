namespace PijacaUDzepu.API.Models.Enums;

public enum OrderStatus
{
    Pending,
    Confirmed,
    Rejected,
    ReadyForPickup,
    Completed,
    Cancelled,
    // Kurir je preuzeo robu i nosi je kupcu (dodato na kraj da ne menja postojeće vrednosti u bazi)
    InDelivery,
    // Kurir nije uspeo da dostavi (razlog je obavezan); roba se vraća prodavcu
    NotDelivered
}
