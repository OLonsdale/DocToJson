namespace DocToJson.Shared;
public class DocumentExtractionResponse
{
    public bool IsError { get; set; }
    public string? Error { get; set; }
    public string? Data { get; set; }
    public RunDetails? Details { get; set; }
    public Usage? Usage { get; set; }              
    public FileProvenance[]? Files { get; set; }   
}

public sealed class RunDetails
{
    public string Model { get; set; } = "";
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int TotalTokens { get; set; }
    public int LatencyMs { get; set; }
    public decimal EstimatedCostUsd { get; set; }
}


public sealed record Usage(
    int InputTokens,
    int OutputTokens,
    int TotalTokens,
    int? ReasoningTokens
);

public sealed record FileProvenance(
    string FileName,
    string FileId,
    long SizeBytes,
    string? ContentType
);