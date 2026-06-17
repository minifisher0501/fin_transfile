namespace FileImporter.Models;

public class ImportConfig
{
    public OracleConfig Oracle { get; set; } = new();
    public List<string> ImportDirectories { get; set; } = new();

    /// <summary>
    /// 來源檔案編碼，預設 big5，可改 utf-8
    /// </summary>
    public string FileEncoding { get; set; } = "big5";

    /// <summary>
    /// 備份時需改名的固定檔名清單（不分大小寫），改名為 {原檔名}_{yyyyMMddHHmm}{副檔名}
    /// </summary>
    public List<string> RenameOnBackupFiles { get; set; } = new();

    public EmailConfig Email { get; set; } = new();
}

public class EmailConfig
{
    public string SmtpHost { get; set; } = string.Empty;
    public int    SmtpPort { get; set; } = 25;
    public bool   UseSsl   { get; set; } = false;
    public string From     { get; set; } = string.Empty;
    public List<string> To { get; set; } = new();
}

public class OracleConfig
{
    public string ConnectionString { get; set; } = string.Empty;
}
