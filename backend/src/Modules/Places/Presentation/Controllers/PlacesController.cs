using Configuration.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Places.Application.Abstractions;
using Places.Application.Contracts;
using Places.Application.Places.Queries.GetPlaceById;
using Places.Application.Places.Queries.SearchPlaces;
using Places.Domain.Entities;

namespace Places.Presentation.Controllers;

[ApiController]
[Route("api/places")]
public sealed class PlacesController : ControllerBase
{
    private readonly SearchPlacesHandler _searchPlacesHandler;
    private readonly GetPlaceByIdHandler _getPlacesByIdHandler;
    private readonly IPlaceRepository _placeRepository;
    private readonly ITuningProvider _tuning;

    public PlacesController(
        SearchPlacesHandler searchPlacesHandler,
        GetPlaceByIdHandler getPlacesByIdHandler,
        IPlaceRepository placeRepository,
        ITuningProvider tuning)
    {
        _searchPlacesHandler = searchPlacesHandler;
        _getPlacesByIdHandler = getPlacesByIdHandler;
        _placeRepository = placeRepository;
        _tuning = tuning;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] string? category,
        [FromQuery] string? source,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default
    )
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var (items, totalCount) = await _placeRepository.GetPagedAsync(
            search,
            category,
            source,
            page,
            pageSize,
            cancellationToken
        );

        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        var responses = items.Select(PlaceResponse.FromEntity).ToList();

        return Ok(new PagedResult<PlaceResponse>(
            responses,
            totalCount,
            page,
            pageSize,
            totalPages
        ));
    }

    [HttpGet("sources")]
    [AllowAnonymous]
    public async Task<IActionResult> GetSources(CancellationToken cancellationToken)
    {
        var sources = await _placeRepository.GetDistinctSourcesAsync(cancellationToken);
        return Ok(sources);
    }

    [HttpGet("categories")]
    [AllowAnonymous]
    public async Task<IActionResult> GetCategories(CancellationToken cancellationToken)
    {
        var categories = await _placeRepository.GetDistinctCategoriesAsync(cancellationToken);
        return Ok(categories);
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string query,
        [FromQuery] double latitude,
        [FromQuery] double longitude,
        [FromQuery] double radiusKm,
        CancellationToken cancellationToken
    )
    {
        var searchOptions = await _tuning.GetSearchOptionsAsync(cancellationToken);

        var searchQuery = new SearchPlacesQuery(
            query,
            latitude,
            longitude,
            radiusKm,
            searchOptions.CandidateLimit
        );
        var places = await _searchPlacesHandler.HandleAsync(
            searchQuery,
            cancellationToken
        );
        return Ok(places);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken
    )
    {
        var query = new GetPlaceByIdQuery(id);
        var place = await _getPlacesByIdHandler.HandleAsync(
            query,
            cancellationToken
        );
        return place is null ? NotFound() : Ok(PlaceResponse.FromEntity(place));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(
        [FromBody] CreatePlaceCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
            return BadRequest("Name is required.");

        if (command.Latitude < -90 || command.Latitude > 90 ||
            command.Longitude < -180 || command.Longitude > 180)
            return BadRequest("Latitude must be between -90 and 90, Longitude between -180 and 180.");

        var externalId = !string.IsNullOrWhiteSpace(command.ExternalId)
            ? command.ExternalId.Trim()
            : $"manual-{Guid.NewGuid():N}";

        var source = !string.IsNullOrWhiteSpace(command.Source)
            ? command.Source.Trim()
            : "Manual";

        var place = new Place(
            externalId,
            command.Name.Trim(),
            command.Latitude,
            command.Longitude,
            source,
            command.Address?.Trim(),
            command.Category?.Trim(),
            command.OpeningHours?.Trim()
        );

        await _placeRepository.AddAsync(place, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = place.Id }, PlaceResponse.FromEntity(place));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdatePlaceCommand command,
        CancellationToken cancellationToken)
    {
        if (id != command.Id)
            return BadRequest("Id mismatch.");

        if (string.IsNullOrWhiteSpace(command.Name))
            return BadRequest("Name is required.");

        if (command.Latitude < -90 || command.Latitude > 90 ||
            command.Longitude < -180 || command.Longitude > 180)
            return BadRequest("Latitude must be between -90 and 90, Longitude between -180 and 180.");

        var place = await _placeRepository.GetByIdAsync(id, cancellationToken);
        if (place is null)
            return NotFound();

        place.UpdateDetails(
            command.Name.Trim(),
            command.Address?.Trim(),
            command.Category?.Trim(),
            command.OpeningHours?.Trim(),
            command.Latitude,
            command.Longitude,
            command.Source?.Trim()
        );

        await _placeRepository.UpdateAsync(place, cancellationToken);
        return Ok(PlaceResponse.FromEntity(place));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var place = await _placeRepository.GetByIdAsync(id, cancellationToken);
        if (place is null)
            return NotFound();

        await _placeRepository.DeleteAsync(place, cancellationToken);
        return NoContent();
    }
}
