using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Users.Application.Abstractions;
using Users.Application.Users.Commands.SetUserRole;
using Users.Domain.Entities;

namespace Users.Presentation.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = "Admin")]
public sealed class AdminUsersController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;

    public AdminUsersController(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? query = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var (items, totalCount) = await _userRepository.GetPagedUsersAsync(
            query, page, pageSize, cancellationToken);

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var result = new
        {
            items = items.Select(u => new
            {
                u.Id,
                u.Username,
                u.Email,
                role = u.Role.ToString(),
                roleNumber = (int)u.Role,
                u.CreatedAt
            }),
            totalCount,
            page,
            pageSize,
            totalPages
        };

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(id);
        if (user is null)
            return NotFound(new { message = "User not found." });

        return Ok(new
        {
            user.Id,
            user.Username,
            user.Email,
            role = user.Role.ToString(),
            roleNumber = (int)user.Role,
            user.CreatedAt
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateAdminUserRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(new { message = "Email is required." });

        if (string.IsNullOrWhiteSpace(request.Username))
            return BadRequest(new { message = "Username is required." });

        if (string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { message = "Password is required." });

        var existing = await _userRepository.GetByEmailAsync(request.Email.Trim().ToLowerInvariant());
        if (existing is not null)
            return BadRequest(new { message = "Email already exists." });

        var role = request.Role == 1 ? UserRole.Admin : UserRole.User;
        var hash = _passwordHasher.Hash(request.Password.Trim());

        var newUser = new User(
            Guid.NewGuid(),
            request.Username.Trim(),
            request.Email.Trim().ToLowerInvariant(),
            hash,
            role
        );

        await _userRepository.AddAsync(newUser);

        return CreatedAtAction(nameof(GetById), new { id = newUser.Id }, new
        {
            newUser.Id,
            newUser.Username,
            newUser.Email,
            role = newUser.Role.ToString(),
            roleNumber = (int)newUser.Role,
            newUser.CreatedAt
        });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateAdminUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(id);
        if (user is null)
            return NotFound(new { message = "User not found." });

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var emailClean = request.Email.Trim().ToLowerInvariant();
            var existing = await _userRepository.GetByEmailAsync(emailClean);
            if (existing is not null && existing.Id != id)
                return BadRequest(new { message = "Email is already taken by another user." });
        }

        var username = !string.IsNullOrWhiteSpace(request.Username) ? request.Username.Trim() : user.Username;
        var email = !string.IsNullOrWhiteSpace(request.Email) ? request.Email.Trim().ToLowerInvariant() : user.Email;
        var role = request.Role.HasValue ? (request.Role.Value == 1 ? UserRole.Admin : UserRole.User) : user.Role;
        var newHash = !string.IsNullOrWhiteSpace(request.Password) ? _passwordHasher.Hash(request.Password.Trim()) : null;

        user.UpdateAdmin(username, email, role, newHash);
        await _userRepository.UpdateAsync(user);

        return Ok(new
        {
            user.Id,
            user.Username,
            user.Email,
            role = user.Role.ToString(),
            roleNumber = (int)user.Role,
            user.CreatedAt
        });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(currentUserId, out var parsedCurrent) && parsedCurrent == id)
        {
            return BadRequest(new { message = "Cannot delete the currently logged in admin user." });
        }

        var user = await _userRepository.GetByIdAsync(id);
        if (user is null)
            return NotFound(new { message = "User not found." });

        await _userRepository.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("delete-batch")]
    public async Task<IActionResult> DeleteBatch(
        [FromBody] DeleteBatchUsersRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Ids is null || request.Ids.Count == 0)
            return BadRequest(new { message = "No user IDs provided." });

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        Guid? parsedCurrent = Guid.TryParse(currentUserId, out var p) ? p : null;

        var targetIds = request.Ids
            .Where(id => parsedCurrent == null || id != parsedCurrent.Value)
            .ToList();

        if (targetIds.Count == 0)
            return BadRequest(new { message = "Cannot delete yourself." });

        await _userRepository.DeleteRangeAsync(targetIds, cancellationToken);

        return Ok(new { deletedCount = targetIds.Count });
    }

    [HttpPost("{id:guid}/role")]
    public async Task<IActionResult> SetRole(
        Guid id,
        [FromBody] SetUserRoleBody body2,
        [FromServices] SetUserRoleHandler handler)
    {
        var result = await handler.HandleAsync(
            new SetUserRoleCommand(id, body2.Role));
        return Ok(result);
    }
}

public sealed record SetUserRoleBody(int Role);

public sealed record CreateAdminUserRequest(
    string Username,
    string Email,
    string Password,
    int Role = 0
);

public sealed record UpdateAdminUserRequest(
    string? Username,
    string? Email,
    int? Role,
    string? Password
);

public sealed record DeleteBatchUsersRequest(
    IReadOnlyList<Guid> Ids
);