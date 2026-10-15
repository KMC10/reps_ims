using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login";
        options.AccessDeniedPath = "/Login";
    });
builder.Services.AddAuthorization();

var app = builder.Build();
app.MapGet("/test-db", async (IConfiguration configuration) =>
{
    var connectionString = configuration.GetConnectionString("RepsDatabase");

    await using var connection =
        new Microsoft.Data.SqlClient.SqlConnection(connectionString);

    await connection.OpenAsync();

    await using var command = connection.CreateCommand();
    command.CommandText = "SELECT DB_NAME()";

    var databaseName = await command.ExecuteScalarAsync();

    return Results.Ok(new { Connected = true, Database = databaseName });
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

//app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapGet("/", () => Results.Redirect("/Login"));
app.Run();
