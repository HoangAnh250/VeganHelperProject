namespace VeganHelper.BLL.Exceptions;

public sealed class ReportRateLimitException() : Exception("At most 10 reports per hour are allowed. Please try again later.");
