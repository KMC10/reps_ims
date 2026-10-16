
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace REPS_IMS.Pages.Forms;

public class IncidentDetailsModel : PageModel
{
    private readonly IConfiguration _configuration;

    public IncidentDetailsModel(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public IncidentInfo? Incident { get; set; }
    public string ErrorMessage { get; set; } = "";
    public bool ShowResolveButton => Incident?.IncidentStatus == 0;

    public List<IncidentNoteInfo> Notes { get; set; } = new();

    public class IncidentInfo
    {
        public long IncidentID { get; set; }
        public string IncidentNo { get; set; } = "";
        public string ReferenceNo { get; set; } = "";
        public string CallAttendant { get; set; } = "";
        public string CallerPhone { get; set; } = "";
        public string CallerName { get; set; } = "";
        public string CallerPIN { get; set; } = "";
        public string CallerAddress { get; set; } = "";
        public string Region { get; set; } = "";
        public string Media { get; set; } = "";
        public string IncidentHappenning { get; set; } = "";
        public string IncidentHappend { get; set; } = "";
        public string AffectedPerson { get; set; } = "";
        public string Relationship { get; set; } = "";
        public string IncidentAddress { get; set; } = "";
        public string ReferencePoint { get; set; } = "";
        public string SuspectPersons { get; set; } = "";
        public string Accent { get; set; } = "";
        public string Priority { get; set; } = "";
        public bool HaveWeapons { get; set; }
        public string PhysicalAppearance { get; set; } = "";
        public string DressAppearance { get; set; } = "";
        public string LeavingDirection { get; set; } = "";
        public string WhenHappened { get; set; } = "";
        public string AdditionalDescription { get; set; } = "";
        public string ResultCode { get; set; } = "";
        public string DelayReason { get; set; } = "";
        public string CallDispatchTime { get; set; } = "";
        public string UnitsAssigned { get; set; } = "";
        public string OfficerAssigned { get; set; } = "";
        public string UnitInformedOn { get; set; } = "";
        public string UnitArrivedOn { get; set; } = "";
        public string ResolvedTime { get; set; } = "";
        public int IncidentStatus { get; set; }
        public List<string> IncidentTypes { get; set; } = new();
    }

    public class IncidentNoteInfo
    {
        public string NoteDescription { get; set; } = "";
        public DateTime? CaptureDate { get; set; }
        public string CaptureBy { get; set; } = "";
    }

    public async Task OnGetAsync(long iid)
    {
        await LoadIncidentAsync(iid);
    }

    public async Task<IActionResult> OnPostMarkAsResolvedAsync(long iid)
    {
        if (iid <= 0)
        {
            ErrorMessage = "No valid incident was specified.";
            return Page();
        }

        await using var connection = new SqlConnection(
            _configuration.GetConnectionString("RepsDatabase"));

        await connection.OpenAsync();

        const string sql = @"
            UPDATE dbo.IncidentRegister
            SET IncidentStatus = 1
            WHERE IncidentID = @IncidentID
              AND IncidentStatus = 0;";

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@IncidentID", iid);

        int affected = await command.ExecuteNonQueryAsync();

        if (affected == 0)
        {
            await LoadIncidentAsync(iid);

            if (Incident == null)
                ErrorMessage = "No incident information found.";
            else if (Incident.IncidentStatus != 0)
                ErrorMessage = "This incident has already been resolved.";
            else
                ErrorMessage = "Failed to update incident information.";

            return Page();
        }

        return RedirectToPage("/Forms/ResolveIncident", new { iid });
    }

    private async Task LoadIncidentAsync(long iid)
    {
        Incident = null;
        Notes.Clear();

        if (iid <= 0)
        {
            ErrorMessage = "No incident information found.";
            return;
        }

        await using var connection = new SqlConnection(
            _configuration.GetConnectionString("RepsDatabase"));

        await connection.OpenAsync();

        const string sql = @"
            SELECT
                i.IncidentID,
                i.IncidentNo,
                i.ReferenceNo,
                i.CapturedBy,
                i.CallerPhone,
                i.CallerFullName,
                i.CallerPIN,
                i.CallerAddress,
                ISNULL(r.RegionName, '') AS RegionName,
                i.CallType,
                i.IncidentHappenning,
                i.IncidentHappend,
                i.AffectedPerson,
                i.Relationship,
                i.IncidentAddress,
                i.ReferencePoint,
                i.SuspectPersons,
                i.Accent,
                ISNULL(p.IncidentPriorityName, '') AS PriorityName,
                i.HaveWeapons,
                i.PhysicalAppearance,
                i.DressAppearance,
                i.LeavingDirection,
                i.WhenHappend,
                i.AdditionalDescription,
                CASE WHEN rc.ResultCodeID IS NULL THEN ''
                     ELSE CONCAT(rc.ResultCodeID, ' - ',
                                 rc.ResultCodeDescription) END AS ResultCode,
                CASE WHEN dr.DelayReasonID IS NULL THEN ''
                     ELSE CONCAT(dr.DelayReasonID, ' - ',
                                 dr.DelayReasonDescription) END AS DelayReason,
                i.CallDispatchTime,
                i.UnitsAssigned,
                i.OfficerAssigned,
                i.UnitInformedOn,
                i.UnitArrivedOn,
                i.ResolvedTime,
                i.IncidentStatus
            FROM dbo.IncidentRegister i
            LEFT JOIN dbo.Region r
                ON r.RegionID = i.RegionID
            LEFT JOIN dbo.IncidentPriority p
                ON p.IncidentPriorityID = i.IncidentPriorityID
            LEFT JOIN dbo.ResultCode rc
                ON rc.ResultCodeID = i.ResultCodeID
            LEFT JOIN dbo.DelayReason dr
                ON dr.DelayReasonID = i.DelayReasonID
            WHERE i.IncidentID = @IncidentID;";

        await using (var command = new SqlCommand(sql, connection))
        {
            command.Parameters.AddWithValue("@IncidentID", iid);
            command.CommandTimeout = 60;

            await using var reader = await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                ErrorMessage = "No incident information found.";
                return;
            }

            Incident = new IncidentInfo
            {
                IncidentID = Convert.ToInt64(reader["IncidentID"]),
                IncidentNo = Text(reader, "IncidentNo"),
                ReferenceNo = Text(reader, "ReferenceNo"),
                CallAttendant = Text(reader, "CapturedBy"),
                CallerPhone = Text(reader, "CallerPhone"),
                CallerName = Text(reader, "CallerFullName"),
                CallerPIN = Text(reader, "CallerPIN"),
                CallerAddress = Text(reader, "CallerAddress"),
                Region = Text(reader, "RegionName"),
                Media = MediaName(reader["CallType"]),
                IncidentHappenning = Text(reader, "IncidentHappenning"),
                IncidentHappend = Text(reader, "IncidentHappend"),
                AffectedPerson = Text(reader, "AffectedPerson"),
                Relationship = Text(reader, "Relationship"),
                IncidentAddress = Text(reader, "IncidentAddress"),
                ReferencePoint = Text(reader, "ReferencePoint"),
                SuspectPersons = Text(reader, "SuspectPersons"),
                Accent = Text(reader, "Accent"),
                Priority = Text(reader, "PriorityName"),
                HaveWeapons = reader["HaveWeapons"] != DBNull.Value
                    && Convert.ToBoolean(reader["HaveWeapons"]),
                PhysicalAppearance = Text(reader, "PhysicalAppearance"),
                DressAppearance = Text(reader, "DressAppearance"),
                LeavingDirection = Text(reader, "LeavingDirection"),
                WhenHappened = Text(reader, "WhenHappend"),
                AdditionalDescription = Text(reader, "AdditionalDescription"),
                ResultCode = Text(reader, "ResultCode"),
                DelayReason = Text(reader, "DelayReason"),
                CallDispatchTime = DateText(reader["CallDispatchTime"]),
                UnitsAssigned = Text(reader, "UnitsAssigned"),
                OfficerAssigned = Text(reader, "OfficerAssigned"),
                UnitInformedOn = DateText(reader["UnitInformedOn"]),
                UnitArrivedOn = DateText(reader["UnitArrivedOn"]),
                ResolvedTime = DateText(reader["ResolvedTime"]),
                IncidentStatus = Convert.ToInt32(reader["IncidentStatus"])
            };
        }

        const string typeSql = @"
            SELECT DISTINCT t.IncidentTypeName
            FROM dbo.IncidentTypeDetails d
            INNER JOIN dbo.IncidentType t
                ON t.IncidentTypeID = d.IncidentTypeID
            WHERE d.IncidentID = @IncidentID
            ORDER BY t.IncidentTypeName;";

        await using (var command = new SqlCommand(typeSql, connection))
        {
            command.Parameters.AddWithValue("@IncidentID", iid);

            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                Incident!.IncidentTypes.Add(reader.GetString(0));
            }
        }

        // Load the notes after the incident and its types.
        await LoadNotesAsync(iid);
    }

    private async Task LoadNotesAsync(long incidentID)
    {
        Notes.Clear();

        const string sql = @"
            SELECT NoteDescription, CaptureDate, CaptureBy
            FROM dbo.IncidentNote
            WHERE IncidentID = @IncidentID
              AND Status = 0
            ORDER BY CaptureDate DESC;";

        await using var connection = new SqlConnection(
            _configuration.GetConnectionString("RepsDatabase"));

        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@IncidentID", incidentID);

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            Notes.Add(new IncidentNoteInfo
            {
                NoteDescription = Text(reader, "NoteDescription"),
                CaptureDate = reader["CaptureDate"] == DBNull.Value
                    ? null
                    : Convert.ToDateTime(reader["CaptureDate"]),
                CaptureBy = Text(reader, "CaptureBy")
            });
        }
    }

    private static string Text(SqlDataReader reader, string column)
    {
        return reader[column] == DBNull.Value
            ? ""
            : Convert.ToString(reader[column]) ?? "";
    }

    private static string DateText(object value)
    {
        return value == DBNull.Value
            ? ""
            : Convert.ToDateTime(value).ToString("dd/MM/yyyy HH:mm");
    }

    private static string MediaName(object value)
    {
        if (value == DBNull.Value)
            return "";

        return Convert.ToInt32(value) switch
        {
            1 => "Voice",
            2 => "Chat",
            3 => "Email",
            4 => "Facebook",
            5 => "Twitter",
            _ => ""
        };
    }
}
