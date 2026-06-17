using Oracle.ManagedDataAccess.Client;

namespace FileImporter.Services;

public class OracleService
{
    private readonly string _connectionString;

    public OracleService(string connectionString)
    {
        _connectionString = connectionString;
    }

    /// <summary>
    /// 批次寫入一個檔案的所有資料列，寫入後呼叫 procedure。
    /// 回傳 procedure 的 o_error_msg（無錯誤則為 null）
    /// </summary>
    public async Task<string?> BulkInsertAsync(
        string dirName,
        string fileName,
        List<string> lines)
    {
        await using var conn = new OracleConnection(_connectionString);
        await conn.OpenAsync();

        // 取得本次 Oracle session_id
        var sessionId = await GetSessionIdAsync(conn);

        await using var tran = await conn.BeginTransactionAsync() as OracleTransaction
            ?? throw new InvalidOperationException("無法建立 Oracle Transaction");

        try
        {
            const string sql = """
                INSERT INTO bnk.w_bnk_fedi_data (session_id, data0, data1, data2, data3)
                VALUES (:session_id, :data0, :data1, :data2, :data3)
                """;

            await using var cmd = new OracleCommand(sql, conn);
            cmd.Transaction  = tran;
            cmd.BindByName   = true;
            cmd.ArrayBindCount = lines.Count;

            var sessionIdArr = Enumerable.Repeat(sessionId, lines.Count).ToArray();
            var data0Arr     = Enumerable.Repeat(dirName,   lines.Count).ToArray();
            var data1Arr     = lines.ToArray();
            var data2Arr     = Enumerable.Repeat(fileName,  lines.Count).ToArray();
            var data3Arr     = Enumerable.Range(1, lines.Count).Select(i => i.ToString()).ToArray();

            cmd.Parameters.Add(new OracleParameter("session_id", OracleDbType.Varchar2, sessionIdArr, System.Data.ParameterDirection.Input));
            cmd.Parameters.Add(new OracleParameter("data0",      OracleDbType.Varchar2, data0Arr,     System.Data.ParameterDirection.Input));
            cmd.Parameters.Add(new OracleParameter("data1",      OracleDbType.Varchar2, data1Arr,     System.Data.ParameterDirection.Input));
            cmd.Parameters.Add(new OracleParameter("data2",      OracleDbType.Varchar2, data2Arr,     System.Data.ParameterDirection.Input));
            cmd.Parameters.Add(new OracleParameter("data3",      OracleDbType.Varchar2, data3Arr,     System.Data.ParameterDirection.Input));

            await cmd.ExecuteNonQueryAsync();
            await tran.CommitAsync();
        }
        catch
        {
            await tran.RollbackAsync();
            throw;
        }

        // 呼叫 procedure（commit 之後，獨立執行）
        return await CallProcedureAsync(conn, sessionId);
    }

    /// <summary>
    /// 查詢目前 Oracle session_id
    /// </summary>
    private static async Task<string> GetSessionIdAsync(OracleConnection conn)
    {
        await using var cmd = new OracleCommand(
            "SELECT CAST(userenv('sessionid') AS VARCHAR2(50)) FROM dual", conn);
        var result = await cmd.ExecuteScalarAsync();
        return result?.ToString() ?? string.Empty;
    }

    /// <summary>
    /// 呼叫 bnk.bnk_hawd_pkg.p_exec_hawd_job，回傳 o_error_msg（無錯誤則為 null）
    /// </summary>
    private static async Task<string?> CallProcedureAsync(OracleConnection conn, string sessionId)
    {
        await using var cmd = new OracleCommand(
            "BEGIN bnk.bnk_hawd_pkg.p_exec_hawd_job(:i_session_id, :o_error_msg); END;", conn);
        cmd.BindByName = true;

        cmd.Parameters.Add(new OracleParameter("i_session_id", OracleDbType.Varchar2, sessionId, System.Data.ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter("o_error_msg",  OracleDbType.Varchar2, 4000, null, System.Data.ParameterDirection.Output));

        await cmd.ExecuteNonQueryAsync();

        var errorMsg = cmd.Parameters["o_error_msg"].Value?.ToString();
        return string.IsNullOrWhiteSpace(errorMsg) ? null : errorMsg;
    }
}
