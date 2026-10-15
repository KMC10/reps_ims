# REPS IMS - ASP.NET Core Migration

This is the initial ASP.NET Core migration of the supplied ASP.NET Web Forms application.

## Target
- ASP.NET Core / .NET 10
- Razor Pages
- Cookie authentication
- SQL Server via Microsoft.Data.SqlClient

## Important
The supplied ZIP contains the Web Forms markup and compiled DLLs, but not the original `.cs` code-behind files.
Therefore the UI/routes have been migrated first and the login flow has been reconstructed from the known application behavior.

Before production:
1. Put the real SQL Server connection strings in `appsettings.json` or user secrets/environment variables.
2. Verify the exact legacy TripleDES implementation in `IMSWEB.dll` so existing passwords continue to authenticate.
3. Migrate each page's code-behind/business logic from `IMSWEB.dll`, `IMS.DataAccessLayer.dll`, and `IMS.Common.dll`.
4. Replace the old ActiveReports v8 WebForms integration with the ASP.NET Core-compatible reporting approach.
5. Add authorization to the pages that require an authenticated user.

## First test
Run the project and open:

`/Login`

After a successful login the migrated flow redirects to:

`/Forms/Dashboard`

The existing Bootstrap/CSS/JS assets were copied from the original application.
