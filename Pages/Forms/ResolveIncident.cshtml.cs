
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;
using System.Data;

namespace REPS_IMS.Pages.Forms;

public class ResolveIncidentModel : PageModel
{
    private readonly IConfiguration _configuration;

    public ResolveIncidentModel(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [BindProperty(SupportsGet = true)]
    public long iid { get; set; }

    public IncidentInfo? Incident { get; set; }

    public List<SelectListItem> ResultCodes { get; set; } = new();
    public List<SelectListItem> DelayReasons { get; set; } = new();

    [BindProperty]
    public ResolutionForm Input { get; set; } = new();

    public string ErrorMessage { get; set; } = "";
    public bool ShowResultModal { get; set; }

    public bool ResolutionSucceeded { get; set; }

    public class IncidentInfo
    {
        public long IncidentID { get; set; }
        public string IncidentNo { get; set; } = "";
        public string ReferenceNo { get; set; } = "";
        public string CallerName { get; set; } = "";
        public string CallerPhone { get; set; } = "";
        public string CallerAddress { get; set; } = "";
        public string Region { get; set; } = "";
        public string IncidentAddress { get; set; } = "";
        public string IncidentHappenning { get; set; } = "";
        public string IncidentHappend { get; set; } = "";
        public string AffectedPerson { get; set; } = "";
        public string Relationship { get; set; } = "";
        public string SuspectPersons { get; set; } = "";
        public string IncidentTypes { get; set; } = "";
        public string Priority { get; set; } = "";
        public string AdditionalDescription { get; set; } = "";
        public int IncidentStatus { get; set; }
    }

    public class ResolutionForm
    {
        public string? ResultCode { get; set; }
        public string? DelayReason { get; set; }
        public DateTime? CallDispatchTime { get; set; }
        public string? UnitsAssigned { get; set; }
        public string? OfficerAssigned { get; set; }
        public DateTime? UnitInformedOn { get; set; }
        public DateTime? UnitArrivedOn { get; set; }
        public DateTime? ResolvedTime { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (iid <= 0)
        {
            ErrorMessage = "No valid incident was specified.";
            return Page();
        }

        await using var connection = new SqlConnection(
            _configuration.GetConnectionString("RepsDatabase"));

        await connection.OpenAsync();

        await LoadOptionsAsync(connection);
        await LoadIncidentAsync(connection, iid);

        if (Incident != null)
        {
            Input.CallDispatchTime = ParseDate(IncidentDispatchTime);
        }

        return Page();
    }

    // Used to prefill the dispatch time, if it already exists.
    private string IncidentDispatchTime { get; set; } = "";

    public async Task<IActionResult> OnPostAsync()
    {
        if (iid <= 0)
        {
            ErrorMessage = "No valid incident was specified.";
            return await ReturnPageAsync();
        }

        if (!int.TryParse(Input.ResultCode, out int resultCodeId))
        {
            ModelState.AddModelError("Input.ResultCode",
                "Please select a result code.");
        }

        if (!int.TryParse(Input.DelayReason, out int delayReasonId))
        {
            ModelState.AddModelError("Input.DelayReason",
                "Please select a delay reason.");
        }

        if (!Input.ResolvedTime.HasValue)
        {
            ModelState.AddModelError("Input.ResolvedTime",
                "Please enter the resolved time.");
        }

        if (!ModelState.IsValid)
        {
            return await ReturnPageAsync();
        }

        var username = User.Identity?.Name;

        if (string.IsNullOrWhiteSpace(username))
        {
            ErrorMessage = "Please log in again before saving.";
            return await ReturnPageAsync();
        }

        await using var connection = new SqlConnection(
            _configuration.GetConnectionString("RepsDatabase"));

        await connection.OpenAsync();

        await using var transaction =
            (SqlTransaction)await connection.BeginTransactionAsync();

        try
        {
            const string sql = @"
                UPDATE dbo.IncidentRegister
                SET
                    ResultCodeID = @ResultCodeID,
                    DelayReasonID = @DelayReasonID,
                    CallDispatchTime = @CallDispatchTime,
                    UnitsAssigned = @UnitsAssigned,
                    OfficerAssigned = @OfficerAssigned,
                    UnitInformedOn = @UnitInformedOn,
                    UnitArrivedOn = @UnitArrivedOn,
                    ResolvedTime = @ResolvedTime,
                    IncidentStatus = 1,
                    UpdateBy = @UpdateBy,
                    UpdateDate = GETDATE(),
                    UpdateUserName = @UpdateUserName
                WHERE IncidentID = @IncidentID
                  AND IncidentStatus = 0;";

            await using (var command =
                new SqlCommand(sql, connection, transaction))
            {
                command.Parameters.Add("@IncidentID", SqlDbType.BigInt)
                    .Value = iid;

                command.Parameters.Add("@ResultCodeID", SqlDbType.Int)
                    .Value = resultCodeId;

                command.Parameters.Add("@DelayReasonID", SqlDbType.Int)
                    .Value = delayReasonId;

                AddDateTime(command, "@CallDispatchTime",
                    Input.CallDispatchTime);

                command.Parameters.Add("@UnitsAssigned", SqlDbType.NVarChar, 255)
                    .Value = (object?)Input.UnitsAssigned ?? DBNull.Value;

                command.Parameters.Add("@OfficerAssigned", SqlDbType.NVarChar, 255)
                    .Value = (object?)Input.OfficerAssigned ?? DBNull.Value;

                AddDateTime(command, "@UnitInformedOn",
                    Input.UnitInformedOn);

                AddDateTime(command, "@UnitArrivedOn",
                    Input.UnitArrivedOn);

                AddDateTime(command, "@ResolvedTime",
                    Input.ResolvedTime);

                command.Parameters.Add("@UpdateBy", SqlDbType.NVarChar, 255)
                    .Value = username;

                command.Parameters.Add("@UpdateUserName", SqlDbType.NVarChar, 255)
                    .Value = username;

                int affected = await command.ExecuteNonQueryAsync();

                if (affected == 0)
                {
                    await transaction.RollbackAsync();

                    ErrorMessage =
                        "This incident could not be resolved. It may already have been resolved.";

                    return await ReturnPageAsync();
                }
            }

            const string historySql = @"
                INSERT INTO dbo.IncidentHistory
                    (IncidentID, CapturedBy, CaptureDate, CaptureExtension,
                     CaptureUserName, Description, Status)
                VALUES
                    (@IncidentID, @CapturedBy, GETDATE(), '',
                     @CaptureUserName, @Description, 1);";

            await using (var historyCommand =
                new SqlCommand(historySql, connection, transaction))
            {
                historyCommand.Parameters.Add("@IncidentID", SqlDbType.BigInt)
                    .Value = iid;

                historyCommand.Parameters.Add("@CapturedBy", SqlDbType.NVarChar, 255)
                    .Value = username;

                historyCommand.Parameters.Add("@CaptureUserName", SqlDbType.NVarChar, 255)
                    .Value = username;

                historyCommand.Parameters.Add("@Description", SqlDbType.NVarChar, -1)
                    .Value = "Incident resolved.";

                await historyCommand.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();

            TempData["SaveMessage"] = "Incident resolved successfully.";

            return RedirectToPage("/Forms/IncidentDetails", new { iid });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();

            ErrorMessage = $"Failed to resolve incident: {ex.Message}";
            return await ReturnPageAsync();
        }
    }

    private async Task<IActionResult> ReturnPageAsync()
    {
        await using var connection = new SqlConnection(
            _configuration.GetConnectionString("RepsDatabase"));

        await connection.OpenAsync();

        await LoadOptionsAsync(connection);
        await LoadIncidentAsync(connection, iid);

        return Page();
    }

    private async Task LoadOptionsAsync(SqlConnection connection)
    {
        ResultCodes = await LoadOptions(
            connection,
            "SELECT ResultCodeID, ResultCodeDescription FROM dbo.ResultCode ORDER BY ResultCodeID",
            "ResultCodeID",
            "ResultCodeDescription",
            "Select result code");

        DelayReasons = await LoadOptions(
            connection,
            "SELECT DelayReasonID, DelayReasonDescription FROM dbo.DelayReason ORDER BY DelayReasonID",
            "DelayReasonID",
            "DelayReasonDescription",
            "Select delay reason");
    }

    private async Task LoadIncidentAsync(
        SqlConnection connection,
        long incidentId)
    {
        const string sql = @"
            SELECT
                i.IncidentID,
                i.IncidentNo,
                i.ReferenceNo,
                i.CallerFullName,
                i.CallerPhone,
                i.CallerAddress,
                ISNULL(r.RegionName, '') AS RegionName,
                i.IncidentAddress,
                i.IncidentHappenning,
                i.IncidentHappend,
                i.AffectedPerson,
                i.Relationship,
                i.SuspectPersons,
                ISNULL(p.IncidentPriorityName, '') AS PriorityName,
                i.AdditionalDescription,
                i.IncidentStatus,
                i.CallDispatchTime,
                i.ResultCodeID,
                i.DelayReasonID,
                i.UnitsAssigned,
                i.OfficerAssigned,
                i.UnitInformedOn,
                i.UnitArrivedOn,
                i.ResolvedTime
            FROM dbo.IncidentRegister i
            LEFT JOIN dbo.Region r ON r.RegionID = i.RegionID
            LEFT JOIN dbo.IncidentPriority p
                ON p.IncidentPriorityID = i.IncidentPriorityID
            WHERE i.IncidentID = @IncidentID;";

        await using (var command = new SqlCommand(sql, connection))
        {
            command.Parameters.Add("@IncidentID", SqlDbType.BigInt)
                .Value = incidentId;

            await using var reader = await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                Incident = null;
                ErrorMessage = "No incident information found.";
                return;
            }

            Incident = new IncidentInfo
            {
                IncidentID = Convert.ToInt64(reader["IncidentID"]),
                IncidentNo = Text(reader, "IncidentNo"),
                ReferenceNo = Text(reader, "ReferenceNo"),
                CallerName = Text(reader, "CallerFullName"),
                CallerPhone = Text(reader, "CallerPhone"),
                CallerAddress = Text(reader, "CallerAddress"),
                Region = Text(reader, "RegionName"),
                IncidentAddress = Text(reader, "IncidentAddress"),
                IncidentHappenning = Text(reader, "IncidentHappenning"),
                IncidentHappend = Text(reader, "IncidentHappend"),
                AffectedPerson = Text(reader, "AffectedPerson"),
                Relationship = Text(reader, "Relationship"),
                SuspectPersons = Text(reader, "SuspectPersons"),
                Priority = Text(reader, "PriorityName"),
                AdditionalDescription = Text(reader, "AdditionalDescription"),
                IncidentStatus = Convert.ToInt32(reader["IncidentStatus"])
            };

            IncidentDispatchTime = DateInput(reader["CallDispatchTime"]);

            Input.ResultCode = Text(reader, "ResultCodeID");
            Input.DelayReason = Text(reader, "DelayReasonID");
            Input.UnitsAssigned = Text(reader, "UnitsAssigned");
            Input.OfficerAssigned = Text(reader, "OfficerAssigned");
            Input.UnitInformedOn = ParseDate(DateInput(reader["UnitInformedOn"]));
            Input.UnitArrivedOn = ParseDate(DateInput(reader["UnitArrivedOn"]));
            Input.ResolvedTime = ParseDate(DateInput(reader["ResolvedTime"]));
        }

        const string typesSql = @"
            SELECT DISTINCT t.IncidentTypeName
            FROM dbo.IncidentTypeDetails d
            INNER JOIN dbo.IncidentType t
                ON t.IncidentTypeID = d.IncidentTypeID
            WHERE d.IncidentID = @IncidentID
              AND d.Status <> 4
            ORDER BY t.IncidentTypeName;";

        var types = new List<string>();

        await using (var command = new SqlCommand(typesSql, connection))
        {
            command.Parameters.Add("@IncidentID", SqlDbType.BigInt)
                .Value = incidentId;

            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                types.Add(reader.GetString(0));
            }
        }

        Incident!.IncidentTypes = string.Join(", ", types);
    }

    private static async Task<List<SelectListItem>> LoadOptions(
        SqlConnection connection,
        string sql,
        string valueColumn,
        string textColumn,
        string placeholder)
    {
        var items = new List<SelectListItem>
        {
            new() { Value = "", Text = placeholder }
        };

        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            items.Add(new SelectListItem
            {
                Value = reader[valueColumn].ToString(),
                Text = reader[textColumn] == DBNull.Value
                    ? ""
                    : reader[textColumn].ToString()
            });
        }

        return items;
    }

    private static void AddDateTime(
        SqlCommand command,
        string parameterName,
        DateTime? value)
    {
        command.Parameters.Add(parameterName, SqlDbType.DateTime)
            .Value = (object?)value ?? DBNull.Value;
    }

    private static string Text(SqlDataReader reader, string column)
    {
        return reader[column] == DBNull.Value
            ? ""
            : Convert.ToString(reader[column]) ?? "";
    }

    private static string DateInput(object value)
    {
        if (value == DBNull.Value)
            return "";

        return Convert.ToDateTime(value).ToString("yyyy-MM-ddTHH:mm");
    }

    private static DateTime? ParseDate(string value)
    {
        return DateTime.TryParse(value, out var date) ? date : null;
    }
}
