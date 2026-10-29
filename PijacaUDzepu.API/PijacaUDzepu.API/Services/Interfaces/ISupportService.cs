using PijacaUDzepu.API.Models.DTO.Input;
using PijacaUDzepu.API.Models.DTO.Output;

namespace PijacaUDzepu.API.Services.Interfaces;

public interface ISupportService
{
    Task Create(SupportRequestInputDto dto, int? userId);
    Task<List<SupportRequestDto>> GetAll(bool? resolved);
    Task<int> GetOpenCount();
    Task<SupportRequestDto> SetResolved(int id, bool resolved);
}
