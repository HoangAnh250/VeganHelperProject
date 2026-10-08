namespace VeganHelper.BLL.Exceptions;

public sealed class ConflictException(string message) : Exception(message);
