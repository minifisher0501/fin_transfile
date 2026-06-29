namespace FileImporter.Services;

public class FileBackupService
{
    private readonly HashSet<string> _renameOnBackupFiles;

    public FileBackupService(IEnumerable<string>? renameOnBackupFiles = null)
    {
        _renameOnBackupFiles = new HashSet<string>(
            renameOnBackupFiles ?? Enumerable.Empty<string>(),
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 將來源檔案備份至 {sourceDir}\{民國年月}\ 子目錄。
    /// 若檔名在 RenameOnBackupFiles 清單中，改名為 {原檔名}_{yyyyMMddHHmm}{副檔名}，否則同名覆蓋
    /// </summary>
    public void Backup(string sourceFilePath)
    {
        var sourceDir  = Path.GetDirectoryName(sourceFilePath)!;
        var fileName   = Path.GetFileName(sourceFilePath);
        var rocYearMonth = GetRocYearMonth();

        var backupDir = Path.Combine(sourceDir, rocYearMonth);
        Directory.CreateDirectory(backupDir); // 不存在則自動建立

        var destFileName = _renameOnBackupFiles.Contains(fileName)
            ? BuildRenamedFileName(fileName)
            : fileName;

        var destPath = Path.Combine(backupDir, destFileName);
        File.Copy(sourceFilePath, destPath, overwrite: true); // 同名覆蓋
        File.Delete(sourceFilePath);                          // 移除原始檔
    }

    /// <summary>
    /// 將檔名改為 {原檔名}_{yyyyMMddHHmm}{副檔名}
    /// </summary>
    private static string BuildRenamedFileName(string fileName)
    {
        var ext            = Path.GetExtension(fileName);
        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        var timestamp      = DateTime.Now.ToString("yyyyMMddHHmm");
        // 無副檔名時補 .txt
        if (string.IsNullOrEmpty(ext)) ext = ".txt";
        return $"{nameWithoutExt}_{timestamp}{ext}";
    }

    /// <summary>
    /// 取得民國年月字串，格式如 11506（民國115年06月）
    /// </summary>
    private static string GetRocYearMonth()
    {
        var now = DateTime.Now;
        var rocYear = now.Year - 1911;
        return $"{rocYear}{now.Month:D2}";
    }
}
