using Essensplan.Web.Data;
using Essensplan.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace Essensplan.Web.Services;

public class MenuService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public MenuService(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<List<Menu>> GetAllAsync(int householdId, MealType? mealType = null)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var query = db.Menus
            .Where(m => m.HouseholdId == householdId)
            .Include(m => m.MenuRecipes).ThenInclude(mr => mr.Recipe)
            .AsQueryable();

        if (mealType.HasValue)
        {
            var bit = MealTypeFlags.From(mealType.Value);
            query = query.Where(m => (m.AllowedMealTypes & bit) != 0);
        }

        return await query.OrderBy(m => m.Name).ToListAsync();
    }

    public async Task<Menu?> GetByIdAsync(int id, int householdId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Menus
            .Where(m => m.Id == id && m.HouseholdId == householdId)
            .Include(m => m.MenuRecipes.OrderBy(mr => mr.SortOrder)).ThenInclude(mr => mr.Recipe)
            .FirstOrDefaultAsync();
    }

    public async Task<Menu> CreateAsync(Menu menu, List<int> recipeIds, int householdId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        menu.HouseholdId = householdId;
        menu.CreatedAt = DateTime.UtcNow;
        if (menu.AllowedMealTypes == 0) menu.AllowedMealTypes = MealTypeFlags.Mittagessen | MealTypeFlags.Abendessen;
        menu.MenuRecipes = recipeIds.Select((rid, i) => new MenuRecipe { RecipeId = rid, SortOrder = i }).ToList();
        db.Menus.Add(menu);
        await db.SaveChangesAsync();
        return menu;
    }

    public async Task UpdateAsync(Menu menu, List<int> recipeIds, int householdId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var existing = await db.Menus
            .Include(m => m.MenuRecipes)
            .FirstOrDefaultAsync(m => m.Id == menu.Id && m.HouseholdId == householdId);
        if (existing is null) return;

        existing.Name = menu.Name;
        existing.Description = menu.Description;
        existing.AllowedMealTypes = menu.AllowedMealTypes;

        db.MenuRecipes.RemoveRange(existing.MenuRecipes);
        existing.MenuRecipes = recipeIds.Select((rid, i) => new MenuRecipe { RecipeId = rid, SortOrder = i }).ToList();

        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id, int householdId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var menu = await db.Menus.FirstOrDefaultAsync(m => m.Id == id && m.HouseholdId == householdId);
        if (menu is null) return;
        db.Menus.Remove(menu);
        await db.SaveChangesAsync();
    }
}
