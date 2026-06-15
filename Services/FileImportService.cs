namespace FileImporter.Services;

public class FileImportService
{
    private readonly OracleService     _oracleService;
    private readonly FileBackupService _backupService;
    private readonly System.Text.Encoding _encoding;

    public FileImportService(OracleService oracleService, FileBackupService backupService, string fileEncoding = "big5")
    {
        _oracleService = oracleService;
        _backupService  = backupService;

        // 註冊 Big5 等非 Unicode 編碼（.NET Core 需要）
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        _encoding = System.Text.Encoding.GetEncoding(fileEncoding);
    }

    /// <summary>
    /// 處理單一目錄：掃描所有 .csv / .txt 並逐檔匯入
    /// </summary>
    public async Task ProcessDirectoryAsync(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            Console.WriteLine($"[SKIP] 目錄不存在：{directoryPath}");
            return;
        }

        var files = Directory.GetFiles(directoryPath, "*.*")
            .Where(f =>
            {
                var ext = Path.GetExtension(f).ToLowerInvariant();
                return ext == ".csv" || ext == ".txt";
            })
            .OrderBy(f => f)
            .ToList();

        if (files.Count == 0)
        {
            Console.WriteLine($"[INFO] 無待處理檔案：{directoryPath}");
            return;
        }

        Console.WriteLine($"[INFO] 開始處理目錄：{directoryPath}，共 {files.Count} 個檔案");

        foreach (var filePath in files)
        {
            await ProcessFileAsync(directoryPath, filePath);
        }
    }

    /// <summary>
    /// 處理單一檔案：讀取 → Insert → 備份，失敗則寫 Log
    /// </summary>
    private async Task ProcessFileAsync(string directoryPath, string filePath)
    {
        var fileName  = Path.GetFileName(filePath);
        var dirName   = new DirectoryInfo(directoryPath).Name;

        Console.WriteLine($"  [FILE] {fileName}");

        try
        {
            // 讀取所有非空白行（Tab 分隔，原始保留）
            var lines = File.ReadAllLines(filePath, _encoding)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToList();

            if (lines.Count == 0)
            {
                Console.WriteLine($"  [SKIP] 檔案無資料：{fileName}");
                return;
            }

            // 寫入 Oracle
            await _oracleService.BulkInsertAsync(dirName, fileName, lines);

            // 備份原始檔
            _backupService.Backup(filePath);

            Console.WriteLine($"  [OK]   {fileName}，共 {lines.Count} 筆");
        }
        catch (Exception ex)
        {
            var errorMsg = $"[ERROR] {DateTime.Now:yyyy-MM-dd HH:mm:ss} | 檔案：{fileName} | {ex.Message}{Environment.NewLine}";
            Console.WriteLine($"  {errorMsg.Trim()}");
            WriteErrorLog(directoryPath, errorMsg);
        }
    }

    /// <summary>
    /// 將錯誤寫入目錄下的 import_error.log
    /// </summary>
    private static void WriteErrorLog(string directoryPath, string message)
    {
        try
        {
            var logPath = Path.Combine(directoryPath, "import_error.log");
            File.AppendAllText(logPath, message, System.Text.Encoding.UTF8);
        }
        catch (Exception logEx)
        {
            Console.WriteLine($"  [WARN] 無法寫入 log：{logEx.Message}");
        }
    }
}
