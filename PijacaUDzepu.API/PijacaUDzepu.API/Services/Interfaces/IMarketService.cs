using PijacaUDzepu.API.Models.DTO.Input;
using PijacaUDzepu.API.Models.DTO.Output;

namespace PijacaUDzepu.API.Services.Interfaces;

public interface IMarketService
{
    Task<List<MarketDto>> GetAll();
    Task<List<MarketDto>> GetAllIncludingInactive();
    Task<MarketDto> GetById(int id);
    Task<MarketDto> Create(MarketInputDto dto);
    Task<MarketDto> Update(int id, MarketInputDto dto);
    Task ToggleActive(int id);
}
