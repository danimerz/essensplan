using Essensplan.Web.Data;
using Essensplan.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace Essensplan.Web.Services;

public class CategoryService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public CategoryService(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<List<RecipeCategory>> GetAllAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.RecipeCategories.OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ToListAsync();
    }

    public async Task<RecipeCategory?> GetByIdAsync(int id)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.RecipeCategories.FindAsync(id);
    }

    public async Task<RecipeCategory> CreateAsync(RecipeCategory category)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        db.RecipeCategories.Add(category);
        await db.SaveChangesAsync();
        return category;
    }

    public async Task UpdateAsync(RecipeCategory category)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var existing = await db.RecipeCategories.FindAsync(category.Id);
        if (existing is null) return;

        existing.Name = category.Name;
        existing.Icon = category.Icon;
        existing.Color = category.Color;
        existing.SortOrder = category.SortOrder;

        await db.SaveChangesAsync();
    }

    public async Task<bool> DeleteAsync(int id)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var category = await db.RecipeCategories.FindAsync(id);
        if (category is null) return false;

        var inUse = await db.Recipes.AnyAsync(r => r.CategoryId == id);
        if (inUse) return false;

        db.RecipeCategories.Remove(category);
        await db.SaveChangesAsync();
        return true;
    }
}
