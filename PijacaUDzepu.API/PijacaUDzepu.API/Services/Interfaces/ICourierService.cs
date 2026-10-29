using PijacaUDzepu.API.Models.DTO.Input;
using PijacaUDzepu.API.Models.DTO.Output;

namespace PijacaUDzepu.API.Services.Interfaces;

public interface ICourierService
{
    Task<CourierProfileDto> GetProfile(int userId);
    Task<CourierProfileDto> UpdateProfile(int userId, CourierProfileInputDto dto);
    Task<CourierProfileDto> SetShift(int userId, bool isOnShift);
}
