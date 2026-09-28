using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Users.Application.Users.Admin.Queries.GetUserDashboard;

namespace Users.Presentation.Controllers;

[ApiController]
[Route("api/admin/dashboard")]
[Authorize(Policy = "AdminOnly")]
public sealed class AdminDashboardController : ControllerBase
{
    [HttpGet("stats")]
    public async Task<ActionResult<GetUserDashboardResult>> GetStats(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromServices] GetUserDashboardHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new GetUserDashboardQuery(from, to),
            cancellationToken);
        return Ok(result);
    }
}
