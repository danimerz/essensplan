using Essensplan.Web.Data;
using Essensplan.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace Essensplan.Web.Services;

public class RecipeRatingService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public RecipeRatingService(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<List<RecipeRating>> GetForRecipeAsync(int recipeId, int householdId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.RecipeRatings
            .Where(r => r.RecipeId == recipeId && r.HouseholdId == householdId)
            .Include(r => r.User)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<RecipeRating?> GetMyRatingAsync(int recipeId, string userId, int householdId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.RecipeRatings
            .FirstOrDefaultAsync(r => r.RecipeId == recipeId && r.UserId == userId && r.HouseholdId == householdId);
    }

    public async Task UpsertAsync(int recipeId, string userId, int householdId, int stars, string? comment)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var existing = await db.RecipeRatings
            .FirstOrDefaultAsync(r => r.RecipeId == recipeId && r.UserId == userId);

        if (existing is null)
        {
            db.RecipeRatings.Add(new RecipeRating
            {
                RecipeId = recipeId,
                UserId = userId,
                HouseholdId = householdId,
                Stars = stars,
                Comment = comment,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.Stars = stars;
            existing.Comment = comment;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int ratingId, string userId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var rating = await db.RecipeRatings
            .FirstOrDefaultAsync(r => r.Id == ratingId && r.UserId == userId);
        if (rating is null) return;
        db.RecipeRatings.Remove(rating);
        await db.SaveChangesAsync();
    }

    public async Task<(double Average, int Count)> GetStatsAsync(int recipeId, int householdId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var ratings = await db.RecipeRatings
            .Where(r => r.RecipeId == recipeId && r.HouseholdId == householdId)
            .Select(r => r.Stars)
            .ToListAsync();

        if (ratings.Count == 0) return (0, 0);
        return (ratings.Average(), ratings.Count);
    }
}
