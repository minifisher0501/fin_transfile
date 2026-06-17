namespace FileImporter.Models;

public class ImportError
{
    public string Directory { get; set; } = string.Empty;
    public string FileName  { get; set; } = string.Empty;
    public string Message   { get; set; } = string.Empty;
    public ErrorType Type   { get; set; }
}

public enum ErrorType
{
    InsertError,
    ProcedureError
}
