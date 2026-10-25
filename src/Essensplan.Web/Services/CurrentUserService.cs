using System.Security.Claims;
using Essensplan.Web.Data;
using Essensplan.Web.Models;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Essensplan.Web.Services;

/// <summary>Scoped per-circuit service that caches the current user's household and role.</summary>
public class CurrentUserService
{
    private readonly AuthenticationStateProvider _authStateProvider;
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    private string? _userId;
    private string? _email;
    private int? _householdId;
    private string? _householdName;
    private HouseholdRole? _householdRole;
    private bool _isSuperAdmin;

    // Memoizes the in-flight load task itself (not just a "done" flag) so concurrent callers
    // within the same circuit/request (e.g. MainLayout and the page both read this at once)
    // all await the same load instead of one of them observing a "loaded" flag flipped before
    // the data behind it has actually arrived.
    private Task? _loadTask;

    public CurrentUserService(
        AuthenticationStateProvider authStateProvider,
        IDbContextFactory<AppDbContext> dbFactory)
    {
        _authStateProvider = authStateProvider;
        _dbFactory = dbFactory;
    }

    private Task EnsureLoadedAsync() => _loadTask ??= LoadAsync();

    private async Task LoadAsync()
    {
        var state = await _authStateProvider.GetAuthenticationStateAsync();
        var principal = state.User;
        if (principal.Identity?.IsAuthenticated != true) return;

        _userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        _email = principal.FindFirstValue(ClaimTypes.Email);
        _isSuperAdmin = principal.IsInRole("SuperAdmin");

        if (_userId is null) return;

        await using var db = await _dbFactory.CreateDbContextAsync();
        var membership = await db.HouseholdMemberships
            .Where(m => m.UserId == _userId)
            .Include(m => m.Household)
            .FirstOrDefaultAsync();

        _householdId = membership?.HouseholdId;
        _householdName = membership?.Household?.Name;
        _householdRole = membership?.Role;
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        await EnsureLoadedAsync();
        return _userId is not null;
    }

    public async Task<string?> GetUserIdAsync()
    {
        await EnsureLoadedAsync();
        return _userId;
    }

    public async Task<string?> GetEmailAsync()
    {
        await EnsureLoadedAsync();
        return _email;
    }

    public async Task<int?> GetHouseholdIdAsync()
    {
        await EnsureLoadedAsync();
        return _householdId;
    }

    public async Task<string?> GetHouseholdNameAsync()
    {
        await EnsureLoadedAsync();
        return _householdName;
    }

    public async Task<bool> IsAdminAsync()
    {
        await EnsureLoadedAsync();
        return _isSuperAdmin || _householdRole == HouseholdRole.Admin;
    }

    public async Task<bool> IsSuperAdminAsync()
    {
        await EnsureLoadedAsync();
        return _isSuperAdmin;
    }
}
