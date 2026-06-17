using FileImporter.Models;
using FileImporter.Services;
using Microsoft.Extensions.Configuration;

// ── 讀取設定 ──────────────────────────────────────────────
var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .Build();

var importConfig = config.Get<ImportConfig>()
    ?? throw new InvalidOperationException("appsettings.json 設定讀取失敗");

if (string.IsNullOrWhiteSpace(importConfig.Oracle.ConnectionString))
    throw new InvalidOperationException("Oracle ConnectionString 未設定");

// ── 初始化服務 ────────────────────────────────────────────
var oracleService = new OracleService(importConfig.Oracle.ConnectionString);
var backupService = new FileBackupService(importConfig.RenameOnBackupFiles);
var importService = new FileImportService(oracleService, backupService, importConfig.FileEncoding);
var emailService  = new EmailService(importConfig.Email);

// ── 主流程 ────────────────────────────────────────────────
Console.WriteLine($"===== FileImporter 開始 {DateTime.Now:yyyy-MM-dd HH:mm:ss} =====");

var allErrors = new List<FileImporter.Models.ImportError>();

foreach (var dir in importConfig.ImportDirectories)
{
    var errors = await importService.ProcessDirectoryAsync(dir);
    allErrors.AddRange(errors);
}

// ── 匯整錯誤寄送通知 mail ─────────────────────────────────
if (allErrors.Count > 0)
{
    Console.WriteLine($"[MAIL] 發現 {allErrors.Count} 個錯誤，準備寄送通知...");
    try
    {
        await emailService.SendErrorSummaryAsync(allErrors);
        Console.WriteLine("[MAIL] 通知信已寄出");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[MAIL ERROR] 寄信失敗：{ex.Message}");
    }
}

Console.WriteLine($"===== FileImporter 完成 {DateTime.Now:yyyy-MM-dd HH:mm:ss} =====");
