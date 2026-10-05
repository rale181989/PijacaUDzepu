using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PijacaUDzepu.API.Models.Errors;

namespace PijacaUDzepu.API.Controllers.Base;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public abstract class BaseApiController : ControllerBase
{
    protected ActionResult GenerateErrorResponse(int statusCode, string title, List<string>? errors = null)
    {
        var response = new ErrorResponse
        {
            Status = statusCode,
            Title = title,
            Errors = errors ?? new List<string>()
        };

        return StatusCode(statusCode, response);
    }
}
