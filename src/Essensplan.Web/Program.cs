using Essensplan.Web.Components;
using Essensplan.Web.Data;
using Essensplan.Web.Models;
using Essensplan.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.local.json", optional: true, reloadOnChange: true);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddRazorPages();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

var mySqlVersionString = builder.Configuration["Database:ServerVersion"] ?? "8.0.34";
var serverVersion = new MySqlServerVersion(new Version(mySqlVersionString));

builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseMySql(connectionString, serverVersion));

// Identity with relaxed password requirements (family app)
builder.Services.AddIdentity<AppUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 8;
    options.SignIn.RequireConfirmedAccount = false;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/anmelden";
    options.LogoutPath = "/abmelden";
    options.AccessDeniedPath = "/anmelden";
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
    options.SlidingExpiration = true;
    options.Cookie.Name = "Essensplan.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

builder.Services.AddScoped<RecipeService>();
builder.Services.AddScoped<CategoryService>();
builder.Services.AddScoped<MenuService>();
builder.Services.AddScoped<WeekPlanService>();
builder.Services.AddScoped<RecipeRatingService>();
builder.Services.AddScoped<ShoppingListService>();
builder.Services.AddScoped<UserManagementService>();
builder.Services.AddScoped<CurrentUserService>();
builder.Services.AddSingleton<WeekPlanChangeNotifier>();

builder.Services.AddHttpClient<RecipeImportService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
});

builder.Services.AddHttpClient<OpenFoodFactsService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("User-Agent", "Essensplan/1.0 (https://github.com/danielmerz/essensplan)");
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapRazorPages();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

if (app.Configuration.GetValue("Database:ApplyMigrationsOnStartup", false))
{
    await using var migScope = app.Services.CreateAsyncScope();
    var dbFactory = migScope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
    await using var db = await dbFactory.CreateDbContextAsync();
    await FixPartialMigrationAsync(db);
    await db.Database.MigrateAsync();
    await EnsureMenuColumnMigratedAsync(db);
}

await SeedAsync(app.Services, app.Logger);

app.Run();

async Task SeedAsync(IServiceProvider services, ILogger logger)
{
    await using var scope = services.CreateAsyncScope();
    var sp = scope.ServiceProvider;
    var dbFactory = sp.GetRequiredService<IDbContextFactory<AppDbContext>>();
    await using var db = await dbFactory.CreateDbContextAsync();

    // Ensure roles exist
    var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
    foreach (var role in new[] { "SuperAdmin" })
    {
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole(role));
    }

    // Skip seeding if any user already exists
    if (await db.Users.AnyAsync()) return;

    // Create the initial household
    var household = new Household { Name = "Familie Merz", CreatedAt = DateTime.UtcNow };
    db.Households.Add(household);
    await db.SaveChangesAsync();

    // Create SuperAdmin user
    var userManager = sp.GetRequiredService<UserManager<AppUser>>();
    var adminEmail = "dani@familie-merz.ch";
    var initialPassword = app.Configuration["Setup:AdminInitialPassword"] ?? GenerateInitialPassword();

    var adminUser = new AppUser
    {
        UserName = adminEmail,
        Email = adminEmail,
        DisplayName = "Dani",
        EmailConfirmed = true,
        IsActive = true,
        CreatedAt = DateTime.UtcNow
    };

    var result = await userManager.CreateAsync(adminUser, initialPassword);
    if (!result.Succeeded)
    {
        logger.LogError("Failed to create admin user: {Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
        return;
    }

    await userManager.AddToRoleAsync(adminUser, "SuperAdmin");

    db.HouseholdMemberships.Add(new HouseholdMembership
    {
        HouseholdId = household.Id,
        UserId = adminUser.Id,
        Role = HouseholdRole.Admin,
        JoinedAt = DateTime.UtcNow
    });
    await db.SaveChangesAsync();

    logger.LogWarning("=== ERSTEINRICHTUNG ===");
    logger.LogWarning("Admin-Benutzer angelegt: {Email}", adminEmail);
    logger.LogWarning("Initiales Passwort:      {Password}", initialPassword);
    logger.LogWarning("Bitte nach erstem Login das Passwort ändern!");
    logger.LogWarning("======================");
}

static string GenerateInitialPassword()
{
    var part = Guid.NewGuid().ToString("N")[..10].ToUpper();
    return $"Ep!{part}";
}

async Task FixPartialMigrationAsync(AppDbContext db)
{
    const string migId = "20260723130526_AddMultiUserSupport";
    try
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync();

        // Only proceed if __EFMigrationsHistory exists (i.e. not a brand-new empty DB)
        using var histCheck = conn.CreateCommand();
        histCheck.CommandText = "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '__EFMigrationsHistory'";
        if (Convert.ToInt64(await histCheck.ExecuteScalarAsync() ?? 0L) == 0) return;

        // Only proceed if this specific migration is NOT yet recorded
        using var migCheck = conn.CreateCommand();
        migCheck.CommandText = $"SELECT COUNT(*) FROM `__EFMigrationsHistory` WHERE `MigrationId` = '{migId}'";
        if (Convert.ToInt64(await migCheck.ExecuteScalarAsync() ?? 0L) > 0) return;

        // Confirm the Households table exists (partial migration state)
        using var hhCheck = conn.CreateCommand();
        hhCheck.CommandText = "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Households'";
        if (Convert.ToInt64(await hhCheck.ExecuteScalarAsync() ?? 0L) == 0) return;

        app.Logger.LogWarning("Partielle Migration erkannt – Abschluss wird durchgeführt...");

        async Task ExecAsync(string sql)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync();
        }

        await ExecAsync("INSERT IGNORE INTO `Households` (`Name`, `CreatedAt`) VALUES ('Familie Merz', UTC_TIMESTAMP())");
        await ExecAsync("UPDATE `Menus` SET `HouseholdId` = 1 WHERE `HouseholdId` = 0");
        await ExecAsync("UPDATE `Recipes` SET `HouseholdId` = 1 WHERE `HouseholdId` = 0");
        await ExecAsync("UPDATE `WeekPlans` SET `HouseholdId` = 1 WHERE `HouseholdId` = 0");
        await ExecAsync("ALTER TABLE `Menus` ADD CONSTRAINT `FK_Menus_Households_HouseholdId` FOREIGN KEY (`HouseholdId`) REFERENCES `Households` (`Id`) ON DELETE CASCADE");
        await ExecAsync("ALTER TABLE `Recipes` ADD CONSTRAINT `FK_Recipes_Households_HouseholdId` FOREIGN KEY (`HouseholdId`) REFERENCES `Households` (`Id`) ON DELETE CASCADE");
        await ExecAsync("ALTER TABLE `WeekPlans` ADD CONSTRAINT `FK_WeekPlans_Households_HouseholdId` FOREIGN KEY (`HouseholdId`) REFERENCES `Households` (`Id`) ON DELETE CASCADE");
        await ExecAsync($"INSERT IGNORE INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`) VALUES ('{migId}', '9.0.0')");

        app.Logger.LogWarning("Partielle Migration erfolgreich abgeschlossen.");
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "FixPartialMigration: Fehler beim Abschliessen der Migration.");
    }
}

async Task EnsureMenuColumnMigratedAsync(AppDbContext db)
{
    try
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync();

        using var checkCmd = conn.CreateCommand();
        checkCmd.CommandText =
            "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS " +
            "WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Menus' AND COLUMN_NAME = 'AllowedMealTypes'";
        var exists = Convert.ToInt64(await checkCmd.ExecuteScalarAsync() ?? 0L);
        if (exists > 0) return;

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
