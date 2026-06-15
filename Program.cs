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

// ── 主流程 ────────────────────────────────────────────────
Console.WriteLine($"===== FileImporter 開始 {DateTime.Now:yyyy-MM-dd HH:mm:ss} =====");

foreach (var dir in importConfig.ImportDirectories)
{
    await importService.ProcessDirectoryAsync(dir);
}

Console.WriteLine($"===== FileImporter 完成 {DateTime.Now:yyyy-MM-dd HH:mm:ss} =====");
