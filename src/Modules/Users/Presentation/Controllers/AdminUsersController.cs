using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Users.Application.Users.Admin.Commands.CreateUser;
using Users.Application.Users.Admin.Commands.DeleteUser;
using Users.Application.Users.Admin.Commands.ResetUserPassword;
using Users.Application.Users.Admin.Commands.UpdateUser;
using Users.Application.Users.Admin.Models;
using Users.Application.Users.Admin.Queries.GetAdminUserById;
using Users.Application.Users.Admin.Queries.ListUsers;
using Users.Application.Users.Validation;
using Users.Presentation.Contracts;

namespace Users.Presentation.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Policy = "AdminOnly")]
public sealed class AdminUsersController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ListUsersResult>> List(
        [FromQuery] string? keyword,
        [FromQuery] string? role,
        [FromQuery] string? excludeRole,
        [FromQuery] string? status,
        [FromQuery] string? gender,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? sortBy,
        [FromQuery] bool? sortDescending,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromServices] ListUsersHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new ListUsersQuery
            {
                Keyword = keyword,
                Role = string.IsNullOrWhiteSpace(role)
                    ? null
                    : UserInputRules.ParseRole(role),
                ExcludeRole = string.IsNullOrWhiteSpace(excludeRole)
                    ? null
                    : UserInputRules.ParseRole(excludeRole),
                Status = string.IsNullOrWhiteSpace(status)
                    ? null
                    : UserInputRules.ParseStatus(status),
                Gender = string.IsNullOrWhiteSpace(gender)
                    ? null
                    : UserInputRules.ParseGender(gender),
                From = from,
                To = to,
                SortBy = string.IsNullOrWhiteSpace(sortBy)
                    ? "createdAt"
                    : sortBy,
                SortDescending = sortDescending ?? true,
                Page = page is > 0 ? page.Value : 1,
                PageSize = pageSize is > 0 ? pageSize.Value : 20,
            },
            cancellationToken);
        return Ok(ListUsersResult.From(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AdminUserResult>> GetById(
        Guid id,
        [FromServices] GetAdminUserByIdHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new GetAdminUserByIdQuery(id),
            cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<AdminUserResult>> Create(
        [FromBody] CreateUserRequest request,
        [FromServices] CreateUserHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new CreateUserCommand(
                request.Username,
                request.Email,
                request.FullName,
                request.Phone,
                request.Gender,
                request.Role,
                request.Status,
                request.Password,
                request.Notify),
            cancellationToken);
        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Id },
            result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AdminUserResult>> Update(
        Guid id,
        [FromBody] UpdateUserRequest request,
        [FromServices] UpdateUserHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            id,
            new UpdateUserCommand(
                request.Username,
                request.Email,
                request.FullName,
                request.Phone,
                request.Gender,
                request.Role,
                request.Status,
                request.Notify),
            User,
            cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        [FromServices] DeleteUserHandler handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(id, User, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/password")]
    public async Task<IActionResult> ResetPassword(
        Guid id,
        [FromBody] ResetUserPasswordRequest request,
        [FromServices] ResetUserPasswordHandler handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(
            id,
            new ResetUserPasswordCommand(
                request.Password,
                request.Notify),
            cancellationToken);
        return NoContent();
    }
}
