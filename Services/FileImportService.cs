using FileImporter.Models;

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

        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        _encoding = System.Text.Encoding.GetEncoding(fileEncoding);
    }

    /// <summary>
    /// 處理單一目錄，回傳該目錄發生的所有錯誤清單
    /// </summary>
    public async Task<List<ImportError>> ProcessDirectoryAsync(string directoryPath)
    {
        var errors = new List<ImportError>();

        if (!Directory.Exists(directoryPath))
        {
            Console.WriteLine($"[SKIP] 目錄不存在：{directoryPath}");
            return errors;
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
            return errors;
        }

        Console.WriteLine($"[INFO] 開始處理目錄：{directoryPath}，共 {files.Count} 個檔案");

        foreach (var filePath in files)
        {
            var fileErrors = await ProcessFileAsync(directoryPath, filePath);
            errors.AddRange(fileErrors);
        }

        return errors;
    }

    /// <summary>
    /// 處理單一檔案，回傳該檔案的錯誤清單（Insert 錯誤 + Procedure 錯誤）
    /// </summary>
    private async Task<List<ImportError>> ProcessFileAsync(string directoryPath, string filePath)
    {
        var errors   = new List<ImportError>();
        var fileName = Path.GetFileName(filePath);
        var dirName  = new DirectoryInfo(directoryPath).Name;

        Console.WriteLine($"  [FILE] {fileName}");

        try
        {
            var lines = File.ReadAllLines(filePath, _encoding)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToList();

            if (lines.Count == 0)
            {
                Console.WriteLine($"  [SKIP] 檔案無資料：{fileName}");
                return errors;
            }

            // Insert + 呼叫 Procedure
            var procError = await _oracleService.BulkInsertAsync(dirName, fileName, lines);

            // 備份原始檔（不管 procedure 成功與否，insert 已 commit 就備份）
            _backupService.Backup(filePath);

            if (procError is not null)
            {
                Console.WriteLine($"  [PROC ERROR] {fileName}：{procError}");
                var err = new ImportError
                {
                    Directory = directoryPath,
                    FileName  = fileName,
                    Message   = procError,
                    Type      = ErrorType.ProcedureError
                };
                errors.Add(err);
                WriteErrorLog(directoryPath, $"[PROC ERROR] {DateTime.Now:yyyy-MM-dd HH:mm:ss} | 檔案：{fileName} | {procError}{Environment.NewLine}");
            }
            else
            {
                Console.WriteLine($"  [OK]   {fileName}，共 {lines.Count} 筆");
            }
        }
        catch (Exception ex)
        {
            var msg = ex.Message;
            Console.WriteLine($"  [ERROR] {fileName}：{msg}");

            var err = new ImportError
            {
                Directory = directoryPath,
                FileName  = fileName,
                Message   = msg,
                Type      = ErrorType.InsertError
            };
            errors.Add(err);
            WriteErrorLog(directoryPath, $"[ERROR] {DateTime.Now:yyyy-MM-dd HH:mm:ss} | 檔案：{fileName} | {msg}{Environment.NewLine}");
        }

        return errors;
    }

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
