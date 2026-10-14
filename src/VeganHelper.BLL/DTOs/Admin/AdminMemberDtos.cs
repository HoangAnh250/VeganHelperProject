using System.ComponentModel.DataAnnotations;

namespace VeganHelper.BLL.DTOs.Admin;

public sealed class AdminMemberListRequest
{
    public int PageIndex { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    [StringLength(100)] public string? Keyword { get; init; }
    public string? Role { get; init; }
    public string? Status { get; init; }
}
public sealed class BanMemberRequest
{
    [Required, StringLength(500)] public string Reason { get; init; } = "";
    public DateTimeOffset? ExpiresAt { get; init; }
}
public sealed class UnbanMemberRequest
{
    [Required, StringLength(500)] public string Reason { get; init; } = "";
}
public sealed class AdminMemberDto
{
    public long Id { get; init; }
    public string Username { get; init; } = "";
    public string Email { get; init; } = "";
    public string? DisplayName { get; init; }
    public string? AvatarUrl { get; init; }
    public string Role { get; init; } = "";
    public string Status { get; init; } = "";
    public bool IsActive { get; init; }
    public DateTime? EmailVerifiedAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? LastLoginAt { get; init; }
    public DateTime? LockedUntil { get; init; }
    public long? BanId { get; init; }
    public string? BanReason { get; init; }
    public DateTime? BanExpiresAt { get; init; }
}
