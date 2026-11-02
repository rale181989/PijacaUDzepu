using PijacaUDzepu.API.Models.DTO.Output;

namespace PijacaUDzepu.API.Services.Interfaces;

public interface IReportService
{
    // Lokalni datum prve porudžbine (za period "Sve"); danas ako porudžbina još nema
    Task<DateOnly> GetFirstOrderDate();
    Task<OrdersReportDto> GetOrdersReport(DateOnly from, DateOnly to);
    Task<List<CustomerReportDto>> GetCustomersReport(DateOnly from, DateOnly to);
    Task<List<CourierReportDto>> GetCouriersReport(DateOnly from, DateOnly to);
}
