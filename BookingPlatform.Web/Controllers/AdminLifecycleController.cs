using BookingPlatform.Infrastructure.Bookings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookingPlatform.Web.Controllers;

[ApiController]
[Route("api/admin/lifecycle")]
[Authorize(Policy = "AdminOnly")]
public class AdminLifecycleController : ControllerBase
{
    private readonly IBookingLifecycleProcessor _processor;

    public AdminLifecycleController(IBookingLifecycleProcessor processor)
    {
        _processor = processor;
    }

    [HttpPost("run")]
    [ProducesResponseType(typeof(BookingLifecycleProcessResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<BookingLifecycleProcessResult>> Run(CancellationToken cancellationToken)
    {
        var result = await _processor.ProcessAsync(cancellationToken);
        return Ok(result);
    }
}
