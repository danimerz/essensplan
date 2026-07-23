using Essensplan.Web.Data;
using Essensplan.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace Essensplan.Web.Services;

public class RecipeService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public RecipeService(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<List<Recipe>> GetAllAsync(int householdId, string? search = null, int? categoryId = null)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var query = db.Recipes
            .Where(r => r.HouseholdId == householdId)
            .Include(r => r.Category)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(r => EF.Functions.Like(r.Name, $"%{term}%")
                                   || (r.Description != null && EF.Functions.Like(r.Description, $"%{term}%")));
        }

        if (categoryId.HasValue)
            query = query.Where(r => r.CategoryId == categoryId);

        return await query.OrderBy(r => r.Name).ToListAsync();
    }

    public async Task<Recipe?> GetByIdAsync(int id, int householdId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Recipes
            .Where(r => r.Id == id && r.HouseholdId == householdId)
            .Include(r => r.Category)
            .Include(r => r.Ingredients.OrderBy(i => i.SortOrder))
            .FirstOrDefaultAsync();
    }

    public async Task<Recipe> CreateAsync(Recipe recipe, int householdId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        recipe.HouseholdId = householdId;
        recipe.CreatedAt = DateTime.UtcNow;
        for (var i = 0; i < recipe.Ingredients.Count; i++)
            recipe.Ingredients[i].SortOrder = i;

        db.Recipes.Add(recipe);
        await db.SaveChangesAsync();

        // Auto-create a matching menu if none with the same name exists in this household
        var menuExists = await db.Menus.AnyAsync(m => m.Name == recipe.Name && m.HouseholdId == householdId);
        if (!menuExists)
        {
            db.Menus.Add(new Menu
            {
                Name = recipe.Name,
                HouseholdId = householdId,
                AllowedMealTypes = await GuessMealTypeFlagsAsync(db, recipe.CategoryId),
                CreatedAt = DateTime.UtcNow,
                MenuRecipes = [new MenuRecipe { RecipeId = recipe.Id, SortOrder = 0 }]
            });
            await db.SaveChangesAsync();
        }

        return recipe;
    }

    private static async Task<int> GuessMealTypeFlagsAsync(AppDbContext db, int? categoryId)
    {
        if (categoryId is null) return MealTypeFlags.Mittagessen | MealTypeFlags.Abendessen;

        var name = await db.RecipeCategories
            .Where(c => c.Id == categoryId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync() ?? "";

        if (name.Contains("rühstück", StringComparison.OrdinalIgnoreCase)) return MealTypeFlags.Fruehstueck;
        if (name.Contains("snack", StringComparison.OrdinalIgnoreCase)) return MealTypeFlags.Snack;
        return MealTypeFlags.Mittagessen | MealTypeFlags.Abendessen;
    }

    public async Task UpdateAsync(Recipe recipe, int householdId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var existing = await db.Recipes
            .Include(r => r.Ingredients)
            .FirstOrDefaultAsync(r => r.Id == recipe.Id && r.HouseholdId == householdId);

        if (existing is null) return;

        existing.Name = recipe.Name;
        existing.Description = recipe.Description;
        existing.Instructions = recipe.Instructions;
        existing.PrepTimeMinutes = recipe.PrepTimeMinutes;
        existing.CookTimeMinutes = recipe.CookTimeMinutes;
        existing.Servings = recipe.Servings;
        existing.ImageUrl = recipe.ImageUrl;
        existing.SourceUrl = recipe.SourceUrl;
        existing.CategoryId = recipe.CategoryId;

        db.RecipeIngredients.RemoveRange(existing.Ingredients);
        existing.Ingredients = recipe.Ingredients
            .Select((ing, i) => new RecipeIngredient
            {
                Name = ing.Name,
                Quantity = ing.Quantity,
                Unit = ing.Unit,
                SortOrder = i
            })
            .ToList();

        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id, int householdId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var recipe = await db.Recipes.FirstOrDefaultAsync(r => r.Id == id && r.HouseholdId == householdId);
        if (recipe is null) return;
        db.Recipes.Remove(recipe);
        await db.SaveChangesAsync();
    }

    public async Task<int> CountAsync(int householdId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Recipes.CountAsync(r => r.HouseholdId == householdId);
    }
}
