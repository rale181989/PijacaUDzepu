using PijacaUDzepu.API.Models.DTO.Input;
using PijacaUDzepu.API.Models.DTO.Output;

namespace PijacaUDzepu.API.Services.Interfaces;

public interface ISupportService
{
    Task Create(SupportRequestInputDto dto, int? userId);
    Task<PagedResult<SupportRequestDto>> GetAll(bool? resolved, int skip = 0, int take = 20);
    Task<int> GetOpenCount();
    Task<SupportRequestDto> SetResolved(int id, bool resolved);
}
