
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace REPS_IMS.Pages.Forms
{
    public class IncidentHistoryModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public IncidentHistoryModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [BindProperty(SupportsGet = true)]
        public long? iid { get; set; }

        public List<HistoryRecord> History { get; set; } = new();

        public string? ErrorMessage { get; set; }

        public class HistoryRecord
        {
            public DateTime? CaptureDate { get; set; }
            public string CapturedBy { get; set; } = "";
            public string Description { get; set; } = "";
        }

        public async Task OnGetAsync()
        {
            if (!iid.HasValue || iid.Value <= 0)
            {
                ErrorMessage = "No incident was selected.";
                return;
            }

            string connectionString =
                _configuration.GetConnectionString("RepsDatabase")
                ?? throw new InvalidOperationException(
                    "Connection string 'RepsDatabase' was not found.");

            const string sql = @"
                SELECT CaptureDate, CapturedBy, Description
                FROM dbo.IncidentHistory
                WHERE IncidentID = @IncidentID
                  AND Status <> 4
                ORDER BY CaptureDate DESC;";

            try
            {
                await using var connection =
                    new SqlConnection(connectionString);

                await connection.OpenAsync();

                await using var command = new SqlCommand(sql, connection);
                command.Parameters.AddWithValue("@IncidentID", iid.Value);

                await using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    History.Add(new HistoryRecord
                    {
                        CaptureDate = reader["CaptureDate"] == DBNull.Value
                            ? null
                            : Convert.ToDateTime(reader["CaptureDate"]),

                        CapturedBy = reader["CapturedBy"] == DBNull.Value
                            ? ""
                            : reader["CapturedBy"].ToString() ?? "",

                        Description = reader["Description"] == DBNull.Value
                            ? ""
                            : reader["Description"].ToString() ?? ""
                    });
                }
            }
            catch
            {
                ErrorMessage = "Unable to load incident history.";
            }
        }
    }
}
