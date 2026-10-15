using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace REPS_IMS.Pages;

public class LoginModel : PageModel
{
    private readonly IConfiguration _configuration;

    public LoginModel(IConfiguration configuration) => _configuration = configuration;

    [BindProperty] public string Username { get; set; } = "";
    [BindProperty] public string Password { get; set; } = "";
    public string? ErrorMessage { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Username and password are required.";
            return Page();
        }

        var encryptedPassword = EncryptLegacy(Password);
        var connectionString = _configuration.GetConnectionString("RepsDatabase");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        //command.CommandTimeout = 120;

        const string sql = @"SELECT TOP 1 UserName, Status
                             FROM Users
                             WHERE UserName = @UserName AND Password = @Password";

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserName", Username.Trim());
        command.Parameters.AddWithValue("@Password", encryptedPassword);

        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            ErrorMessage = "Invalid username or password.";
            return Page();
        }

        var status = Convert.ToInt32(reader["Status"]);

        if (status == 2)
        {
            ErrorMessage = "This user name is blocked by administrator.";
            return Page();
        }

        if (status != 1)
        {
            ErrorMessage = "This user account is not active.";
            return Page();
        }

        var claims = new[] { new Claim(ClaimTypes.Name, reader["UserName"].ToString() ?? Username.Trim()) };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));

        return RedirectToPage("/Forms/Dashboard");
    }

    private static string EncryptLegacy(string plainText)
    {
        // The original application uses TripleDES. Keep this method compatible
        // with the existing database format; verify the exact legacy key/mode
        // against EncryptionHelpers in IMSWEB.dll before production use.
        var key = Enumerable.Range(1, 24).Select(i => (byte)i).ToArray();

        using var tripleDes = TripleDES.Create();
        tripleDes.Key = key;
        tripleDes.Mode = CipherMode.ECB;
        tripleDes.Padding = PaddingMode.PKCS7;

        var bytes = Encoding.UTF8.GetBytes(plainText);
        var encrypted = tripleDes.CreateEncryptor().TransformFinalBlock(bytes, 0, bytes.Length);
        return Convert.ToBase64String(encrypted);
    }
}
