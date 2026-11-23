using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PijacaUDzepu.API.Controllers.Base;
using PijacaUDzepu.API.Helpers;

namespace PijacaUDzepu.API.Controllers;

// Koje su funkcije uključene (Helpers/Features); klijent ih učita jednom pri pokretanju
public class FeaturesController : BaseApiController
{
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Get() => Ok(new { stallLocation = Features.StallLocation });
}
