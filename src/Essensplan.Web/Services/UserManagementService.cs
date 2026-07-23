using Essensplan.Web.Data;
using Essensplan.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Essensplan.Web.Services;

public record HouseholdMemberDto(
    string UserId,
    string Email,
    string DisplayName,
    HouseholdRole Role,
    bool IsActive,
    DateTime JoinedAt);

public class UserManagementService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly UserManager<AppUser> _userManager;

    public UserManagementService(IDbContextFactory<AppDbContext> dbFactory, UserManager<AppUser> userManager)
    {
        _dbFactory = dbFactory;
        _userManager = userManager;
    }

    public async Task<List<HouseholdMemberDto>> GetMembersAsync(int householdId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.HouseholdMemberships
            .Where(m => m.HouseholdId == householdId)
            .Include(m => m.User)
            .OrderBy(m => m.User.DisplayName)
            .Select(m => new HouseholdMemberDto(
                m.UserId,
                m.User.Email ?? "",
                m.User.DisplayName,
                m.Role,
                m.User.IsActive,
                m.JoinedAt))
            .ToListAsync();
    }

    public async Task<(bool Success, string Error)> InviteUserAsync(
        int householdId, string email, string displayName, HouseholdRole role, string initialPassword)
    {
        var existingUser = await _userManager.FindByEmailAsync(email);
        if (existingUser is not null)
        {
            // Add to household if not already a member
            await using var db2 = await _dbFactory.CreateDbContextAsync();
            var alreadyMember = await db2.HouseholdMemberships
                .AnyAsync(m => m.HouseholdId == householdId && m.UserId == existingUser.Id);
            if (alreadyMember)
                return (false, "Dieser Benutzer ist bereits Mitglied des Haushalts.");

            db2.HouseholdMemberships.Add(new HouseholdMembership
            {
                HouseholdId = householdId,
                UserId = existingUser.Id,
                Role = role,
                JoinedAt = DateTime.UtcNow
            });
            await db2.SaveChangesAsync();
            return (true, string.Empty);
        }

        var user = new AppUser
        {
            UserName = email,
            Email = email,
            DisplayName = displayName,
            EmailConfirmed = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, initialPassword);
        if (!result.Succeeded)
            return (false, string.Join("; ", result.Errors.Select(e => e.Description)));

        await using var db = await _dbFactory.CreateDbContextAsync();
        db.HouseholdMemberships.Add(new HouseholdMembership
        {
            HouseholdId = householdId,
            UserId = user.Id,
            Role = role,
            JoinedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        return (true, string.Empty);
    }

    public async Task<bool> SetActiveAsync(string userId, bool isActive)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return false;
        user.IsActive = isActive;
        var result = await _userManager.UpdateAsync(user);
        return result.Succeeded;
    }

    public async Task<bool> SetRoleAsync(int householdId, string userId, HouseholdRole role)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var membership = await db.HouseholdMemberships
            .FirstOrDefaultAsync(m => m.HouseholdId == householdId && m.UserId == userId);
        if (membership is null) return false;
        membership.Role = role;
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveFromHouseholdAsync(int householdId, string userId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var membership = await db.HouseholdMemberships
            .FirstOrDefaultAsync(m => m.HouseholdId == householdId && m.UserId == userId);
        if (membership is null) return false;
        db.HouseholdMemberships.Remove(membership);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<(bool Success, string Error)> ChangePasswordAsync(string userId, string currentPassword, string newPassword)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return (false, "Benutzer nicht gefunden.");
        var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        return result.Succeeded ? (true, string.Empty) : (false, string.Join("; ", result.Errors.Select(e => e.Description)));
    }
}
