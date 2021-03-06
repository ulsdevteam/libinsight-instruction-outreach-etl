using dotenv.net;
using CommandLine;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;

DotEnv.Load();
IConfiguration config = new ConfigurationBuilder().AddEnvironmentVariables().Build();
await CommandLine.Parser.Default.ParseArguments<Options>(args).WithParsedAsync(async options =>
{
    try
    {
        var libInsightClient = new LibInsightClient();
        await libInsightClient.Authorize(config["LIBINSIGHT_CLIENT_ID"], config["LIBINSIGHT_CLIENT_SECRET"]);
        // These numbers come from the API set up in the LibInsight interface.
        var records = await libInsightClient.GetRecords(29168, 19, options.FromDate ?? StartOfFiscalYear(), options.ToDate ?? DateTime.Today);
        using var db = new Database(config["ORACLE_CONNECTION_STRING"]);
        await db.EnsureTablesExist();
        foreach (var record in records)
        {
            try
            {
                var recordId = (int?)record["_id"];
                if (recordId is null)
                {
                    Console.Error.WriteLine("Record is missing an Id.");
                }
                else if (await db.RecordExistsInDb(recordId.Value))
                {
                    await db.UpdateRecord(record);
                }
                else
                {
                    await db.InsertRecord(record);
                }
            }
            catch (OracleException exception)
            {
                Console.Error.WriteLine(exception);
            }
        }
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine(exception);
        throw;
    }
});

// Returns the first day of the previous July
DateTime StartOfFiscalYear() => new DateTime(DateTime.Today.Month > 7 ? DateTime.Today.Year : DateTime.Today.Year - 1, 7, 1);
