using PijacaUDzepu.API.Models.DTO.Input;
using PijacaUDzepu.API.Models.DTO.Output;

namespace PijacaUDzepu.API.Services.Interfaces;

public interface IVendorService
{
    Task<List<VendorDto>> GetAllVendors(bool includeInactive = false);
    Task<VendorDto> GetVendorById(int id);
    Task<VendorDto> CreateVendor(VendorInputDto dto);
    Task<VendorDto> UpdateVendor(int id, VendorInputDto dto);
    Task ToggleVendorActive(int id);
    Task<int?> GetVendorIdForUser(int userId);
    Task<bool> UserBelongsToVendor(int userId, int vendorId);
    Task<string> UpdateVendorImage(int vendorId, IFormFile file);
}
