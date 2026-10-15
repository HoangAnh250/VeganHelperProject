namespace VeganHelper.API.Configuration;

public sealed class GeminiOptions
{
    public bool Enabled { get; set; } = true;
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gemini-2.5-flash-lite";
    public string Endpoint { get; set; } = "https://generativelanguage.googleapis.com/v1beta";
    public int TimeoutSeconds { get; set; } = 30;
    public int MaxOutputTokens { get; set; } = 512;
    public int MaxInputCharacters { get; set; } = 30_000;
    public double Temperature { get; set; } = 0.2;
}
