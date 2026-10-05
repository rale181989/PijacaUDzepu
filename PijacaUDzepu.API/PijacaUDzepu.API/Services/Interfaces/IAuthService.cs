using PijacaUDzepu.API.Models.DTO.Input;
using PijacaUDzepu.API.Models.DTO.Output;

namespace PijacaUDzepu.API.Services.Interfaces;

public interface IAuthService
{
    Task<AuthResponseDto> Login(LoginDto dto);
    Task<AuthResponseDto> Register(RegisterDto dto);
    Task<UserDto> CreateVendorAdmin(CreateVendorAdminDto dto);
    Task<UserDto> GetCurrentUser(int userId);
    Task InviteVendor(InviteVendorDto dto);
    Task<AuthResponseDto> AcceptInvitation(AcceptInvitationDto dto);
    Task ChangePassword(int userId, string currentPassword, string newPassword);
}
