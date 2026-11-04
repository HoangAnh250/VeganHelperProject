namespace VeganHelper.API.Infrastructure.AI;

public sealed class AiChatPromptDefinition
{
    public string Version { get; init; } = string.Empty;
    public string SystemInstruction { get; init; } = string.Empty;
    public string PostSearchInstruction { get; init; } = string.Empty;
}
