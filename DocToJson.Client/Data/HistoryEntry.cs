namespace DocToJson.Client.Data;

public class HistoryEntry
{
    public Guid Id { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public List<string> FileNames { get; set; } = [];
    public string Prompt { get; set; } = "";
    public string? Schema { get; set; }
    public string? SchemaValidationErrors { get; set; }
    public string? SubmissionError { get; set; }
    public string? ResponseJson { get; set; }
}