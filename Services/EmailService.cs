using FileImporter.Models;
using MailKit.Net.Smtp;
using MimeKit;

namespace FileImporter.Services;

public class EmailService
{
    private readonly EmailConfig _config;

    public EmailService(EmailConfig config)
    {
        _config = config;
    }

    /// <summary>
    /// 匯整所有錯誤寄送一封通知 mail
    /// </summary>
    public async Task SendErrorSummaryAsync(List<ImportError> errors)
    {
        if (errors.Count == 0 || _config.To.Count == 0) return;

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(_config.From));
        foreach (var to in _config.To)
            message.To.Add(MailboxAddress.Parse(to));

        message.Subject = $"[FileImporter] 轉檔錯誤通知 {DateTime.Now:yyyy-MM-dd HH:mm}";
        message.Body    = new TextPart("plain") { Text = BuildBody(errors) };

        using var client = new SmtpClient();
        await client.ConnectAsync(_config.SmtpHost, _config.SmtpPort,
            _config.UseSsl
                ? MailKit.Security.SecureSocketOptions.SslOnConnect
                : MailKit.Security.SecureSocketOptions.None);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }

    private static string BuildBody(List<ImportError> errors)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("FileImporter 轉檔發生以下錯誤，請確認：");
        sb.AppendLine();

        foreach (var grp in errors.GroupBy(e => e.Directory))
        {
            sb.AppendLine($"【目錄】{grp.Key}");
            foreach (var e in grp)
            {
                var tag = e.Type == ErrorType.ProcedureError ? "Procedure 錯誤" : "Insert 錯誤";
                sb.AppendLine($"  - 檔案：{e.FileName}  [{tag}] {e.Message}");
            }
            sb.AppendLine();
        }

        sb.AppendLine($"執行時間：{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        return sb.ToString();
    }
}
