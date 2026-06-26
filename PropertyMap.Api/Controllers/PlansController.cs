using Microsoft.AspNetCore.Mvc;
using PropertyMap.Core.Interfaces;

namespace PropertyMap.Api.Controllers;

[ApiController]
[Route("api/plans")]
public class PlansController : ControllerBase
{
    private readonly IPlanRepository _plans;

    public PlansController(IPlanRepository plans)
    {
        _plans = plans;
    }

    [HttpGet]
    public async Task<IActionResult> GetActive() =>
        Ok(await _plans.GetActiveAsync());
}
