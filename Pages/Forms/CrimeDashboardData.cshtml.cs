using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using System.Data;

namespace REPS_IMS.Pages.Forms;

[IgnoreAntiforgeryToken]
public class CrimeDashboardDataModel : PageModel
{
    private readonly IConfiguration _configuration;

    public CrimeDashboardDataModel(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var results = new List<CrimeChartNode>();

        var connectionString =
            _configuration.GetConnectionString("RepsDatabase");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return StatusCode(
                500, "Database connection is not configured.");
        }

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = @"
            SELECT TOP (@TopCount)
                t.IncidentTypeName AS [Name],
                COUNT(*) AS [Size]
            FROM dbo.IncidentTypeDetails d
            INNER JOIN dbo.IncidentRegister i
                ON i.IncidentID = d.IncidentID
            INNER JOIN dbo.IncidentType t
                ON t.IncidentTypeID = d.IncidentTypeID
            WHERE i.CaptureDate >= @StartDate
              AND i.CaptureDate < @EndDate
            GROUP BY
                t.IncidentTypeID,
                t.IncidentTypeName
            HAVING COUNT(*) > 0
            ORDER BY COUNT(*) DESC;";

        var endDate = DateTime.Today.AddDays(1);
        //var startDate = endDate.AddYears(-5);
        var startDate = endDate.AddDays(-30);

        // Matches the original DashboardCrimeCount setting.
        var topCount = 10;

        await using var command = new SqlCommand(sql, connection);

        command.Parameters.Add("@TopCount", SqlDbType.Int)
            .Value = topCount;

        command.Parameters.Add("@StartDate", SqlDbType.DateTime)
            .Value = startDate;

        command.Parameters.Add("@EndDate", SqlDbType.DateTime)
            .Value = endDate;

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            results.Add(new CrimeChartNode
            {
                Name = reader.GetString(0),
                Size = Convert.ToInt32(reader.GetValue(1))
            });
        }

        return new JsonResult(results);
    }
}

public class CrimeChartNode
{
    public string Name { get; set; } = string.Empty;
    public int Size { get; set; }
}