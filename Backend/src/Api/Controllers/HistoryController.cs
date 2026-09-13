using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MyTarotReader.Api.Helpers;
using MyTarotReader.Application.Common.Models;
using MyTarotReader.Application.Contracts.Services;

namespace MyTarotReader.Api.Controllers;

[Route("api/histories")]
[ApiController]
[ProducesErrorResponseType(typeof(ApiResponse<object>))]
public class HistoryController(IHistoryService service) : ControllerBase
{
    private readonly IHistoryService _service = service;

    /// <summary>
    /// Retrieves the history of tarot readings for the authenticated user.
    /// </summary>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<List<GetHistoryResult>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetHistoryAsync(CancellationToken cancellationToken)
    {
        var userId = JwtHelper.GetUserId(HttpContext);

        var history = await _service.GetHistoryAsync(userId, cancellationToken);

        return Ok(ApiResponse.Success(history));
    }

    /// <summary>
    /// Deletes a specific history entry for the authenticated user.
    /// </summary>
    [HttpDelete("{historyId:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteHistoryAsync(
        Guid historyId,
        CancellationToken cancellationToken
    )
    {
        var userId = JwtHelper.GetUserId(HttpContext);

        await _service.DeleteHistoryAsync(userId, historyId, cancellationToken);

        return Ok(ApiResponse.Success());
    }
}
