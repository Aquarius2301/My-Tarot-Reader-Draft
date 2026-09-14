using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyTarotReader.Api.Helpers;
using MyTarotReader.Application.Common.Models;
using MyTarotReader.Application.Contracts.Services;

namespace MyTarotReader.Api.Controllers;

[Route("api/tarot-reading")]
[ApiController]
[ProducesErrorResponseType(typeof(ApiResponse<object>))]
public class TarotReadingController(ITarotReadingService service) : ControllerBase
{
    private readonly ITarotReadingService _service = service;

    /// <summary>
    /// Retrieves the last drawn tarot card for an authenticated user.
    /// </summary>
    [HttpGet("draw")]
    [Authorize]
    [ProducesResponseType(
        typeof(ApiResponse<GetLastDrawnCardForAuthResult>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetLastDrawnCardForAuthAsync(
        CancellationToken cancellationToken
    )
    {
        var userId = JwtHelper.GetUserId(HttpContext);

        var result = await _service.GetLastDrawnCardForAuthAsync(userId, cancellationToken);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>
    /// Creates a new tarot card draw for an authenticated user.
    /// </summary>
    [HttpPost("draw")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateDrawForAuthAsync(
        [FromBody] CreateDrawForAuthRequest request,
        CancellationToken cancellationToken
    )
    {
        var userId = JwtHelper.GetUserId(HttpContext);
        await _service.CreateDrawForAuthAsync(request, userId, cancellationToken);
        return Ok(ApiResponse.Success());
    }

    /// <summary>
    /// Retrieves the last drawn tarot card for a guest user.
    /// </summary>
    /// <remarks> The card is saved in Redis with a 12-hour cooldown. </remarks>
    [HttpGet("guest-draw")]
    [ProducesResponseType(
        typeof(ApiResponse<GetLastDrawnCardForGuestResult>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetLastDrawnCardForGuestAsync(
        [FromQuery] string guestKey,
        CancellationToken cancellationToken
    )
    {
        var availability = await _service.GetLastDrawnCardForGuestAsync(
            guestKey,
            cancellationToken
        );
        return Ok(ApiResponse.Success(availability));
    }

    /// <summary>
    /// Creates a new tarot card draw for a guest user.
    /// </summary>
    /// <remarks> The card is saved in Redis with a 12-hour cooldown. </remarks>
    [HttpPost("guest-draw")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> CreateDrawForGuestAsync(
        [FromBody] CreateDrawForGuestRequest request,
        CancellationToken cancellationToken
    )
    {
        CookieHelper.Append(
            Response,
            CookieHelper.GuestCookieName,
            request.GuestKey,
            DateTimeOffset.UtcNow.AddDays(1)
        ); // Save the guest key in a cookie for easy to remove key on swagger (testing)

        await _service.CreateDrawForGuestAsync(request, cancellationToken);

        return Ok(ApiResponse.Success());
    }

    /// <summary>
    /// Clears the last drawn tarot card for a guest user.
    /// </summary>
    /// <remarks> This endpoint is for testing purposes only. </remarks>
    [HttpDelete("guest-draw")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ClearGuestDrawAsync(
        [FromQuery] string guestKey,
        CancellationToken cancellationToken
    )
    {
        await _service.RemoveDrawForGuestAsync(guestKey, cancellationToken);
        return Ok(ApiResponse.Success());
    }
}
