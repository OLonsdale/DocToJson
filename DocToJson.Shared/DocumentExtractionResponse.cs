namespace DocToJson.Shared;

public class DocumentExtractionResponse
{
    public bool IsError { get; set; }
    public string? Data { get; set; }   // the assistant text (your JSON string)
    public string? Error { get; set; }  // error message or raw error JSON
}
