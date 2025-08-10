namespace DocToJson.Client.Data;

using System;
using System.Collections.Generic;
using DocToJson.Shared;

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

    public RunDetails? Details { get; set; }
    public Usage? Usage { get; set; }
    public List<FileProvenance>? Files { get; set; }
}
