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
    private readonly UsersDbContext _dbContext;
    private readonly IEmailSender _emailSender;
    private readonly IConfiguration _config;
    private readonly ILogger<FriendsController> _logger;

    public FriendsController(
        UsersDbContext dbContext,
        IEmailSender emailSender,
        IConfiguration config,
        ILogger<FriendsController> logger)
    {
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
        if (string.IsNullOrWhiteSpace(dto.Email) || !EmailRegex.IsMatch(dto.Email.Trim()))
        {
            return BadRequest(new { message = "Vui lòng nhập địa chỉ email hợp lệ." });
        }

        var targetEmail = dto.Email.Trim().ToLowerInvariant();
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
            return BadRequest(new { message = "Bạn không thể gửi lời mời kết bạn cho chính mình." });
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
                return BadRequest(new { message = "Hai bạn đã là bạn bè trên hệ thống." });
            }

            if (existingFriendship.Status == FriendshipStatus.Pending)
            {
                return BadRequest(new { message = "Lời mời kết bạn đã được gửi trước đó và đang chờ phản hồi." });
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

        var html = $@"
<div style=""font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; max-width: 580px; margin: 0 auto; padding: 24px; border: 1px solid #e2e8f0; border-radius: 16px; background-color: #ffffff; color: #1e293b;"">
  <div style=""border-bottom: 2px solid #3b82f6; padding-bottom: 16px; margin-bottom: 24px;"">
    <h1 style=""margin: 0; color: #1e293b; font-size: 22px; font-weight: 700;"">📍 LOSM - Tìm Điểm Hẹn</h1>
    <p style=""margin: 4px 0 0 0; color: #64748b; font-size: 13px;"">Nền tảng tìm điểm hẹn công bằng và tối ưu lộ trình</p>
  </div>

  <p style=""font-size: 15px; line-height: 1.6; margin-bottom: 16px;"">
    Xin chào,
  </p>
  <p style=""font-size: 15px; line-height: 1.6; margin-bottom: 24px;"">
    <strong>{sender.Username}</strong> ({sender.Email}) vừa gửi lời mời kết bạn với bạn trên <strong>LOSM</strong>. 
    Khi trở thành bạn bè, hai bạn có thể cùng nhau tìm quán cà phê, nhà hàng và điểm hẹn chung với khoảng cách và thời gian di chuyển công bằng nhất!
  </p>

  <div style=""text-align: center; margin: 32px 0;"">
    <a href=""{inviteUrl}"" style=""background-color: #2563eb; color: #ffffff; padding: 14px 28px; text-decoration: none; border-radius: 10px; font-weight: 600; font-size: 14px; display: inline-block; box-shadow: 0 4px 6px -1px rgba(37, 99, 235, 0.2);"">
      Xem và chấp nhận lời mời kết bạn
    </a>
  </div>

  <p style=""font-size: 12px; color: #94a3b8; line-height: 1.5; margin-top: 24px; border-top: 1px solid #f1f5f9; padding-top: 16px;"">
    Nếu nút bấm trên không hoạt động, bạn có thể sao chép và dán liên kết sau vào trình duyệt:<br/>
    <a href=""{inviteUrl}"" style=""color: #2563eb; word-break: break-all;"">{inviteUrl}</a>
  </p>
</div>";

        var plainText = $"Xin chào!\n\n{sender.Username} ({sender.Email}) vừa gửi lời mời kết bạn với bạn trên LOSM.\nNhấn vào liên kết sau để xem và chấp nhận lời mời:\n{inviteUrl}\n";

        await _emailSender.SendAsync(new EmailRequest(
            ToAddress: targetEmail,
            ToName: receiver?.Username ?? targetEmail,
            Subject: $"[LOSM] {sender.Username} đã gửi cho bạn lời mời kết bạn",
            HtmlBody: html,
            PlainTextBody: plainText
        ), cancellationToken);

        return Ok(new
        {
            success = true,
            message = $"Đã gửi lời mời kết bạn đến {targetEmail} thành công!",
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
            return BadRequest(new { message = "Mã token không hợp lệ." });
        }

        var friendship = await _dbContext.Friendships
            .Include(f => f.Sender)
            .FirstOrDefaultAsync(f => f.Token == token.Trim(), cancellationToken);

        if (friendship is null)
        {
            return NotFound(new { message = "Lời mời kết bạn không tồn tại hoặc đã hết hạn." });
        }

        var userExists = await _dbContext.Users.AnyAsync(u => u.Email == friendship.ReceiverEmail, cancellationToken);

        return Ok(new
        {
            id = friendship.Id,
            senderId = friendship.SenderId,
            senderName = friendship.Sender.Username,
            senderEmail = friendship.Sender.Email,
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
            return NotFound(new { message = "Không tìm thấy lời mời kết bạn." });
        }

        var isRecipient = friendship.ReceiverId == currentUserId ||
                          string.Equals(friendship.ReceiverEmail, currentUser.Email, StringComparison.OrdinalIgnoreCase);

        if (!isRecipient)
        {
            return Forbid();
        }

        if (dto.Accept)
        {
            friendship.Accept(currentUserId);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Ok(new
            {
                success = true,
                status = "Accepted",
                message = $"Bạn và {friendship.Sender.Username} đã trở thành bạn bè!",
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
                message = "Đã từ chối lời mời kết bạn."
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
            return new
            {
                id = friendUser?.Id ?? (isSender ? f.ReceiverId ?? Guid.Empty : f.SenderId),
                username = friendUser?.Username ?? (isSender ? f.ReceiverEmail.Split('@')[0] : f.Sender.Username),
                email = friendUser?.Email ?? (isSender ? f.ReceiverEmail : f.Sender.Email),
                since = f.UpdatedAt ?? f.CreatedAt
            };
        }).Where(f => f.id != Guid.Empty).ToList();

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

        var incoming = await _dbContext.Friendships
            .Include(f => f.Sender)
            .Where(f => f.Status == FriendshipStatus.Pending &&
                        (f.ReceiverId == currentUserId || f.ReceiverEmail == currentUser.Email.ToLower()))
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => new
            {
                id = f.Id,
                token = f.Token,
                senderId = f.SenderId,
                senderName = f.Sender.Username,
                senderEmail = f.Sender.Email,
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
    [HttpDelete("{friendId:guid}")]
    public async Task<IActionResult> RemoveFriend(Guid friendId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var friendship = await _dbContext.Friendships
            .FirstOrDefaultAsync(f =>
                f.Status == FriendshipStatus.Accepted &&
                ((f.SenderId == currentUserId && f.ReceiverId == friendId) ||
                 (f.ReceiverId == currentUserId && f.SenderId == friendId)),
                cancellationToken);

        if (friendship is null)
        {
            return NotFound(new { message = "Không tìm thấy mối quan hệ bạn bè để xóa." });
        }

        _dbContext.Friendships.Remove(friendship);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
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

public sealed record InviteFriendDto(string Email);

public sealed record RespondFriendDto(Guid? FriendshipId, string? Token, bool Accept);
