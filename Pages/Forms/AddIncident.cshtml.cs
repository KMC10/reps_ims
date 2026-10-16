
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;
using System.Data;

namespace REPS_IMS.Pages.Forms
{
    public class AddIncidentModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public AddIncidentModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [BindProperty(SupportsGet = true)]
        public string ReturnUrl { get; set; } = "/Forms/Dashboard";

        [BindProperty(SupportsGet = true)]
        public long? iid { get; set; }

        public List<SelectListItem> Regions { get; set; } = new();
        public List<SelectListItem> Priorities { get; set; } = new();

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
                "RegionID",
                "RegionName",
                "Select region");

            Priorities = await LoadOptions(
                connection,
                @"SELECT IncidentPriorityID, IncidentPriorityName
                  FROM dbo.IncidentPriority
                  ORDER BY IncidentPriorityID",
                "IncidentPriorityID",
                "IncidentPriorityName",
                "Select priority");

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
                    i.AdditionalDescription
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
                    AdditionalDescription = DbText(reader, "AdditionalDescription")
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
                bool isEditing = iid.HasValue && iid.Value > 0;

                const string insertSql = @"
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
                CaptureUserName, UpdateUserName
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
                @CaptureUserName, NULL
            );";

                const string updateSql = @"
            UPDATE dbo.IncidentRegister
            SET
                ReferenceNo = @ReferenceNo,
                IncidentNo = @IncidentNo,
                IncidentPriorityID = @PriorityID,
                IncidentAddress = @IncidentAddress,
                WhenHappend = @WhenHappened,
                IncidentHappenning = @IncidentHappenning,
                IncidentHappend = @IncidentHappend,
                AdditionalDescription = @AdditionalDescription,
                ReferencePoint = @ReferencePoint,
                RegionID = @RegionID,
                AffectedPerson = @AffectedPerson,
                CallerFullName = @CallerName,
                CallerPhone = @CallerPhone,
                CallerAddress = @CallerAddress,
                CallType = @CallType,
                Relationship = @Relationship,
                SuspectPersons = @SuspectPerson,
                Accent = @Accent,
                HaveWeapons = @HasWeapon,
                PhysicalAppearance = @PhysicalAppearance,
                DressAppearance = @DressAppearance,
                LeavingDirection = @LeavingDirection,
                CallerPIN = @CallerPIN,
                UpdateBy = @UpdateBy,
                UpdateDate = GETDATE(),
                UpdateUserName = @UpdateUserName
            OUTPUT INSERTED.IncidentID
            WHERE IncidentID = @IncidentID;";

                long incidentId;

                await using (var command = new SqlCommand(
                    isEditing ? updateSql : insertSql,
                    connection,
                    transaction))
                {
                    if (isEditing)
                    {
                        command.Parameters.Add("@IncidentID", SqlDbType.BigInt)
                            .Value = iid!.Value;

                        command.Parameters.Add("@UpdateBy", SqlDbType.NVarChar, 255)
                            .Value = username;

                        command.Parameters.Add("@UpdateUserName", SqlDbType.NVarChar, 255)
                            .Value = username;
                    }
                    else
                    {
                        command.Parameters.Add("@CapturedBy", SqlDbType.NVarChar, 255)
                            .Value = username;

                        command.Parameters.Add("@CaptureDate", SqlDbType.DateTime)
                            .Value = DateTime.Now;

                        command.Parameters.Add("@CaptureUserName", SqlDbType.NVarChar, 255)
                            .Value = username;

                        command.Parameters.Add("@UpdateDate", SqlDbType.DateTime)
                            .Value = new DateTime(1900, 1, 1);
                    }

                    command.Parameters.Add("@ReferenceNo", SqlDbType.NVarChar, 255)
                        .Value = Input.CallRefNo ?? "";

                    command.Parameters.Add("@IncidentNo", SqlDbType.NVarChar, 255)
                        .Value = Input.IncidentNo!.Trim();

                    command.Parameters.Add("@PriorityID", SqlDbType.Int)
                        .Value = priorityId;

                    command.Parameters.Add("@IncidentAddress", SqlDbType.NVarChar, -1)
                        .Value = Input.IncidentAddress ?? "";

                    command.Parameters.Add("@WhenHappened", SqlDbType.NVarChar, 255)
                        .Value = Input.IncidentTime ?? "";

                    command.Parameters.Add("@IncidentHappenning", SqlDbType.NVarChar, -1)
                        .Value = Input.IncidentHappenning ?? "";

                    command.Parameters.Add("@IncidentHappend", SqlDbType.NVarChar, -1)
                        .Value = Input.IncidentHappend ?? "";

                    command.Parameters.Add("@AdditionalDescription", SqlDbType.NVarChar, -1)
                        .Value = Input.AdditionalDescription ?? "";

                    command.Parameters.Add("@ReferencePoint", SqlDbType.NVarChar, 255)
                        .Value = Input.ReferencePoint ?? "";

                    command.Parameters.Add("@RegionID", SqlDbType.Int)
                        .Value = regionId;

                    command.Parameters.Add("@AffectedPerson", SqlDbType.NVarChar, 255)
                        .Value = Input.AffectedPerson ?? "";

                    command.Parameters.Add("@CallerName", SqlDbType.NVarChar, 255)
                        .Value = Input.CallerName!.Trim();

                    command.Parameters.Add("@CallerPhone", SqlDbType.NVarChar, 100)
                        .Value = Input.CallerCellNo ?? "";

                    command.Parameters.Add("@CallerAddress", SqlDbType.NVarChar, -1)
                        .Value = Input.CallerAddress ?? "";

                    command.Parameters.Add("@CallType", SqlDbType.Int)
                        .Value = callType;

                    command.Parameters.Add("@Relationship", SqlDbType.NVarChar, 255)
                        .Value = Input.RelationShip ?? "";

                    command.Parameters.Add("@SuspectPerson", SqlDbType.NVarChar, -1)
                        .Value = Input.SuspectPerson ?? "";

                    command.Parameters.Add("@Accent", SqlDbType.NVarChar, 255)
                        .Value = Input.Accent ?? "";

                    command.Parameters.Add("@HasWeapon", SqlDbType.Bit)
                        .Value = Input.HasWeapon;

                    command.Parameters.Add("@PhysicalAppearance", SqlDbType.NVarChar, -1)
                        .Value = Input.PhysicalAppearance ?? "";

                    command.Parameters.Add("@DressAppearance", SqlDbType.NVarChar, -1)
                        .Value = Input.DressAppearance ?? "";

                    command.Parameters.Add("@LeavingDirection", SqlDbType.NVarChar, -1)
                        .Value = Input.LeavingDirection ?? "";

                    command.Parameters.Add("@CallerPIN", SqlDbType.NVarChar, 50)
                        .Value = string.IsNullOrWhiteSpace(Input.CallerPIN)
                            ? DBNull.Value
                            : Input.CallerPIN;

                    var result = await command.ExecuteScalarAsync();

                    if (result == null || result == DBNull.Value)
                    {
                        throw new InvalidOperationException(
                            isEditing
                                ? "The incident could not be found for updating."
                                : "The incident could not be created.");
                    }

                    incidentId = Convert.ToInt64(result);
                }

                // Replace the incident's active offence selections.
                // Status 4 retains the old records as inactive history.
                if (isEditing)
                {
                    const string deactivateSql = @"
                UPDATE dbo.IncidentTypeDetails
                SET Status = 4
                WHERE IncidentID = @IncidentID
                  AND Status <> 4;";

                    await using var deactivateCommand =
                        new SqlCommand(deactivateSql, connection, transaction);

                    deactivateCommand.Parameters.Add("@IncidentID", SqlDbType.BigInt)
                        .Value = incidentId;

                    await deactivateCommand.ExecuteNonQueryAsync();
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

                    offenceCommand.Parameters.Add("@IncidentID", SqlDbType.BigInt)
                        .Value = incidentId;

                    offenceCommand.Parameters.Add("@TypeID", SqlDbType.Int)
                        .Value = offenceId;

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
                (@IncidentID, @CapturedBy, GETDATE(), '',
                 @CaptureUserName, @Description, 1);";

                await using (var historyCommand =
                    new SqlCommand(historySql, connection, transaction))
                {
                    historyCommand.Parameters.Add("@IncidentID", SqlDbType.BigInt)
                        .Value = incidentId;

                    historyCommand.Parameters.Add("@CapturedBy", SqlDbType.NVarChar, 255)
                        .Value = username;

                    historyCommand.Parameters.Add("@CaptureUserName", SqlDbType.NVarChar, 255)
                        .Value = username;

                    historyCommand.Parameters.Add("@Description", SqlDbType.NVarChar, -1)
                        .Value = isEditing
                            ? "Incident information updated."
                            : "Incident created.";

                    await historyCommand.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();

                TempData["SaveMessage"] = isEditing
                    ? $"Incident {Input.IncidentNo} updated successfully."
                    : $"Incident {Input.IncidentNo} saved successfully. Record ID: {incidentId}.";

                return RedirectToPage(
                    "/Forms/IncidentDetails",
                    new { iid = incidentId });
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
