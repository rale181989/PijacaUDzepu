using PijacaUDzepu.API.Models;

namespace PijacaUDzepu.API.Services.Interfaces;

public interface ITokenService
{
    Task<string> CreateToken(User user);
}
