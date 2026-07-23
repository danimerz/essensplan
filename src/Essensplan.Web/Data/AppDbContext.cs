using Essensplan.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace Essensplan.Web.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<RecipeCategory> RecipeCategories => Set<RecipeCategory>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();
    public DbSet<Menu> Menus => Set<Menu>();
    public DbSet<MenuRecipe> MenuRecipes => Set<MenuRecipe>();
    public DbSet<WeekPlan> WeekPlans => Set<WeekPlan>();
    public DbSet<WeekPlanEntry> WeekPlanEntries => Set<WeekPlanEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RecipeCategory>(entity =>
        {
            entity.HasIndex(c => c.Name).IsUnique();
        });

        modelBuilder.Entity<Recipe>(entity =>
        {
            entity.HasOne(r => r.Category)
                  .WithMany(c => c.Recipes)
                  .HasForeignKey(r => r.CategoryId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(r => r.Name);
        });

        modelBuilder.Entity<RecipeIngredient>(entity =>
        {
            entity.HasOne(i => i.Recipe)
                  .WithMany(r => r.Ingredients)
                  .HasForeignKey(i => i.RecipeId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.Property(i => i.Quantity).HasPrecision(10, 2);
        });

        modelBuilder.Entity<MenuRecipe>(entity =>
        {
            entity.HasOne(mr => mr.Menu)
                  .WithMany(m => m.MenuRecipes)
                  .HasForeignKey(mr => mr.MenuId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(mr => mr.Recipe)
                  .WithMany(r => r.MenuRecipes)
                  .HasForeignKey(mr => mr.RecipeId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WeekPlan>(entity =>
        {
            entity.HasIndex(w => w.StartDate).IsUnique();
        });

        modelBuilder.Entity<WeekPlanEntry>(entity =>
        {
            entity.HasOne(e => e.WeekPlan)
                  .WithMany(w => w.Entries)
                  .HasForeignKey(e => e.WeekPlanId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Menu)
                  .WithMany(m => m.WeekPlanEntries)
                  .HasForeignKey(e => e.MenuId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => new { e.WeekPlanId, e.Date, e.MealType }).IsUnique();
        });

        modelBuilder.Entity<RecipeCategory>().HasData(
            new RecipeCategory { Id = 1, Name = "Hauptgericht", Icon = "🍝", Color = "#FF7A59", SortOrder = 1 },
            new RecipeCategory { Id = 2, Name = "Vorspeise", Icon = "🥗", Color = "#4ECDC4", SortOrder = 2 },
            new RecipeCategory { Id = 3, Name = "Beilage", Icon = "🥔", Color = "#FFD166", SortOrder = 3 },
            new RecipeCategory { Id = 4, Name = "Dessert", Icon = "🍰", Color = "#C77DFF", SortOrder = 4 },
            new RecipeCategory { Id = 5, Name = "Frühstück", Icon = "🥐", Color = "#F4A261", SortOrder = 5 },
            new RecipeCategory { Id = 6, Name = "Suppe", Icon = "🍲", Color = "#6C63FF", SortOrder = 6 },
            new RecipeCategory { Id = 7, Name = "Snack", Icon = "🍿", Color = "#06D6A0", SortOrder = 7 }
        );
    }
}
