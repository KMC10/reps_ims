
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace REPS_IMS.Pages.Forms;

public class IncidentReportModel : PageModel
{
    private readonly IConfiguration _configuration;

    public IncidentReportModel(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [BindProperty]
    public string DateFrom { get; set; } = "";

    [BindProperty]
    public string DateTo { get; set; } = "";

    [BindProperty]
    public string AgentID { get; set; } = "";

    [BindProperty]
    public int CategoryId { get; set; } = -1;

    [BindProperty]
    public int TypeId { get; set; } = -1;

    [BindProperty]
    public int RegionId { get; set; } = -1;

    [BindProperty]
    public int IncidentStatus { get; set; } = -1;

    [BindProperty]
    public string CallerPIN { get; set; } = "";

    [BindProperty]
    public string IncidentNo { get; set; } = "";

    [BindProperty]
    public string PhoneNo { get; set; } = "";

    public List<LookupItem> Categories { get; set; } = new();
    public List<LookupItem> Types { get; set; } = new();
    public List<LookupItem> Regions { get; set; } = new();
    public List<LookupItem> Statuses { get; set; } = new();

    public string ErrorMessage { get; set; } = "";
    public string SuccessMessage { get; set; } = "";


    public List<ReportRow> ReportRows { get; set; } = new();

    public class ReportRow
    {
        public string IncidentNo { get; set; } = "";
        public string AgentID { get; set; } = "";
        public string ReceivedDate { get; set; } = "";
        public string IncidentType { get; set; } = "";
        public string PoliceStation { get; set; } = "";
        public string ReceivedBy { get; set; } = "";
        public string DispatchTime { get; set; } = "";
        public string UnitAssigned { get; set; } = "";
        public string UnitInformedOn { get; set; } = "";
        public string ArrivalTime { get; set; } = "";
        public string Duration { get; set; } = "";
        public string DelayCode { get; set; } = "";
        public string ResultCode { get; set; } = "";
        public string ResolveTime { get; set; } = "";
    }


    public class LookupItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    public async Task OnGetAsync()
    {
        DateFrom = DateTime.Today.ToString("dd/MM/yyyy 00:00");
        DateTo = DateTime.Now.ToString("dd/MM/yyyy HH:mm");

        await LoadLookupsAsync();
    }


    public async Task<IActionResult> OnPostSearchAsync()
    {
        await LoadLookupsAsync();
        ReportRows.Clear();
        ErrorMessage = "";

        if (!DateTime.TryParseExact(
                DateFrom, "dd/MM/yyyy HH:mm",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var from) ||
            !DateTime.TryParseExact(
                DateTo, "dd/MM/yyyy HH:mm",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var to))
        {
            ErrorMessage = "Enter valid dates in dd/MM/yyyy HH:mm format.";
            return Page();
        }

        if (to < from)
        {
            ErrorMessage = "The To Date cannot be earlier than the From Date.";
            return Page();
        }

        await using var connection = new SqlConnection(
            _configuration.GetConnectionString("RepsDatabase"));

        await connection.OpenAsync();

        const string sql = @"
        SELECT
            i.IncidentNo,
            i.CaptureDate,
            i.ReferencePoint,
            i.ReferenceNo,
            i.CallDispatchTime,
            i.UnitsAssigned,
            i.UnitInformedOn,
            i.UnitArrivedOn,
            i.ResolvedTime,
            i.DelayReasonID,
            i.ResultCodeID,
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
        WHERE i.CaptureDate >= @FromDate
          AND i.CaptureDate <= @ToDate
          AND (@RegionId <= 0 OR i.RegionID = @RegionId)
          AND (@Status < 0 OR i.IncidentStatus = @Status)
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
        ORDER BY i.CaptureDate DESC;";

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@FromDate", System.Data.SqlDbType.DateTime).Value = from;
        command.Parameters.Add("@ToDate", System.Data.SqlDbType.DateTime).Value = to;
        command.Parameters.AddWithValue("@RegionId", RegionId);
        command.Parameters.AddWithValue("@Status", IncidentStatus);
        command.Parameters.AddWithValue("@CategoryId", CategoryId);
        command.Parameters.AddWithValue("@TypeId", TypeId);
        command.CommandTimeout = 120;

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            DateTime? dispatch = reader["CallDispatchTime"] == DBNull.Value
                ? null : Convert.ToDateTime(reader["CallDispatchTime"]);

            DateTime? arrival = reader["UnitArrivedOn"] == DBNull.Value
                ? null : Convert.ToDateTime(reader["UnitArrivedOn"]);

            DateTime? resolved = reader["ResolvedTime"] == DBNull.Value
                ? null : Convert.ToDateTime(reader["ResolvedTime"]);

            string FormatDate(object value) =>
                value == DBNull.Value ? "" :
                Convert.ToDateTime(value).ToString("dd/MM/yyyy HH:mm");

            string duration = dispatch.HasValue && arrival.HasValue
                ? (arrival.Value - dispatch.Value).ToString(@"hh\:mm\:ss")
                : "";

            ReportRows.Add(new ReportRow
            {
                IncidentNo = reader["IncidentNo"]?.ToString() ?? "",
                ReceivedDate = FormatDate(reader["CaptureDate"]),
                IncidentType = reader["IncidentTypeName"]?.ToString() ?? "",
                PoliceStation = reader["ReferencePoint"]?.ToString() ?? "",
                ReceivedBy = reader["ReferenceNo"]?.ToString() ?? "",
                DispatchTime = FormatDate(reader["CallDispatchTime"]),
                UnitAssigned = reader["UnitsAssigned"]?.ToString() ?? "",
                UnitInformedOn = FormatDate(reader["UnitInformedOn"]),
                ArrivalTime = FormatDate(reader["UnitArrivedOn"]),
                Duration = duration,
                DelayCode = reader["DelayReasonID"] == DBNull.Value
                    ? "" : reader["DelayReasonID"].ToString()!,
                ResultCode = reader["ResultCodeID"] == DBNull.Value
                    ? "" : reader["ResultCodeID"].ToString()!,
                ResolveTime = FormatDate(reader["ResolvedTime"])
            });
        }

        return Page();
    }


    public async Task<IActionResult> OnPostCategoryChangedAsync()
    {
        await LoadLookupsAsync();
        return Page();
    }

    private async Task LoadLookupsAsync()
    {
        Categories.Clear();
        Types.Clear();
        Regions.Clear();
        Statuses.Clear();

        await using var connection = new SqlConnection(
            _configuration.GetConnectionString("RepsDatabase"));

        await connection.OpenAsync();

        Categories.Add(new LookupItem { Id = -1, Name = "Select" });

        const string categorySql = @"
            SELECT IncidentCategoryID, IncidentCategoryName
            FROM dbo.IncidentCategory
            ORDER BY IncidentCategoryName;";

        await using (var command = new SqlCommand(categorySql, connection))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                Categories.Add(new LookupItem
                {
                    Id = reader.GetInt32(0),
                    Name = reader.IsDBNull(1) ? "" : reader.GetString(1)
                });
            }
        }

        Regions.Add(new LookupItem { Id = -1, Name = "Select" });

        const string regionSql = @"
            SELECT RegionID, RegionName
            FROM dbo.Region
            ORDER BY RegionName;";

        await using (var command = new SqlCommand(regionSql, connection))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                Regions.Add(new LookupItem
                {
                    Id = reader.GetInt32(0),
                    Name = reader.IsDBNull(1) ? "" : reader.GetString(1)
                });
            }
        }

        Types.Add(new LookupItem { Id = -1, Name = "Select" });

        const string typeSql = @"
            SELECT IncidentTypeID, IncidentTypeName
            FROM dbo.IncidentType
            WHERE (@CategoryId <= 0
                   OR IncidentCategoryID = @CategoryId)
            ORDER BY IncidentTypeName;";

        await using (var command = new SqlCommand(typeSql, connection))
        {
            command.Parameters.AddWithValue("@CategoryId", CategoryId);

            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                Types.Add(new LookupItem
                {
                    Id = reader.GetInt32(0),
                    Name = reader.IsDBNull(1) ? "" : reader.GetString(1)
                });
            }
        }

        Statuses.Add(new LookupItem { Id = -1, Name = "ALL" });

        // Keep the original status labels; verify their numeric values
        // against the original IncidentStatus enum before relying on them.
        Statuses.Add(new LookupItem { Id = 0, Name = "Open" });
        Statuses.Add(new LookupItem { Id = 1, Name = "Closed" });
    }
}

