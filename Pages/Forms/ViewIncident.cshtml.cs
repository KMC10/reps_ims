
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using System.Globalization;

namespace REPS_IMS.Pages.Forms;

public class ViewIncidentModel : PageModel
{
    private readonly IConfiguration _configuration;

    public ViewIncidentModel(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [BindProperty]
    public string DateFrom { get; set; } = "";

    [BindProperty]
    public string DateTo { get; set; } = "";

    [BindProperty]
    public int CategoryId { get; set; } = -1;

    [BindProperty]
    public int TypeId { get; set; } = -1;

    [BindProperty]
    public int RegionId { get; set; } = -1;

    [BindProperty]
    public string CallerPhoneSearch { get; set; } = "";

    public List<LookupItem> Categories { get; set; } = new();
    public List<LookupItem> Types { get; set; } = new();
    public List<LookupItem> Regions { get; set; } = new();
    public List<IncidentRow> Incidents { get; set; } = new();

    public string ErrorMessage { get; set; } = "";

    public class LookupItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    public class IncidentRow
    {
        public long IncidentID { get; set; }
        public string CallerPhone { get; set; } = "";
        public string IncidentTypeName { get; set; } = "";
        public string ReferenceNo { get; set; } = "";
        public string CaptureDate { get; set; } = "";
        public string CapturedBy { get; set; } = "";
        public string RegionName { get; set; } = "";
        public string Status { get; set; } = "";
    }

    public async Task OnGetAsync()
    {
        DateFrom = DateTime.Today.ToString("dd/MM/yyyy");
        DateTo = DateTime.Today.ToString("dd/MM/yyyy");

        await LoadLookupsAsync();
        await LoadRecentIncidentsAsync();
    }

    public async Task<IActionResult> OnPostSearchAsync()
    {
        await LoadLookupsAsync();

        if (!DateTime.TryParseExact(
                DateFrom, "dd/MM/yyyy",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var from) ||
            !DateTime.TryParseExact(
                DateTo, "dd/MM/yyyy",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var to))
        {
            ErrorMessage = "Enter valid dates in dd/MM/yyyy format.";
            return Page();
        }

        if (to.Date < from.Date)
        {
            ErrorMessage = "The To Date cannot be earlier than the From Date.";
            return Page();
        }

        await SearchIncidentsAsync(from.Date, to.Date.AddDays(1));

        return Page();
    }

    public async Task<IActionResult> OnPostSearchByIncidentNoAsync()
    {
        await LoadLookupsAsync();

        if (string.IsNullOrWhiteSpace(CallerPhoneSearch))
        {
            ErrorMessage = "Enter an Caller number to search.";
            return Page();
        }

        await SearchByCallerPhoneAsync(CallerPhoneSearch.Trim());

        if (Incidents.Count == 0)
            ErrorMessage = "No incident was found with that Caller Number.";

        return Page();
    }

    public async Task<IActionResult> OnPostCategoryChangedAsync()
    {
        TypeId = -1;
        await LoadLookupsAsync();

        return Page();
    }

    private async Task LoadLookupsAsync()
    {
        Categories.Clear();
        Types.Clear();
        Regions.Clear();

        await using var connection = new SqlConnection(
            _configuration.GetConnectionString("RepsDatabase"));

        await connection.OpenAsync();

        await using (var command = new SqlCommand(@"
            SELECT IncidentCategoryID, IncidentCategoryName
            FROM dbo.IncidentCategory
            ORDER BY IncidentCategoryName;", connection))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                Categories.Add(new LookupItem
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1)
                });
            }
        }

        await using (var command = new SqlCommand(@"
            SELECT RegionID, RegionName
            FROM dbo.Region
            ORDER BY RegionName;", connection))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                Regions.Add(new LookupItem
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1)
                });
            }
        }

        await using (var command = new SqlCommand(@"
            SELECT IncidentTypeID, IncidentTypeName
            FROM dbo.IncidentType
            WHERE (@CategoryId <= 0
                   OR IncidentCategoryID = @CategoryId)
            ORDER BY IncidentTypeName;", connection))
        {
            command.Parameters.AddWithValue("@CategoryId", CategoryId);

            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                Types.Add(new LookupItem
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1)
                });
            }
        }
    }

    private async Task LoadRecentIncidentsAsync()
    {
        await LoadIncidentsAsync(null, null, true);
    }

    private async Task SearchIncidentsAsync(
        DateTime from, DateTime toExclusive)
    {
        await LoadIncidentsAsync(from, toExclusive, false);
    }

    private async Task LoadIncidentsAsync(
        DateTime? from,
        DateTime? toExclusive,
        bool limitResults)
    {
        Incidents.Clear();

        await using var connection = new SqlConnection(
            _configuration.GetConnectionString("RepsDatabase"));

        await connection.OpenAsync();

        string limit = limitResults ? "TOP (200)" : "";

            string sql = $@"
                WITH FilteredIncidents AS
                (
                    SELECT {limit}
                        i.IncidentID,
                        i.IncidentNo,
                        i.ReferenceNo,
                        i.CaptureDate,
                        i.CapturedBy,
                        i.IncidentStatus,
                        i.RegionID
                    FROM dbo.IncidentRegister i
                    WHERE
                        (@FromDate IS NULL OR i.CaptureDate >= @FromDate)
                        AND (@ToDate IS NULL OR i.CaptureDate < @ToDate)
                        AND (@RegionId <= 0 OR i.RegionID = @RegionId)
                        AND (
                            (@CategoryId <= 0 AND @TypeId <= 0)
                            OR EXISTS (
                                SELECT 1
                                FROM dbo.IncidentTypeDetails d
                                WHERE d.IncidentID = i.IncidentID
                                  AND d.Status <> 4
                                  AND (@CategoryId <= 0
                                       OR d.IncidentCategoryID = @CategoryId)
                                  AND (@TypeId <= 0
                                       OR d.IncidentTypeID = @TypeId)
                            )
                        )
                    ORDER BY i.CaptureDate DESC
                ),
                DistinctIncidentTypes AS
                (
                    SELECT DISTINCT
                        d.IncidentID,
                        t.IncidentTypeName
                    FROM FilteredIncidents fi
                    INNER JOIN dbo.IncidentTypeDetails d
                        ON d.IncidentID = fi.IncidentID
                    INNER JOIN dbo.IncidentType t
                        ON t.IncidentTypeID = d.IncidentTypeID
                    WHERE d.Status <> 4
                ),
                IncidentTypes AS
                (
                    SELECT
                        IncidentID,
                        STRING_AGG(IncidentTypeName, ', ') AS IncidentTypeName
                    FROM DistinctIncidentTypes
                    GROUP BY IncidentID
                )
                SELECT
                    fi.IncidentID,
                    fi.IncidentNo,
                    fi.ReferenceNo,
                    fi.CaptureDate,
                    fi.CapturedBy,
                    fi.IncidentStatus,
                    ISNULL(r.RegionName, '') AS RegionName,
                    ISNULL(it.IncidentTypeName, '') AS IncidentTypeName
                FROM FilteredIncidents fi
                LEFT JOIN dbo.Region r
                    ON r.RegionID = fi.RegionID
                LEFT JOIN IncidentTypes it
                    ON it.IncidentID = fi.IncidentID
                ORDER BY fi.CaptureDate DESC
                OPTION (RECOMPILE);";

        await using var command = new SqlCommand(sql, connection);

        command.Parameters.Add("@FromDate", System.Data.SqlDbType.DateTime)
            .Value = from.HasValue ? from.Value : DBNull.Value;

        command.Parameters.Add("@ToDate", System.Data.SqlDbType.DateTime)
            .Value = toExclusive.HasValue ? toExclusive.Value : DBNull.Value;

        command.Parameters.AddWithValue("@CategoryId", CategoryId);
        command.Parameters.AddWithValue("@TypeId", TypeId);
        command.Parameters.AddWithValue("@RegionId", RegionId);

        command.CommandTimeout = 120;

        await using var reader = await command.ExecuteReaderAsync();
        command.CommandTimeout = 120;

        while (await reader.ReadAsync())
        {
            Incidents.Add(ReadIncident(reader));
        }
    }

    private async Task SearchByCallerPhoneAsync(string CallerPhone)
    {
        Incidents.Clear();

        await using var connection = new SqlConnection(
            _configuration.GetConnectionString("RepsDatabase"));

        await connection.OpenAsync();

        const string sql = @"
            SELECT TOP (1)
                i.IncidentID,
                i.CallerPhone,
                i.ReferenceNo,
                i.CaptureDate,
                i.CapturedBy,
                i.IncidentStatus,
                ISNULL(r.RegionName, '') AS RegionName,
                ISNULL((
                    SELECT STRING_AGG(x.IncidentTypeName, ', ')
                    FROM (
                        SELECT DISTINCT t.IncidentTypeName
                        FROM dbo.IncidentTypeDetails d
                        INNER JOIN dbo.IncidentType t
                            ON t.IncidentTypeID = d.IncidentTypeID
                        WHERE d.IncidentID = i.IncidentID
                          AND d.Status <> 4
                    ) x
                ), '') AS IncidentTypeName
            FROM dbo.IncidentRegister i
            LEFT JOIN dbo.Region r
                ON r.RegionID = i.RegionID
            WHERE i.CallerPhone = @CallerPhone
            ORDER BY i.CaptureDate DESC;";

        await using var command = new SqlCommand(sql, connection);

        command.Parameters.AddWithValue("@CallerPhone", CallerPhone);
        command.CommandTimeout = 120;

        await using var reader = await command.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            Incidents.Add(ReadIncident(reader));
        }
    }

    private static IncidentRow ReadIncident(SqlDataReader reader)
    {
        return new IncidentRow
        {
            IncidentID = reader.GetInt64(0),
           CallerPhone = reader.IsDBNull(1) ? "" : reader.GetString(1),
            ReferenceNo = reader.IsDBNull(2) ? "" : reader.GetString(2),
            CaptureDate = reader.GetDateTime(3).ToString("dd/MM/yyyy"),
            CapturedBy = reader.IsDBNull(4) ? "" : reader.GetString(4),
            Status = reader.GetByte(5) == 0 ? "Open" : "Closed",
            RegionName = reader.GetString(6),
            IncidentTypeName = reader.GetString(7)
        };
    }
}
