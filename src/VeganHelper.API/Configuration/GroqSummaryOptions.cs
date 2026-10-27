namespace VeganHelper.API.Configuration;

public sealed class GroqSummaryOptions
{
    public bool Enabled { get; set; }
    public string ApiKey { get; set; } = "";
    public string WhisperModel { get; set; } = "whisper-large-v3-turbo";
    public string SummaryModel { get; set; } = "openai/gpt-oss-20b";
    public string FfmpegPath { get; set; } = "ffmpeg";
}
