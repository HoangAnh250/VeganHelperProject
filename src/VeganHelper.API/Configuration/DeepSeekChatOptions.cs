namespace VeganHelper.API.Configuration;

public sealed class DeepSeekChatOptions
{
    public bool Enabled { get; set; } = true;
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "deepseek-flash";
    public int TimeoutSeconds { get; set; } = 45;
    public int MaxOutputTokens { get; set; } = 1024;
    public int MaxInputCharacters { get; set; } = 30_000;
    public double Temperature { get; set; } = 0.2;
}
