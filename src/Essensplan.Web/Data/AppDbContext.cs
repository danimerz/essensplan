using Essensplan.Web.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Essensplan.Web.Data;

public class AppDbContext : IdentityDbContext<AppUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Household> Households => Set<Household>();
    public DbSet<HouseholdMembership> HouseholdMemberships => Set<HouseholdMembership>();
    public DbSet<HouseholdRecipe> HouseholdRecipes => Set<HouseholdRecipe>();
    public DbSet<RecipeCategory> RecipeCategories => Set<RecipeCategory>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();
    public DbSet<Menu> Menus => Set<Menu>();
    public DbSet<MenuRecipe> MenuRecipes => Set<MenuRecipe>();
    public DbSet<WeekPlan> WeekPlans => Set<WeekPlan>();
    public DbSet<WeekPlanEntry> WeekPlanEntries => Set<WeekPlanEntry>();
    public DbSet<RecipeRating> RecipeRatings => Set<RecipeRating>();
    public DbSet<ShoppingListItem> ShoppingListItems => Set<ShoppingListItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<HouseholdMembership>(entity =>
        {
            entity.HasIndex(m => new { m.HouseholdId, m.UserId }).IsUnique();
            entity.HasOne(m => m.Household).WithMany(h => h.Memberships)
                  .HasForeignKey(m => m.HouseholdId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(m => m.User).WithMany(u => u.HouseholdMemberships)
                  .HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<HouseholdRecipe>(entity =>
        {
            entity.HasKey(hr => new { hr.HouseholdId, hr.RecipeId });
            entity.HasOne(hr => hr.Household).WithMany(h => h.HouseholdRecipes)
                  .HasForeignKey(hr => hr.HouseholdId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(hr => hr.Recipe).WithMany(r => r.HouseholdRecipes)
                  .HasForeignKey(hr => hr.RecipeId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RecipeCategory>(entity =>
        {
            entity.HasIndex(c => c.Name).IsUnique();
        });

        modelBuilder.Entity<Recipe>(entity =>
        {
            entity.HasOne(r => r.Category).WithMany(c => c.Recipes)
                  .HasForeignKey(r => r.CategoryId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(r => r.Name);
        });

        modelBuilder.Entity<RecipeIngredient>(entity =>
        {
            entity.HasOne(i => i.Recipe).WithMany(r => r.Ingredients)
                  .HasForeignKey(i => i.RecipeId).OnDelete(DeleteBehavior.Cascade);
            entity.Property(i => i.Quantity).HasPrecision(10, 2);
        });

        modelBuilder.Entity<MenuRecipe>(entity =>
        {
            entity.HasOne(mr => mr.Menu).WithMany(m => m.MenuRecipes)
                  .HasForeignKey(mr => mr.MenuId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(mr => mr.Recipe).WithMany(r => r.MenuRecipes)
                  .HasForeignKey(mr => mr.RecipeId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Menu>(entity =>
        {
            entity.HasOne(m => m.Household).WithMany(h => h.Menus)
                  .HasForeignKey(m => m.HouseholdId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WeekPlan>(entity =>
        {
            entity.HasIndex(w => new { w.HouseholdId, w.StartDate }).IsUnique();
            entity.HasOne(w => w.Household).WithMany(h => h.WeekPlans)
                  .HasForeignKey(w => w.HouseholdId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WeekPlanEntry>(entity =>
        {
            entity.HasOne(e => e.WeekPlan).WithMany(w => w.Entries)
                  .HasForeignKey(e => e.WeekPlanId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Menu).WithMany(m => m.WeekPlanEntries)
                  .HasForeignKey(e => e.MenuId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(e => new { e.WeekPlanId, e.Date, e.MealType }).IsUnique();
        });

        modelBuilder.Entity<RecipeRating>(entity =>
        {
            entity.HasIndex(r => new { r.RecipeId, r.UserId }).IsUnique();
            entity.HasOne(r => r.Recipe).WithMany(rec => rec.Ratings)
                  .HasForeignKey(r => r.RecipeId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(r => r.User).WithMany(u => u.Ratings)
                  .HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ShoppingListItem>(entity =>
        {
            entity.HasOne(i => i.Household).WithMany(h => h.ShoppingListItems)
                  .HasForeignKey(i => i.HouseholdId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(i => new { i.HouseholdId, i.IsDone });
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
