using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using System.Data;

namespace REPS_IMS.Pages.Forms;

[IgnoreAntiforgeryToken]
public class CrimeDashboardBubbleDataModel : PageModel
{
    private readonly IConfiguration _configuration;

    public CrimeDashboardBubbleDataModel(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var connectionString =
            _configuration.GetConnectionString("RepsDatabase");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return StatusCode(500, "Database connection is not configured.");
        }

        var categories = new List<BubbleCategory>();

        var endDate = DateTime.Today.AddDays(1);
        var startDate = endDate.AddYears(-5);

        const string sql = @"
            SELECT
                c.IncidentCategoryName,
                t.IncidentTypeName,
                COUNT(*) AS IncidentCount
            FROM dbo.IncidentTypeDetails d
            INNER JOIN dbo.IncidentRegister i
                ON i.IncidentID = d.IncidentID
            INNER JOIN dbo.IncidentType t
                ON t.IncidentTypeID = d.IncidentTypeID
            INNER JOIN dbo.IncidentCategory c
                ON c.IncidentCategoryID = d.IncidentCategoryID
            WHERE i.CaptureDate >= @StartDate
              AND i.CaptureDate < @EndDate
            GROUP BY
                c.IncidentCategoryID,
                c.IncidentCategoryName,
                t.IncidentTypeID,
                t.IncidentTypeName
            ORDER BY
                c.IncidentCategoryName,
                t.IncidentTypeName;";

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);

        command.Parameters.Add("@StartDate", SqlDbType.DateTime).Value =
            startDate;

        command.Parameters.Add("@EndDate", SqlDbType.DateTime).Value =
            endDate;

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var categoryName = reader.GetString(0);
            var typeName = reader.GetString(1);
            var count = Convert.ToInt32(reader.GetValue(2));

            var category = categories.FirstOrDefault(
                c => c.Name == categoryName);

            if (category == null)
            {
                category = new BubbleCategory
                {
                    Name = categoryName
                };

                categories.Add(category);
            }

            category.Children.Add(new BubbleCrimeType
            {
                Name = typeName,
                Size = count
            });
        }

        return new JsonResult(new BubbleRoot
        {
            Name = "Crimes",
            Children = categories
                .Where(c => c.Children.Count > 0)
                .ToList()
        });
    }
}

public class BubbleRoot
{
    public string Name { get; set; } = string.Empty;
    public List<BubbleCategory> Children { get; set; } = new();
}

public class BubbleCategory
{
    public string Name { get; set; } = string.Empty;
    public List<BubbleCrimeType> Children { get; set; } = new();
}

public class BubbleCrimeType
{
    public string Name { get; set; } = string.Empty;
    public int Size { get; set; }
}
