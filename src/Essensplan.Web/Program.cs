using Essensplan.Web.Components;
using Essensplan.Web.Data;
using Essensplan.Web.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.local.json", optional: true, reloadOnChange: true);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found. Set it in appsettings.json or the ConnectionStrings__DefaultConnection environment variable.");

// A fixed server version avoids an extra blocking DB round-trip at startup (and keeps
// `dotnet ef` tooling working without a reachable database). Override via config if needed.
var mySqlVersionString = builder.Configuration["Database:ServerVersion"] ?? "8.0.34";
var serverVersion = new MySqlServerVersion(new Version(mySqlVersionString));

builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseMySql(connectionString, serverVersion));

builder.Services.AddScoped<RecipeService>();
builder.Services.AddScoped<CategoryService>();
builder.Services.AddScoped<MenuService>();
builder.Services.AddScoped<WeekPlanService>();

builder.Services.AddHttpClient<RecipeImportService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

if (app.Configuration.GetValue("Database:ApplyMigrationsOnStartup", false))
{
    using var scope = app.Services.CreateScope();
    var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
    await using var db = await dbFactory.CreateDbContextAsync();
    await db.Database.MigrateAsync();
    await EnsureMenuColumnMigratedAsync(db);
}

async Task EnsureMenuColumnMigratedAsync(AppDbContext db)
{
    // Repair: AddMenuAllowedMealTypes may be in __EFMigrationsHistory but the column rename
    // may have failed silently on some MariaDB versions. Uses raw ADO.NET to bypass EF's
    // query pipeline and check the actual schema state.
    try
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync();

        // Check if AllowedMealTypes already exists
        using var checkCmd = conn.CreateCommand();
        checkCmd.CommandText =
            "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS " +
            "WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Menus' AND COLUMN_NAME = 'AllowedMealTypes'";
        var exists = Convert.ToInt64(await checkCmd.ExecuteScalarAsync() ?? 0L);
        if (exists > 0) return; // column is there, nothing to do

        app.Logger.LogInformation("Applying column fix: renaming MealType → AllowedMealTypes on Menus table.");

        using var addCmd = conn.CreateCommand();
        addCmd.CommandText = "ALTER TABLE `Menus` ADD COLUMN `AllowedMealTypes` int NOT NULL DEFAULT 6";
        await addCmd.ExecuteNonQueryAsync();

        using var convertCmd = conn.CreateCommand();
        convertCmd.CommandText = "UPDATE `Menus` SET `AllowedMealTypes` = 1 << `MealType`";
        await convertCmd.ExecuteNonQueryAsync();

        using var dropCmd = conn.CreateCommand();
        dropCmd.CommandText = "ALTER TABLE `Menus` DROP COLUMN IF EXISTS `MealType`";
        await dropCmd.ExecuteNonQueryAsync();

        app.Logger.LogInformation("Column fix applied successfully.");
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "EnsureMenuColumnMigrated: could not check/fix column state, continuing.");
    }
}

app.Run();
