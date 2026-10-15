using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace REPS_IMS.Pages.Forms;

public class DashboardModel : PageModel
{
    private readonly IConfiguration _configuration;

public DashboardModel(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public List<RegionReport> RegionReports { get; set; } = new();

    public class RegionReport
    {
        public string RegionName { get; set; } = "";
        public int TotalIncidents { get; set; }

        public List<CategoryCount> Categories { get; set; } = new();
    }

    public class CategoryCount
    {
        public string CategoryName { get; set; } = "";
        public int Total { get; set; }
    }

    public async Task OnGetAsync()
    {
        var connectionString =
            _configuration.GetConnectionString("RepsDatabase");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();



        const string sql = @"
            DECLARE @StartDate date =
                DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1);

            DECLARE @EndDate date = DATEADD(MONTH, 1, @StartDate);

            SELECT i.IncidentID, i.RegionID
            INTO #MonthlyIncidents
            FROM dbo.IncidentRegister AS i
            WHERE i.CaptureDate >= @StartDate
              AND i.CaptureDate < @EndDate;

            CREATE CLUSTERED INDEX IX_MonthlyIncidents_IncidentID
                ON #MonthlyIncidents (IncidentID);

            WITH RegionalTotals AS
            (
                SELECT RegionID,
                       COUNT(DISTINCT IncidentID) AS TotalIncidents
                FROM #MonthlyIncidents
                GROUP BY RegionID
            ),
            CategoryTotals AS
            (
                SELECT mi.RegionID,
                       d.IncidentCategoryID,
                       COUNT(d.TypeDetailsID) AS TotalDetails
                FROM #MonthlyIncidents AS mi
                INNER JOIN dbo.IncidentTypeDetails AS d
                    ON d.IncidentID = mi.IncidentID
                WHERE d.Status <> 4
                GROUP BY mi.RegionID, d.IncidentCategoryID
            )
            SELECT
                r.RegionID,
                r.RegionName,
                c.IncidentCategoryID,
                c.IncidentCategoryName,
                COALESCE(rt.TotalIncidents, 0) AS TotalIncidents,
                COALESCE(ct.TotalDetails, 0) AS TotalDetails
            FROM dbo.Region AS r
            CROSS JOIN dbo.IncidentCategory AS c
            LEFT JOIN RegionalTotals AS rt
                ON rt.RegionID = r.RegionID
            LEFT JOIN CategoryTotals AS ct
                ON ct.RegionID = r.RegionID
                AND ct.IncidentCategoryID = c.IncidentCategoryID
            ORDER BY r.RegionName, c.IncidentCategoryName;";


        await using var command = new SqlCommand(sql, connection);
        command.CommandTimeout = 120;
        await using var reader = await command.ExecuteReaderAsync();

        var reports = new Dictionary<int, RegionReport>();

        while (await reader.ReadAsync())
        {
            int regionId = reader.GetInt32(0);

            if (!reports.TryGetValue(regionId, out var report))
            {
                report = new RegionReport
                {
                    RegionName = reader.GetString(1),
                    TotalIncidents = Convert.ToInt32(reader["TotalIncidents"])
                };

                reports.Add(regionId, report);
            }

            report.Categories.Add(new CategoryCount
            {
                CategoryName = reader.GetString(3),
                Total = Convert.ToInt32(reader["TotalDetails"])
            });
        }

        RegionReports = reports.Values.ToList();
    }


}
