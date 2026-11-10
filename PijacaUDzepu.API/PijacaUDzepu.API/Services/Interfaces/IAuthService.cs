using PijacaUDzepu.API.Models.DTO.Input;
using PijacaUDzepu.API.Models.DTO.Output;

namespace PijacaUDzepu.API.Services.Interfaces;

public interface IAuthService
{
    Task<AuthResponseDto> Login(LoginDto dto, string clientIp);
    Task<AuthResponseDto> Register(RegisterDto dto);
    Task<UserDto> CreateVendorAdmin(CreateVendorAdminDto dto);
    Task<UserDto> CreateCourier(CreateCourierDto dto);
    Task<UserDto> GetCurrentUser(int userId);
    Task InviteVendor(InviteVendorDto dto);
    Task<AuthResponseDto> AcceptInvitation(AcceptInvitationDto dto);
    Task<UserDto> UpdateProfile(int userId, UpdateProfileDto dto);
    Task ChangePassword(int userId, string currentPassword, string newPassword);
    Task RequestPasswordReset(string login);
    Task ResetPassword(string token, string newPassword);
    Task DeleteAccount(int userId, string password);
    Task<string> AdminResetPassword(int userId);
}
