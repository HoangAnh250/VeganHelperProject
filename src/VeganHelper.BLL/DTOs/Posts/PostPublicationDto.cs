namespace VeganHelper.BLL.DTOs.Posts;

public sealed record PostPublicationDto(long Id, string Status, int ContentRevision, bool ModerationHold);
