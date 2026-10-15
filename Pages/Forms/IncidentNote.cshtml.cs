
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace REPS_IMS.Pages.Forms
{
    public class IncidentNoteModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public IncidentNoteModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [BindProperty(SupportsGet = true)]
        public long? iid { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? ext { get; set; }

        [BindProperty]
        public string NoteText { get; set; } = "";

        [BindProperty]
        public long? EditNoteID { get; set; }

        public List<NoteRecord> Notes { get; set; } = new();

        public string? ErrorMessage { get; set; }

        public class NoteRecord
        {
            public long NoteID { get; set; }
            public string NoteDescription { get; set; } = "";
            public DateTime? CaptureDate { get; set; }
            public string CaptureBy { get; set; } = "";
        }

        private string ConnectionString =>
            _configuration.GetConnectionString("RepsDatabase")
            ?? throw new InvalidOperationException(
                "Connection string 'RepsDatabase' was not found.");

        private string CurrentUsername =>
            User.Identity?.Name
            ?? throw new InvalidOperationException(
                "The logged-in username could not be determined.");

        public async Task OnGetAsync()
        {
            if (!iid.HasValue || iid.Value <= 0)
            {
                ErrorMessage = "No incident was selected.";
                return;
            }

            await LoadNotesAsync();
        }

        public async Task<IActionResult> OnPostSaveAsync()
        {
            if (!iid.HasValue || iid.Value <= 0)
            {
                ErrorMessage = "No incident was selected.";
                await LoadNotesAsync();
                return Page();
            }

            if (string.IsNullOrWhiteSpace(NoteText))
            {
                ErrorMessage = "Please enter a note.";
                await LoadNotesAsync();
                return Page();
            }

            try
            {
                await using var connection = new SqlConnection(ConnectionString);
                await connection.OpenAsync();

                if (EditNoteID.HasValue)
                {
                    const string updateSql = @"
                        UPDATE dbo.IncidentNote
                        SET NoteDescription = @NoteDescription,
                            UpdateBy = @UpdateBy,
                            UpdateDate = @UpdateDate,
                            UpdateExtension = @UpdateExtension
                        WHERE NoteID = @NoteID
                          AND IncidentID = @IncidentID
                          AND Status = 0;";

                    await using var command =
                        new SqlCommand(updateSql, connection);

                    command.Parameters.AddWithValue(
                        "@NoteDescription", NoteText.Trim());
                    command.Parameters.AddWithValue(
                        "@UpdateBy", CurrentUsername);
                    command.Parameters.AddWithValue(
                        "@UpdateDate", DateTime.Now);
                    command.Parameters.AddWithValue(
                        "@UpdateExtension", (object?)ext ?? DBNull.Value);
                    command.Parameters.AddWithValue(
                        "@NoteID", EditNoteID.Value);
                    command.Parameters.AddWithValue(
                        "@IncidentID", iid.Value);

                    if (await command.ExecuteNonQueryAsync() == 0)
                    {
                        ErrorMessage = "The note could not be updated.";
                        await LoadNotesAsync();
                        return Page();
                    }
                }
                else
                {
                    const string insertSql = @"
                        INSERT INTO dbo.IncidentNote
                            (IncidentID, NoteDescription, CaptureDate,
                             CaptureBy, Status, CaptureExtension)
                        VALUES
                            (@IncidentID, @NoteDescription, @CaptureDate,
                             @CaptureBy, 0, @CaptureExtension);";

                    await using var command =
                        new SqlCommand(insertSql, connection);

                    command.Parameters.AddWithValue(
                        "@IncidentID", iid.Value);
                    command.Parameters.AddWithValue(
                        "@NoteDescription", NoteText.Trim());
                    command.Parameters.AddWithValue(
                        "@CaptureDate", DateTime.Now);
                    command.Parameters.AddWithValue(
                        "@CaptureBy", CurrentUsername);
                    command.Parameters.AddWithValue(
                        "@CaptureExtension", (object?)ext ?? DBNull.Value);

                    await command.ExecuteNonQueryAsync();
                }

                return RedirectToPage("/Forms/IncidentNote",
                    new { iid, ext, @ref = "sup" });
            }
            catch
            {
                ErrorMessage = "Unable to save the note.";
                await LoadNotesAsync();
                return Page();
            }
        }

        public async Task<IActionResult> OnPostResetAsync()
        {
            return RedirectToPage("/Forms/IncidentNote",
                new { iid, ext, @ref = "sup" });
        }

        public async Task<IActionResult> OnPostEditAsync(long noteID)
        {
            if (!iid.HasValue)
                return BadRequest();

            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();

            const string sql = @"
                SELECT NoteID, NoteDescription
                FROM dbo.IncidentNote
                WHERE NoteID = @NoteID
                  AND IncidentID = @IncidentID
                  AND Status = 0;";

            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@NoteID", noteID);
            command.Parameters.AddWithValue("@IncidentID", iid.Value);

            await using var reader = await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return NotFound();

            EditNoteID = Convert.ToInt64(reader["NoteID"]);
            NoteText = reader["NoteDescription"]?.ToString() ?? "";

            await reader.CloseAsync();
            await LoadNotesAsync();

            return Page();
        }

        public async Task<IActionResult> OnPostDeleteAsync(long noteID)
        {
            if (!iid.HasValue || iid.Value <= 0)
                return BadRequest();

            try
            {
                await using var connection = new SqlConnection(ConnectionString);
                await connection.OpenAsync();

                const string sql = @"
                    UPDATE dbo.IncidentNote
                    SET Status = 4,
                        UpdateBy = @UpdateBy,
                        UpdateDate = @UpdateDate,
                        UpdateExtension = @UpdateExtension
                    WHERE NoteID = @NoteID
                      AND IncidentID = @IncidentID
                      AND Status = 0;";

                await using var command = new SqlCommand(sql, connection);
                command.Parameters.AddWithValue("@UpdateBy", CurrentUsername);
                command.Parameters.AddWithValue("@UpdateDate", DateTime.Now);
                command.Parameters.AddWithValue(
                    "@UpdateExtension", (object?)ext ?? DBNull.Value);
                command.Parameters.AddWithValue("@NoteID", noteID);
                command.Parameters.AddWithValue("@IncidentID", iid.Value);

                await command.ExecuteNonQueryAsync();

                return RedirectToPage("/Forms/IncidentNote",
                   new { iid, ext, @ref = "sup" });
            }
            catch
            {
                ErrorMessage = "Unable to delete the note.";
                await LoadNotesAsync();
                return Page();
            }
        }

        private async Task LoadNotesAsync()
        {
            Notes.Clear();

            if (!iid.HasValue || iid.Value <= 0)
                return;

            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();

            const string sql = @"
                SELECT NoteID, NoteDescription, CaptureDate, CaptureBy
                FROM dbo.IncidentNote
                WHERE IncidentID = @IncidentID
                  AND Status = 0
                ORDER BY CaptureDate DESC;";

            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@IncidentID", iid.Value);

            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                Notes.Add(new NoteRecord
                {
                    NoteID = Convert.ToInt64(reader["NoteID"]),
                    NoteDescription =
                        reader["NoteDescription"]?.ToString() ?? "",
                    CaptureDate = reader["CaptureDate"] == DBNull.Value
                        ? null
                        : Convert.ToDateTime(reader["CaptureDate"]),
                    CaptureBy = reader["CaptureBy"]?.ToString() ?? ""
                });
            }
        }
    }
}
