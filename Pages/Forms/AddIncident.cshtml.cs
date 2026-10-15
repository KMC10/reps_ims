
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;

namespace REPS_IMS.Pages.Forms
{
    public class AddIncidentModel : PageModel
    {
        private readonly IConfiguration _configuration;

        [BindProperty(SupportsGet = true)]
        public string ReturnUrl { get; set; } = "/Forms/Dashboard";


        public AddIncidentModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [BindProperty(SupportsGet = true)]
        public long? iid { get; set; }

        public List<SelectListItem> Regions { get; set; } = new();
        public List<SelectListItem> Priorities { get; set; } = new();
        public List<SelectListItem> DelayReasons { get; set; } = new();
        public List<SelectListItem> ResultCodes { get; set; } = new();

        public Dictionary<int, List<SelectListItem>> OffenceOptions { get; set; } = new();

        public List<SelectListItem> MediaOptions { get; set; } = new()
        {
            new SelectListItem { Value = "-1", Text = "Select" },
            new SelectListItem { Value = "1", Text = "Voice" },
            new SelectListItem { Value = "2", Text = "Chat" },
            new SelectListItem { Value = "3", Text = "Email" },
            new SelectListItem { Value = "4", Text = "Facebook" },
            new SelectListItem { Value = "5", Text = "Twitter" }
        };

        [BindProperty]
        public IncidentForm Input { get; set; } = new();

        [BindProperty]
        public List<int> SelectedOffenceIds { get; set; } = new();

        public class IncidentForm
        {
            public string? CallAttendant { get; set; }
            public string? CallerCellNo { get; set; }
            public string? CallerName { get; set; }
            public string? CallerPIN { get; set; }
            public string? CallerAddress { get; set; }
            public string? Region { get; set; }
            public string? CallRefNo { get; set; }
            public string? Media { get; set; }
            public string? IncidentHappenning { get; set; }
            public string? IncidentHappend { get; set; }
            public string? AffectedPerson { get; set; }
            public string? RelationShip { get; set; }
            public string? IncidentAddress { get; set; }
            public string? ReferencePoint { get; set; }
            public string? SuspectPerson { get; set; }
            public string? Accent { get; set; }
            public string? Priority { get; set; }
            public bool HasWeapon { get; set; }
            public string? PhysicalAppearance { get; set; }
            public string? DressAppearance { get; set; }
            public string? LeavingDirection { get; set; }
            public string? IncidentTime { get; set; }
            public string? AdditionalDescription { get; set; }
            public string? IncidentNo { get; set; }
            public string? ResultCode { get; set; }
            public string? DelayReason { get; set; }
            public string? CallDispatchTime { get; set; }
            public string? UnitsAssigned { get; set; }
            public string? OfficerAssigned { get; set; }
            public string? UnitInformed { get; set; }
            public string? UnitArrived { get; set; }
            public string? ResolvedTime { get; set; }
        }

        public async Task OnGetAsync()
        {
            var connectionString =
                _configuration.GetConnectionString("RepsDatabase");

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            Regions = await LoadOptions(
                connection,
                "SELECT RegionID, RegionName FROM dbo.Region ORDER BY RegionName",
                "RegionID", "RegionName", "Select region");

            Priorities = await LoadOptions(
                connection,
                @"SELECT IncidentPriorityID, IncidentPriorityName
                  FROM dbo.IncidentPriority
                  ORDER BY IncidentPriorityID",
                "IncidentPriorityID", "IncidentPriorityName", "Select priority");

            DelayReasons = await LoadOptions(
                connection,
                @"SELECT DelayReasonID, DelayReasonDescription
                  FROM dbo.DelayReason
                  ORDER BY DelayReasonID",
                "DelayReasonID", "DelayReasonDescription", "Select delay reason");

            ResultCodes = await LoadOptions(
                connection,
                @"SELECT ResultCodeID, ResultCodeDescription
                  FROM dbo.ResultCode
                  ORDER BY ResultCodeID",
                "ResultCodeID", "ResultCodeDescription", "Select result code");

            const string offenceSql = @"
                SELECT IncidentCategoryID, IncidentTypeID, IncidentTypeName
                FROM dbo.IncidentType
                WHERE Status = 1
                ORDER BY IncidentCategoryID, IncidentTypeName;";

            await using (var command = new SqlCommand(offenceSql, connection))
            await using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    int categoryId = reader.GetInt32(0);

                    if (!OffenceOptions.ContainsKey(categoryId))
                    {
                        OffenceOptions[categoryId] = new List<SelectListItem>();
                    }

                    OffenceOptions[categoryId].Add(new SelectListItem
                    {
                        Value = reader.GetInt32(1).ToString(),
                        Text = reader.GetString(2)
                    });
                }
            }

            if (iid.HasValue && iid.Value > 0)
            {
                await LoadIncidentAsync(iid.Value, connection);
            }
        }

        private async Task LoadIncidentAsync(
            long incidentId,
            SqlConnection connection)
        {
            const string sql = @"
                SELECT
                    i.IncidentNo,
                    i.ReferenceNo,
                    i.CapturedBy,
                    i.CallerPhone,
                    i.CallerFullName,
                    i.CallerPIN,
                    i.CallerAddress,
                    i.RegionID,
                    i.CallType,
                    i.IncidentPriorityID,
                    i.IncidentHappenning,
                    i.IncidentHappend,
                    i.AffectedPerson,
                    i.Relationship,
                    i.IncidentAddress,
                    i.ReferencePoint,
                    i.SuspectPersons,
                    i.Accent,
                    i.HaveWeapons,
                    i.PhysicalAppearance,
                    i.DressAppearance,
                    i.LeavingDirection,
                    i.WhenHappend,
                    i.AdditionalDescription,
                    i.ResultCodeID,
                    i.DelayReasonID,
                    i.CallDispatchTime,
                    i.UnitsAssigned,
                    i.OfficerAssigned,
                    i.UnitInformedOn,
                    i.UnitArrivedOn,
                    i.ResolvedTime
                FROM dbo.IncidentRegister i
                WHERE i.IncidentID = @IncidentID;";

            await using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@IncidentID", incidentId);

                await using var reader = await command.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                {
                    ModelState.AddModelError(
                        string.Empty,
                        "The selected incident could not be found.");

                    return;
                }

                Input = new IncidentForm
                {
                    IncidentNo = DbText(reader, "IncidentNo"),
                    CallRefNo = DbText(reader, "ReferenceNo"),
                    CallAttendant = DbText(reader, "CapturedBy"),
                    CallerCellNo = DbText(reader, "CallerPhone"),
                    CallerName = DbText(reader, "CallerFullName"),
                    CallerPIN = DbText(reader, "CallerPIN"),
                    CallerAddress = DbText(reader, "CallerAddress"),
                    Region = DbText(reader, "RegionID"),
                    Media = DbText(reader, "CallType"),
                    Priority = DbText(reader, "IncidentPriorityID"),
                    IncidentHappenning = DbText(reader, "IncidentHappenning"),
                    IncidentHappend = DbText(reader, "IncidentHappend"),
                    AffectedPerson = DbText(reader, "AffectedPerson"),
                    RelationShip = DbText(reader, "Relationship"),
                    IncidentAddress = DbText(reader, "IncidentAddress"),
                    ReferencePoint = DbText(reader, "ReferencePoint"),
                    SuspectPerson = DbText(reader, "SuspectPersons"),
                    Accent = DbText(reader, "Accent"),
                    HasWeapon = reader["HaveWeapons"] != DBNull.Value
                                && Convert.ToBoolean(reader["HaveWeapons"]),
                    PhysicalAppearance = DbText(reader, "PhysicalAppearance"),
                    DressAppearance = DbText(reader, "DressAppearance"),
                    LeavingDirection = DbText(reader, "LeavingDirection"),
                    IncidentTime = DbDate(reader, "WhenHappend"),
                    AdditionalDescription = DbText(reader, "AdditionalDescription"),
                    ResultCode = DbText(reader, "ResultCodeID"),
                    DelayReason = DbText(reader, "DelayReasonID"),
                    CallDispatchTime = DbDate(reader, "CallDispatchTime"),
                    UnitsAssigned = DbText(reader, "UnitsAssigned"),
                    OfficerAssigned = DbText(reader, "OfficerAssigned"),
                    UnitInformed = DbDate(reader, "UnitInformedOn"),
                    UnitArrived = DbDate(reader, "UnitArrivedOn"),
                    ResolvedTime = DbDate(reader, "ResolvedTime")
                };
            }

            const string typesSql = @"
                SELECT IncidentTypeID
                FROM dbo.IncidentTypeDetails
                WHERE IncidentID = @IncidentID
                  AND Status <> 4;";

            SelectedOffenceIds.Clear();

            await using (var typesCommand = new SqlCommand(typesSql, connection))
            {
                typesCommand.Parameters.AddWithValue("@IncidentID", incidentId);

                await using var typesReader =
                    await typesCommand.ExecuteReaderAsync();

                while (await typesReader.ReadAsync())
                {
                    SelectedOffenceIds.Add(typesReader.GetInt32(0));
                }
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (Input == null ||
                !int.TryParse(Input.Region, out int regionId) ||
                !int.TryParse(Input.Priority, out int priorityId) ||
                !int.TryParse(Input.Media, out int callType) ||
                string.IsNullOrWhiteSpace(Input.IncidentNo) ||
                string.IsNullOrWhiteSpace(Input.CallerName))
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Incident number, caller name, region, priority, and media are required.");

                await OnGetAsync();
                return Page();
            }

            if (callType < 1 || callType > 5)
            {
                ModelState.AddModelError(
                    "Input.Media", "Please select a valid media type.");

                await OnGetAsync();
                return Page();
            }

            var username = User.Identity?.Name;

            if (string.IsNullOrWhiteSpace(username))
            {
                ModelState.AddModelError(
                    string.Empty, "Please log in again before saving.");

                await OnGetAsync();
                return Page();
            }

            var connectionString =
                _configuration.GetConnectionString("RepsDatabase");

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            await using SqlTransaction transaction =
                (SqlTransaction)await connection.BeginTransactionAsync();

            try
            {
                const string sql = @"
                    INSERT INTO dbo.IncidentRegister
                    (
                        ReferenceNo, IncidentNo, IncidentPriorityID, IncidentStatus,
                        IncidentAddress, WhenHappend, IncidentHappenning, IncidentHappend,
                        AdditionalDescription, ReferencePoint, RegionID, AffectedPerson,
                        CallerFullName, CallerPhone, CallerAddress, CallType, Relationship,
                        CallerLocation, SuspectGroup, SuspectPersons, Accent, HaveWeapons,
                        PhysicalAppearance, DressAppearance, LeavingDirection,
                        CapturedBy, CaptureDate, UpdateBy, UpdateDate,
                        CaptureExtension, UpdateExtension, CallerPIN,
                        CaptureUserName, UpdateUserName, ResolvedTime,
                        ResultCodeID, DelayReasonID, CallDispatchTime, UnitsAssigned,
                        OfficerAssigned, UnitInformedOn, UnitArrivedOn
                    )
                    OUTPUT INSERTED.IncidentID
                    VALUES
                    (
                        @ReferenceNo, @IncidentNo, @PriorityID, 0,
                        @IncidentAddress, @WhenHappened, @IncidentHappenning, @IncidentHappend,
                        @AdditionalDescription, @ReferencePoint, @RegionID, @AffectedPerson,
                        @CallerName, @CallerPhone, @CallerAddress, @CallType, @Relationship,
                        '', '', @SuspectPerson, @Accent, @HasWeapon,
                        @PhysicalAppearance, @DressAppearance, @LeavingDirection,
                        @CapturedBy, @CaptureDate, '', @UpdateDate,
                        NULL, NULL, @CallerPIN,
                        @CaptureUserName, NULL, @ResolvedTime,
                        @ResultCodeID, @DelayReasonID, @CallDispatchTime, @UnitsAssigned,
                        @OfficerAssigned, @UnitInformedOn, @UnitArrivedOn
                    );";

                long incidentId;

                await using (var command =
                    new SqlCommand(sql, connection, transaction))
                {
                    command.Parameters.AddWithValue("@ReferenceNo", Input.CallRefNo ?? "");
                    command.Parameters.AddWithValue("@IncidentNo", Input.IncidentNo!.Trim());
                    command.Parameters.AddWithValue("@PriorityID", priorityId);
                    command.Parameters.AddWithValue("@IncidentAddress", Input.IncidentAddress ?? "");
                    command.Parameters.AddWithValue("@WhenHappened", Input.IncidentTime ?? "");
                    command.Parameters.AddWithValue("@IncidentHappenning", Input.IncidentHappenning ?? "");
                    command.Parameters.AddWithValue("@IncidentHappend", Input.IncidentHappend ?? "");
                    command.Parameters.AddWithValue("@AdditionalDescription", Input.AdditionalDescription ?? "");
                    command.Parameters.AddWithValue("@ReferencePoint", Input.ReferencePoint ?? "");
                    command.Parameters.AddWithValue("@RegionID", regionId);
                    command.Parameters.AddWithValue("@AffectedPerson", Input.AffectedPerson ?? "");
                    command.Parameters.AddWithValue("@CallerName", Input.CallerName!.Trim());
                    command.Parameters.AddWithValue("@CallerPhone", Input.CallerCellNo ?? "");
                    command.Parameters.AddWithValue("@CallerAddress", Input.CallerAddress ?? "");
                    command.Parameters.AddWithValue("@CallType", callType);
                    command.Parameters.AddWithValue("@Relationship", Input.RelationShip ?? "");
                    command.Parameters.AddWithValue("@SuspectPerson", Input.SuspectPerson ?? "");
                    command.Parameters.AddWithValue("@Accent", Input.Accent ?? "");
                    command.Parameters.AddWithValue("@HasWeapon", Input.HasWeapon);
                    command.Parameters.AddWithValue("@PhysicalAppearance", Input.PhysicalAppearance ?? "");
                    command.Parameters.AddWithValue("@DressAppearance", Input.DressAppearance ?? "");
                    command.Parameters.AddWithValue("@LeavingDirection", Input.LeavingDirection ?? "");
                    command.Parameters.AddWithValue("@CapturedBy", username);
                    command.Parameters.AddWithValue("@CaptureDate", DateTime.Now);
                    command.Parameters.AddWithValue("@UpdateDate", new DateTime(1900, 1, 1));
                    command.Parameters.AddWithValue("@CaptureUserName", username);

                    command.Parameters.AddWithValue(
                        "@CallerPIN",
                        string.IsNullOrWhiteSpace(Input.CallerPIN)
                            ? DBNull.Value
                            : Input.CallerPIN);

                    command.Parameters.AddWithValue(
                        "@ResolvedTime",
                        (object?)ParseNullableDateTime(Input.ResolvedTime) ?? DBNull.Value);

                    command.Parameters.AddWithValue(
                        "@ResultCodeID",
                        (object?)ParseNullableInt(Input.ResultCode) ?? DBNull.Value);

                    command.Parameters.AddWithValue(
                        "@DelayReasonID",
                        (object?)ParseNullableInt(Input.DelayReason) ?? DBNull.Value);

                    command.Parameters.AddWithValue(
                        "@CallDispatchTime",
                        (object?)ParseNullableDateTime(Input.CallDispatchTime) ?? DBNull.Value);

                    command.Parameters.AddWithValue("@UnitsAssigned", Input.UnitsAssigned ?? "");
                    command.Parameters.AddWithValue("@OfficerAssigned", Input.OfficerAssigned ?? "");

                    command.Parameters.AddWithValue(
                        "@UnitInformedOn",
                        (object?)ParseNullableDateTime(Input.UnitInformed) ?? DBNull.Value);

                    command.Parameters.AddWithValue(
                        "@UnitArrivedOn",
                        (object?)ParseNullableDateTime(Input.UnitArrived) ?? DBNull.Value);

                    incidentId = Convert.ToInt64(await command.ExecuteScalarAsync());
                }

                foreach (var offenceId in SelectedOffenceIds.Distinct())
                {
                    const string offenceSql = @"
                        INSERT INTO dbo.IncidentTypeDetails
                            (IncidentID, IncidentCategoryID, IncidentTypeID, Status)
                        SELECT
                            @IncidentID, IncidentCategoryID, IncidentTypeID, 1
                        FROM dbo.IncidentType
                        WHERE IncidentTypeID = @TypeID
                          AND Status = 1;";

                    await using var offenceCommand =
                        new SqlCommand(offenceSql, connection, transaction);

                    offenceCommand.Parameters.AddWithValue("@IncidentID", incidentId);
                    offenceCommand.Parameters.AddWithValue("@TypeID", offenceId);

                    int inserted = await offenceCommand.ExecuteNonQueryAsync();

                    if (inserted != 1)
                    {
                        throw new InvalidOperationException(
                            $"Offence type {offenceId} is invalid or inactive.");
                    }
                }

                const string historySql = @"
                    INSERT INTO dbo.IncidentHistory
                        (IncidentID, CapturedBy, CaptureDate, CaptureExtension,
                         CaptureUserName, Description, Status)
                    VALUES
                        (@IncidentID, @CapturedBy, @CaptureDate, '',
                         @CaptureUserName, @Description, 1);";

                await using (var historyCommand =
                    new SqlCommand(historySql, connection, transaction))
                {
                    historyCommand.Parameters.AddWithValue("@IncidentID", incidentId);
                    historyCommand.Parameters.AddWithValue("@CapturedBy", username);
                    historyCommand.Parameters.AddWithValue("@CaptureDate", DateTime.Now);
                    historyCommand.Parameters.AddWithValue("@CaptureUserName", username);
                    historyCommand.Parameters.AddWithValue(
                        "@Description", "Incident created.");

                    await historyCommand.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();

                TempData["SaveMessage"] =
                    $"Incident {Input.IncidentNo} saved successfully. Record ID: {incidentId}.";

                return RedirectToPage("/Forms/AddIncident");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                ModelState.AddModelError(
                    string.Empty, $"Save failed: {ex.Message}");

                await OnGetAsync();
                return Page();
            }
        }

        private static string DbText(SqlDataReader reader, string column)
        {
            return reader[column] == DBNull.Value
                ? ""
                : Convert.ToString(reader[column]) ?? "";
        }


        private static string DbDate(SqlDataReader reader, string column)
        {
            if (reader[column] == DBNull.Value)
                return "";

            var value = reader[column];

            if (value is DateTime dateTime)
                return dateTime.ToString("yyyy-MM-ddTHH:mm");

            var text = value.ToString();

            if (string.IsNullOrWhiteSpace(text))
                return "";

            return DateTime.TryParse(text, out var parsedDate)
                ? parsedDate.ToString("yyyy-MM-ddTHH:mm")
                : "";
        }


        private static int? ParseNullableInt(string? value)
        {
            return int.TryParse(value, out int result) ? result : null;
        }


        private static DateTime? ParseNullableDateTime(string? value)
        {
            return DateTime.TryParse(value, out DateTime result) ? result : null;
        }

        private static async Task<List<SelectListItem>> LoadOptions(
            SqlConnection connection,
            string sql,
            string valueColumn,
            string textColumn,
            string placeholder)
        {
            var options = new List<SelectListItem>
            {
                new SelectListItem
                {
                    Value = "",
                    Text = placeholder
                }
            };

            await using var command = new SqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                options.Add(new SelectListItem
                {
                    Value = reader[valueColumn].ToString(),
                    Text = reader[textColumn] == DBNull.Value
                        ? ""
                        : reader[textColumn].ToString()
                });
            }

            return options;
        }
    }
}
