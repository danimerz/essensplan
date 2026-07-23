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

    public async Task<List<Recipe>> GetAllAsync(string? search = null, int? categoryId = null)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var query = db.Recipes.Include(r => r.Category).AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(r => EF.Functions.Like(r.Name, $"%{term}%")
                                   || (r.Description != null && EF.Functions.Like(r.Description, $"%{term}%")));
        }

        if (categoryId.HasValue)
        {
            query = query.Where(r => r.CategoryId == categoryId);
        }

        return await query.OrderBy(r => r.Name).ToListAsync();
    }

    public async Task<Recipe?> GetByIdAsync(int id)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Recipes
            .Include(r => r.Category)
            .Include(r => r.Ingredients.OrderBy(i => i.SortOrder))
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<Recipe> CreateAsync(Recipe recipe)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        recipe.CreatedAt = DateTime.UtcNow;
        for (var i = 0; i < recipe.Ingredients.Count; i++)
        {
            recipe.Ingredients[i].SortOrder = i;
        }
        db.Recipes.Add(recipe);
        await db.SaveChangesAsync();
        return recipe;
    }

    public async Task UpdateAsync(Recipe recipe)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var existing = await db.Recipes
            .Include(r => r.Ingredients)
            .FirstOrDefaultAsync(r => r.Id == recipe.Id);

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

    public async Task DeleteAsync(int id)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var recipe = await db.Recipes.FindAsync(id);
        if (recipe is null) return;
        db.Recipes.Remove(recipe);
        await db.SaveChangesAsync();
    }

    public async Task<int> CountAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Recipes.CountAsync();
    }
}
