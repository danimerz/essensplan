using Essensplan.Web.Data;
using Essensplan.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace Essensplan.Web.Services;

public record GlobalRecipeMatch(Recipe Recipe, int HouseholdCount, int IngredientCount);

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
            .Where(r => r.HouseholdRecipes.Any(hr => hr.HouseholdId == householdId))
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

    public async Task<HashSet<int>> GetSharedRecipeIdsAsync(int householdId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var myRecipeIds = await db.HouseholdRecipes
            .Where(hr => hr.HouseholdId == householdId)
            .Select(hr => hr.RecipeId)
            .ToListAsync();

        var shared = await db.HouseholdRecipes
            .Where(hr => myRecipeIds.Contains(hr.RecipeId) && hr.HouseholdId != householdId)
            .Select(hr => hr.RecipeId)
            .Distinct()
            .ToListAsync();

        return shared.ToHashSet();
    }

    public async Task<Recipe?> GetByIdAsync(int id, int householdId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Recipes
            .Where(r => r.Id == id && r.HouseholdRecipes.Any(hr => hr.HouseholdId == householdId))
            .Include(r => r.Category)
            .Include(r => r.Ingredients.OrderBy(i => i.SortOrder))
            .FirstOrDefaultAsync();
    }

    public async Task<int> GetShareCountAsync(int recipeId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.HouseholdRecipes.CountAsync(hr => hr.RecipeId == recipeId);
    }

    public async Task<Recipe?> FindGlobalByUrlAsync(string url)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Recipes
            .Include(r => r.Category)
            .FirstOrDefaultAsync(r => r.SourceUrl == url);
    }

    public async Task<List<GlobalRecipeMatch>> FindGlobalMatchesByNameAsync(string name)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var normalized = name.Trim();

        var recipes = await db.Recipes
            .Where(r => EF.Functions.Like(r.Name, normalized))
            .Include(r => r.Category)
            .Include(r => r.Ingredients)
            .ToListAsync();

        if (recipes.Count == 0) return [];

        var ids = recipes.Select(r => r.Id).ToList();
        var counts = await db.HouseholdRecipes
            .Where(hr => ids.Contains(hr.RecipeId))
            .GroupBy(hr => hr.RecipeId)
            .Select(g => new { RecipeId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.RecipeId, x => x.Count);

        return recipes
            .Select(r => new GlobalRecipeMatch(r, counts.GetValueOrDefault(r.Id, 0), r.Ingredients.Count))
            .OrderByDescending(m => m.HouseholdCount)
            .ToList();
    }

    public async Task<Recipe> CreateAsync(Recipe recipe, int householdId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        recipe.CreatedAt = DateTime.UtcNow;
        for (var i = 0; i < recipe.Ingredients.Count; i++)
            recipe.Ingredients[i].SortOrder = i;

        db.Recipes.Add(recipe);
        await db.SaveChangesAsync();

        db.HouseholdRecipes.Add(new HouseholdRecipe
        {
            HouseholdId = householdId,
            RecipeId = recipe.Id,
            AddedAt = DateTime.UtcNow
        });

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
        }

        await db.SaveChangesAsync();
        return recipe;
    }

    public async Task AssignToHouseholdAsync(int recipeId, int householdId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var already = await db.HouseholdRecipes
            .AnyAsync(hr => hr.RecipeId == recipeId && hr.HouseholdId == householdId);
        if (already) return;

        db.HouseholdRecipes.Add(new HouseholdRecipe
        {
            HouseholdId = householdId,
            RecipeId = recipeId,
            AddedAt = DateTime.UtcNow
        });

        var recipe = await db.Recipes.FindAsync(recipeId);
        if (recipe is not null)
        {
            var menuExists = await db.Menus.AnyAsync(m => m.Name == recipe.Name && m.HouseholdId == householdId);
            if (!menuExists)
            {
                db.Menus.Add(new Menu
                {
                    Name = recipe.Name,
                    HouseholdId = householdId,
                    AllowedMealTypes = await GuessMealTypeFlagsAsync(db, recipe.CategoryId),
                    CreatedAt = DateTime.UtcNow,
                    MenuRecipes = [new MenuRecipe { RecipeId = recipeId, SortOrder = 0 }]
                });
            }
        }

        await db.SaveChangesAsync();
    }

    // Returns the ID of the saved recipe (may differ from input if recipe was forked)
    public async Task<int> UpdateAsync(Recipe recipe, int householdId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var shareCount = await db.HouseholdRecipes.CountAsync(hr => hr.RecipeId == recipe.Id);

        if (shareCount > 1)
        {
            // Fork: create a copy for this household
            var original = await db.Recipes
                .Include(r => r.Ingredients)
                .FirstOrDefaultAsync(r => r.Id == recipe.Id);
            if (original is null) return recipe.Id;

            var fork = new Recipe
            {
                Name = recipe.Name,
                Description = recipe.Description,
                Instructions = recipe.Instructions,
                PrepTimeMinutes = recipe.PrepTimeMinutes,
                CookTimeMinutes = recipe.CookTimeMinutes,
                Servings = recipe.Servings,
                ImageUrl = recipe.ImageUrl,
                SourceUrl = original.SourceUrl,
                CategoryId = recipe.CategoryId,
                CreatedAt = DateTime.UtcNow,
                Ingredients = recipe.Ingredients
                    .Select((ing, i) => new RecipeIngredient
                    {
                        Name = ing.Name,
                        Quantity = ing.Quantity,
                        Unit = ing.Unit,
                        SortOrder = i
                    })
                    .ToList()
            };

            db.Recipes.Add(fork);
            await db.SaveChangesAsync();

            // Reassign household from original to fork
            var oldAssignment = await db.HouseholdRecipes
                .FirstOrDefaultAsync(hr => hr.RecipeId == recipe.Id && hr.HouseholdId == householdId);
            if (oldAssignment is not null) db.HouseholdRecipes.Remove(oldAssignment);

            db.HouseholdRecipes.Add(new HouseholdRecipe
            {
                HouseholdId = householdId,
                RecipeId = fork.Id,
                AddedAt = DateTime.UtcNow
            });

            // Update any menus in this household that referenced the original recipe
            var menuRecipesToUpdate = await db.MenuRecipes
                .Where(mr => mr.RecipeId == recipe.Id)
                .Join(db.Menus.Where(m => m.HouseholdId == householdId),
                      mr => mr.MenuId, m => m.Id, (mr, m) => mr)
                .ToListAsync();
            foreach (var mr in menuRecipesToUpdate)
                mr.RecipeId = fork.Id;

            await db.SaveChangesAsync();
            return fork.Id;
        }
        else
        {
            // Only in this household — update in place
            var existing = await db.Recipes
                .Include(r => r.Ingredients)
                .FirstOrDefaultAsync(r => r.Id == recipe.Id);
            if (existing is null) return recipe.Id;

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
            return existing.Id;
        }
    }

    public async Task DeleteAsync(int id, int householdId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var assignment = await db.HouseholdRecipes
            .FirstOrDefaultAsync(hr => hr.RecipeId == id && hr.HouseholdId == householdId);
        if (assignment is null) return;

        // Remove from all menus in this household
        var menuRecipes = await db.MenuRecipes
            .Where(mr => mr.RecipeId == id)
            .Join(db.Menus.Where(m => m.HouseholdId == householdId),
                  mr => mr.MenuId, m => m.Id, (mr, _) => mr)
            .ToListAsync();
        db.MenuRecipes.RemoveRange(menuRecipes);

        db.HouseholdRecipes.Remove(assignment);
        await db.SaveChangesAsync();

        // Clean up orphaned recipe
        var stillUsed = await db.HouseholdRecipes.AnyAsync(hr => hr.RecipeId == id);
        if (!stillUsed)
        {
            var recipe = await db.Recipes.FindAsync(id);
            if (recipe is not null) db.Recipes.Remove(recipe);
            await db.SaveChangesAsync();
        }
    }

    public async Task<int> CountAsync(int householdId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.HouseholdRecipes.CountAsync(hr => hr.HouseholdId == householdId);
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
}
