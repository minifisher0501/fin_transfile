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
    /// 批次寫入一個檔案的所有資料列
    /// </summary>
    public async Task BulkInsertAsync(
        string dirName,
        string fileName,
        List<string> lines)
    {
        await using var conn = new OracleConnection(_connectionString);
        await conn.OpenAsync();

        await using var tran = await conn.BeginTransactionAsync() as OracleTransaction
            ?? throw new InvalidOperationException("無法建立 Oracle Transaction");

        try
        {
            const string sql = """
                INSERT INTO bnk.w_bnk_fedi_data (session_id, data0, data1, data2, data3)
                VALUES (userenv('sessionid'), :data0, :data1, :data2, :data3)
                """;

            await using var cmd = new OracleCommand(sql, conn);
            cmd.Transaction = tran;
            cmd.BindByName = true;

            // 使用 Array Binding 批次 Insert
            cmd.ArrayBindCount = lines.Count;

            var data0Arr  = Enumerable.Repeat(dirName, lines.Count).ToArray();
            var data1Arr  = lines.ToArray();
            var data2Arr  = Enumerable.Repeat(fileName, lines.Count).ToArray();
            var data3Arr  = Enumerable.Range(1, lines.Count).Select(i => i.ToString()).ToArray();

            cmd.Parameters.Add(new OracleParameter("data0", OracleDbType.Varchar2, data0Arr,  System.Data.ParameterDirection.Input));
            cmd.Parameters.Add(new OracleParameter("data1", OracleDbType.Varchar2, data1Arr,  System.Data.ParameterDirection.Input));
            cmd.Parameters.Add(new OracleParameter("data2", OracleDbType.Varchar2, data2Arr,  System.Data.ParameterDirection.Input));
            cmd.Parameters.Add(new OracleParameter("data3", OracleDbType.Varchar2, data3Arr,  System.Data.ParameterDirection.Input));

            await cmd.ExecuteNonQueryAsync();
            await tran.CommitAsync();
        }
        catch
        {
            await tran.RollbackAsync();
            throw;
        }
    }
}
