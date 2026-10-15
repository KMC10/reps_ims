using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using System.Data;

namespace REPS_IMS.Pages.Forms;

[IgnoreAntiforgeryToken]
public class IncidentMapModel : PageModel
{
    private readonly IConfiguration _configuration;

    public IncidentMapModel(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var markers = new List<IncidentMapData>();

        var connectionString =
            _configuration.GetConnectionString("RepsDatabase");

        if (string.IsNullOrWhiteSpace(connectionString))
            return StatusCode(500, "Database connection is not configured.");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = @"
            SELECT
                r.RegionName,
                r.Latitude AS Lat,
                r.Longitude AS Lon,
                COUNT(i.IncidentID) AS [Count]
            FROM dbo.Region r
            INNER JOIN dbo.IncidentRegister i
                ON i.RegionID = r.RegionID
            WHERE i.CaptureDate >= @StartDate
              AND i.CaptureDate < @EndDate
            GROUP BY
                r.RegionName,
                r.Latitude,
                r.Longitude
            ORDER BY r.RegionName;";

        var endDate = DateTime.Today.AddDays(1);
        var startDate = endDate.AddDays(-365);

        await using var command = new SqlCommand(sql, connection);

        command.Parameters.Add("@StartDate", SqlDbType.DateTime).Value = startDate;
        command.Parameters.Add("@EndDate", SqlDbType.DateTime).Value = endDate;

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            markers.Add(new IncidentMapData
            {
                RegionName = reader.GetString(0),
                Lat = Convert.ToDouble(reader.GetValue(1)),
                Lon = Convert.ToDouble(reader.GetValue(2)),
                Count = Convert.ToInt32(reader.GetValue(3))
            });
        }

        return new JsonResult(markers);
    }
}