using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Places.Application.SavedPlaces.Commands.SavePlace;
using Places.Application.SavedPlaces.Commands.UnsavePlace;
using Places.Application.SavedPlaces.Queries.GetSavedPlacesByUser;

namespace Places.Presentation.Controllers;

[ApiController]
[Route("api/saved-places")]
[Authorize]
public sealed class SavedPlacesController : ControllerBase
{
    private readonly SavePlaceHandler _savePlaceHandler;
    private readonly UnsavePlaceHandler _unsavePlaceHandler;
    private readonly GetSavedPlacesByUserHandler _getSavedPlacesHandler;

    public SavedPlacesController(
        SavePlaceHandler savePlaceHandler,
        UnsavePlaceHandler unsavePlaceHandler,
        GetSavedPlacesByUserHandler getSavedPlacesHandler
    )
    {
        _savePlaceHandler = savePlaceHandler;
        _unsavePlaceHandler = unsavePlaceHandler;
        _getSavedPlacesHandler = getSavedPlacesHandler;
    }

    [HttpGet]
    public async Task<IActionResult> GetMine(
        CancellationToken cancellationToken
    )
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var query = new GetSavedPlacesByUserQuery(userId);
        var result = await _getSavedPlacesHandler.HandleAsync(
            query,
            cancellationToken
        );
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Save(
        SavePlaceRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var result = await _savePlaceHandler.HandleAsync(
            new SavePlaceCommand(
                userId,
                request.PlaceId,
                request.Note
            ),
            cancellationToken
        );
        return Ok(result);
    }

    [HttpDelete("{placeId:guid}")]
    public async Task<IActionResult> Unsave(
        Guid placeId,
        CancellationToken cancellationToken
    )
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var result = await _unsavePlaceHandler.HandleAsync(
            new UnsavePlaceCommand(userId, placeId),
            cancellationToken
        );
        return Ok(result);
    }

    private bool TryGetUserId(out Guid userId)
    {
        userId = Guid.Empty;
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out userId);
    }
}

public sealed record SavePlaceRequest(
    Guid PlaceId,
    string? Note
);
