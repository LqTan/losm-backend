using System.Security.Claims;
using System.Text.RegularExpressions;
using Common.Email;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Users.Domain.Entities;
using Users.Infrastructure.Persistence;

namespace Users.Presentation.Controllers;

[ApiController]
[Route("api/friends")]
public sealed class FriendsController : ControllerBase
{
    private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);
    private readonly IEmailTemplateRenderer _templates;
    private readonly UsersDbContext _dbContext;
    private readonly IEmailSender _emailSender;
    private readonly IConfiguration _config;
    private readonly ILogger<FriendsController> _logger;

    public FriendsController(
        UsersDbContext dbContext,
        IEmailSender emailSender,
        IConfiguration config,
        ILogger<FriendsController> logger,
        IEmailTemplateRenderer templates)
    {
        _templates = templates;
        _dbContext = dbContext;
        _emailSender = emailSender;
        _config = config;
        _logger = logger;
    }

    [Authorize]
    [HttpPost("invite")]
    public async Task<IActionResult> InviteFriend(
        [FromBody] InviteFriendDto dto,
        CancellationToken cancellationToken)
    {
        var targetEmail = dto.TargetEmail.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(targetEmail) || !EmailRegex.IsMatch(targetEmail))
        {
            return BadRequest(new { message = "Please enter a valid email address." });
        }
        if (!TryGetUserId(out var senderId))
        {
            return Unauthorized();
        }

        var sender = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == senderId, cancellationToken);
        if (sender is null)
        {
            return Unauthorized();
        }

        if (string.Equals(sender.Email, targetEmail, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "You cannot send a friend request to yourself." });
        }

        var existingFriendship = await _dbContext.Friendships
            .FirstOrDefaultAsync(f =>
                (f.SenderId == senderId && f.ReceiverEmail == targetEmail) ||
                (f.ReceiverId == senderId && f.Sender.Email == targetEmail),
                cancellationToken);

        if (existingFriendship is not null)
        {
            if (existingFriendship.Status == FriendshipStatus.Accepted)
            {
                return BadRequest(new { message = "You are already friends." });
            }

            if (existingFriendship.Status == FriendshipStatus.Pending)
            {
                return BadRequest(new { message = "A friend request has already been sent and is awaiting a response." });
            }
        }

        var receiver = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == targetEmail, cancellationToken);
        var token = Guid.NewGuid().ToString("N");

        var friendship = new Friendship(
            Guid.NewGuid(),
            senderId,
            targetEmail,
            receiver?.Id,
            token
        );

        _dbContext.Friendships.Add(friendship);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Gửi email lời mời kết bạn
        var frontendUrl = ResolveFrontendUrl();
        var inviteUrl = $"{frontendUrl}/friends/invite?token={token}&email={Uri.EscapeDataString(targetEmail)}";

        var values = new Dictionary<string, string?>
        {
            ["senderName"] = sender.Username, ["senderEmail"] = sender.Email, ["inviteUrl"] = inviteUrl
        };
        var html = await _templates.RenderAsync("email.friend.html", values, cancellationToken);
        var plainText = await _templates.RenderAsync("email.friend.text", values, cancellationToken);
        var subject = await _templates.RenderAsync("email.friend.subject", values, cancellationToken);

        await _emailSender.SendAsync(new EmailRequest(
            ToAddress: targetEmail,
            ToName: receiver?.Username ?? targetEmail,
            Subject: subject,
            HtmlBody: html,
            PlainTextBody: plainText
        ), cancellationToken);

        return Ok(new
        {
            success = true,
            message = $"Friend request sent to {targetEmail}.",
            token
        });
    }

    [AllowAnonymous]
    [HttpGet("invite-info")]
    public async Task<IActionResult> GetInviteInfo(
        [FromQuery] string token,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return BadRequest(new { message = "Invalid token." });
        }

        var friendship = await _dbContext.Friendships
            .Include(f => f.Sender)
            .FirstOrDefaultAsync(f => f.Token == token.Trim(), cancellationToken);

        if (friendship is null)
        {
            return NotFound(new { message = "Friend request not found or expired." });
        }

        var userExists = await _dbContext.Users.AnyAsync(u => u.Email == friendship.ReceiverEmail, cancellationToken);

        return Ok(new
        {
            id = friendship.Id,
            senderId = friendship.SenderId,
            senderName = friendship.Sender.Username,
            senderUsername = friendship.Sender.Username,
            senderEmail = friendship.Sender.Email,
            senderAvatarUrl = friendship.Sender.AvatarUrl,
            receiverEmail = friendship.ReceiverEmail,
            status = friendship.Status.ToString(),
            userExists,
            createdAt = friendship.CreatedAt
        });
    }

    [Authorize]
    [HttpPost("respond")]
    public async Task<IActionResult> RespondToInvite(
        [FromBody] RespondFriendDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var currentUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == currentUserId, cancellationToken);
        if (currentUser is null)
        {
            return Unauthorized();
        }

        Friendship? friendship = null;
        if (!string.IsNullOrWhiteSpace(dto.Token))
        {
            friendship = await _dbContext.Friendships
                .Include(f => f.Sender)
                .FirstOrDefaultAsync(f => f.Token == dto.Token.Trim(), cancellationToken);
        }
        else if (dto.FriendshipId.HasValue)
        {
            friendship = await _dbContext.Friendships
                .Include(f => f.Sender)
                .FirstOrDefaultAsync(f => f.Id == dto.FriendshipId.Value, cancellationToken);
        }

        if (friendship is null)
        {
            return NotFound(new { message = "Friend request not found." });
        }

        var isRecipient = friendship.ReceiverId == currentUserId ||
                          string.Equals(friendship.ReceiverEmail, currentUser.Email, StringComparison.OrdinalIgnoreCase);

        if (!isRecipient)
        {
            return Forbid();
        }

        if (dto.IsAccepted)
        {
            friendship.Accept(currentUserId);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Ok(new
            {
                success = true,
                status = "Accepted",
                message = $"You and {friendship.Sender.Username} are now friends.",
                friend = new
                {
                    id = friendship.SenderId,
                    username = friendship.Sender.Username,
                    email = friendship.Sender.Email
                }
            });
        }
        else
        {
            friendship.Decline(currentUserId);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Ok(new
            {
                success = true,
                status = "Declined",
                message = "Friend request declined."
            });
        }
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> GetFriends(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var friendships = await _dbContext.Friendships
            .Include(f => f.Sender)
            .Include(f => f.Receiver)
            .Where(f => f.Status == FriendshipStatus.Accepted &&
                        (f.SenderId == currentUserId || f.ReceiverId == currentUserId))
            .OrderByDescending(f => f.UpdatedAt ?? f.CreatedAt)
            .ToListAsync(cancellationToken);

        var friends = friendships.Select(f =>
        {
            var isSender = f.SenderId == currentUserId;
            var friendUser = isSender ? f.Receiver : f.Sender;
            var friendId = friendUser?.Id ?? (isSender ? f.ReceiverId ?? Guid.Empty : f.SenderId);
            var username = friendUser?.Username ?? (isSender ? f.ReceiverEmail.Split('@')[0] : f.Sender.Username);
            var email = friendUser?.Email ?? (isSender ? f.ReceiverEmail : f.Sender.Email);
            var avatarUrl = friendUser?.AvatarUrl;

            return new
            {
                id = f.Id,
                friendId,
                username,
                email,
                avatarUrl,
                createdAt = f.UpdatedAt ?? f.CreatedAt,
                since = f.UpdatedAt ?? f.CreatedAt,
                friend = new
                {
                    id = friendId,
                    username,
                    email,
                    avatarUrl
                }
            };
        }).Where(f => f.friendId != Guid.Empty).ToList();

        return Ok(friends);
    }

    [Authorize]
    [HttpGet("requests")]
    public async Task<IActionResult> GetRequests(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var currentUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == currentUserId, cancellationToken);
        if (currentUser is null)
        {
            return Unauthorized();
        }

        var userEmail = currentUser.Email?.ToLowerInvariant() ?? string.Empty;

        var incoming = await _dbContext.Friendships
            .Include(f => f.Sender)
            .Where(f => f.Status == FriendshipStatus.Pending &&
                        (f.ReceiverId == currentUserId || f.ReceiverEmail == userEmail))
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => new
            {
                id = f.Id,
                token = f.Token,
                senderId = f.SenderId,
                senderName = f.Sender.Username,
                senderUsername = f.Sender.Username,
                senderEmail = f.Sender.Email,
                senderAvatarUrl = f.Sender.AvatarUrl,
                receiverEmail = f.ReceiverEmail,
                createdAt = f.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var outgoing = await _dbContext.Friendships
            .Where(f => f.Status == FriendshipStatus.Pending && f.SenderId == currentUserId)
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => new
            {
                id = f.Id,
                token = f.Token,
                receiverEmail = f.ReceiverEmail,
                createdAt = f.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            incoming,
            outgoing,
            incomingCount = incoming.Count
        });
    }

    [Authorize]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> RemoveFriend(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var friendship = await _dbContext.Friendships
            .FirstOrDefaultAsync(f =>
                (f.Id == id && (f.SenderId == currentUserId || f.ReceiverId == currentUserId)) ||
                (f.Status == FriendshipStatus.Accepted &&
                 ((f.SenderId == currentUserId && f.ReceiverId == id) ||
                  (f.ReceiverId == currentUserId && f.SenderId == id))),
                cancellationToken);

        if (friendship is null)
        {
            return NotFound(new { message = "No friend request or friendship found to delete." });
        }

        _dbContext.Friendships.Remove(friendship);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new { success = true, message = "Deleted successfully." });
    }

    private bool TryGetUserId(out Guid userId)
    {
        userId = Guid.Empty;
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out userId);
    }

    private string ResolveFrontendUrl()
    {
        var configured = _config["Frontend:BaseUrl"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured.TrimEnd('/');
        }

        var scheme = Request.Headers["X-Forwarded-Proto"].FirstOrDefault() ?? Request.Scheme;
        var host = Request.Headers["X-Forwarded-Host"].FirstOrDefault() ?? Request.Host.Value;
        if (!string.IsNullOrWhiteSpace(host) && !host.Contains(":5232") && !host.Contains(":8082") && !host.Contains(":8080"))
        {
            return $"{scheme}://{host}".TrimEnd('/');
        }

        return "https://losm.duckdns.org";
    }
}

public sealed class InviteFriendDto
{
    public string? Email { get; set; }
    public string? ReceiverEmail { get; set; }
    public string? Message { get; set; }

    public string TargetEmail =>
        !string.IsNullOrWhiteSpace(ReceiverEmail) ? ReceiverEmail :
        !string.IsNullOrWhiteSpace(Email) ? Email : string.Empty;
}

public sealed class RespondFriendDto
{
    public Guid? FriendshipId { get; set; }
    public string? Token { get; set; }
    public bool? Accept { get; set; }
    public string? Action { get; set; }

    public bool IsAccepted =>
        Accept == true ||
        string.Equals(Action, "accept", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Action, "accepted", StringComparison.OrdinalIgnoreCase);
}
