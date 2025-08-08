namespace DocToJson.Shared;

public class DocumentExtractionRequest
{

        public string Prompt { get; set; } = "";
        public List<FilePart> Files { get; set; } = new();
        public string? JsonSchema { get; set; }
        public string? SchemaName { get; set; }
    


}
    public class FilePart
    {
        public string FileName { get; set; } = "";
        public byte[] Bytes { get; set; } = Array.Empty<byte>();
    }
