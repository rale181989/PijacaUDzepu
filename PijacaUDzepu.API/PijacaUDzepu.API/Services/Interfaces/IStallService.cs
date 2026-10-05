using PijacaUDzepu.API.Models.DTO.Input;
using PijacaUDzepu.API.Models.DTO.Output;

namespace PijacaUDzepu.API.Services.Interfaces;

public interface IStallService
{
    Task<List<StallDto>> GetByMarket(int marketId);
    Task<List<StallDto>> GetAll();
    Task<StallDto> Create(StallInputDto dto);
    Task<StallDto> Update(int id, StallInputDto dto);
    Task Delete(int id);
}
