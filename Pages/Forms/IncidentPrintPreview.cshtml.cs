
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace REPS_IMS.Pages.Forms;

public class IncidentPrintPreviewModel : PageModel
{
    private readonly IConfiguration _configuration;

    public IncidentPrintPreviewModel(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public Dictionary<string, string> Fields { get; private set; } = new();
    public string ErrorMessage { get; private set; } = "";

    public async Task OnGetAsync(long iid)
    {
        if (iid <= 0)
        {
            ErrorMessage = "No valid incident was selected.";
            return;
        }

        var connectionString =
            _configuration.GetConnectionString("RepsDatabase");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            ErrorMessage = "Database connection is not configured.";
            return;
        }

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
                i.ResolvedTime
            FROM dbo.IncidentRegister i
            LEFT JOIN dbo.Region r ON r.RegionID = i.RegionID
            LEFT JOIN dbo.IncidentPriority p
                ON p.IncidentPriorityID = i.IncidentPriorityID
            LEFT JOIN dbo.ResultCode rc ON rc.ResultCodeID = i.ResultCodeID
            LEFT JOIN dbo.DelayReason dr ON dr.DelayReasonID = i.DelayReasonID
            WHERE i.IncidentID = @IncidentID;";

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@IncidentID", iid);

            await using var reader = await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                ErrorMessage = "No incident information was found.";
                return;
            }

            string Value(string column)
            {
                if (reader[column] == DBNull.Value)
                    return "";

                var value = reader[column];

                if (value is DateTime date)
                    return date.ToString("dd/MM/yyyy HH:mm");

                if (column == "CallType")
                {
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

                if (column == "HaveWeapons")
                    return Convert.ToBoolean(value) ? "Yes" : "No";

                return Convert.ToString(value) ?? "";
            }

            var labels = new (string Label, string Column)[]
            {
                ("Incident ID", "IncidentID"),
                ("Incident No.", "IncidentNo"),
                ("Reference No.", "ReferenceNo"),
                ("Registration Date", "IncidentID"),
                ("Call Attendant", "CapturedBy"),
                ("Caller Phone", "CallerPhone"),
                ("Caller Name", "CallerFullName"),
                ("Caller PIN", "CallerPIN"),
                ("Caller Address", "CallerAddress"),
                ("Region", "RegionName"),
                ("Media", "CallType"),
                ("Incident Address", "IncidentAddress"),
                ("Reference Point", "ReferencePoint"),
                ("Affected Person", "AffectedPerson"),
                ("Relationship with Caller", "Relationship"),
                ("What is happening", "IncidentHappenning"),
                ("What has happened", "IncidentHappend"),
                ("Incident Priority", "PriorityName"),
                ("Suspect Persons", "SuspectPersons"),
                ("Accent", "Accent"),
                ("Weapons", "HaveWeapons"),
                ("Physical Appearance", "PhysicalAppearance"),
                ("How they are dressed", "DressAppearance"),
                ("Direction of Travel", "LeavingDirection"),
                ("When it happened", "WhenHappend"),
                ("Additional Description", "AdditionalDescription"),
                ("Result Code", "ResultCode"),
                ("Delay Reason", "DelayReason"),
                ("Call Dispatch Time", "CallDispatchTime"),
                ("Units Assigned", "UnitsAssigned"),
                ("Officer Assigned", "OfficerAssigned"),
                ("Unit Informed On", "UnitInformedOn"),
                ("Unit Arrived On", "UnitArrivedOn"),
                ("Resolved Time", "ResolvedTime")
            };

            foreach (var field in labels)
            {
                Fields[field.Label] = field.Column == "IncidentID"
                    && field.Label == "Registration Date"
                        ? ""
                        : Value(field.Column);
            }
        }
        catch
        {
            ErrorMessage = "Unable to load incident information for printing.";
        }
    }
}