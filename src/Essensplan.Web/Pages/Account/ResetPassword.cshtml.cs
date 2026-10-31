using System.Text;
using Essensplan.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace Essensplan.Web.Pages.Account;

public class ResetPasswordModel(UserManager<AppUser> users) : PageModel
{
    [BindProperty(SupportsGet = true)] public string Email { get; set; } = "";
    [BindProperty(SupportsGet = true)] public string Code { get; set; } = "";
    [BindProperty] public string Password { get; set; } = "";
    [BindProperty] public string ConfirmPassword { get; set; } = "";
    public string? ErrorMessage { get; set; }
    public bool Done { get; set; }

    public async Task<IActionResult> OnPostAsync()
    {
        if (Password.Length < 8) { ErrorMessage = "Das Passwort braucht mindestens 8 Zeichen."; return Page(); }
        if (Password != ConfirmPassword) { ErrorMessage = "Die Passwörter stimmen nicht überein."; return Page(); }

        var user = await users.FindByEmailAsync(Email.Trim());
        string token;
        try { token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(Code)); }
        catch (FormatException) { ErrorMessage = "Der Link ist ungültig."; return Page(); }

        if (user is null) { ErrorMessage = "Der Link ist ungültig oder abgelaufen."; return Page(); }
        var result = await users.ResetPasswordAsync(user, token, Password);
        if (!result.Succeeded)
        {
            ErrorMessage = result.Errors.Any(e => e.Code == "InvalidToken")
                ? "Der Link ist ungültig oder abgelaufen. Bitte einen neuen anfordern."
                : string.Join(" ", result.Errors.Select(e => e.Description));
            return Page();
        }
        await users.SetLockoutEndDateAsync(user, null);
        Done = true;
        return Page();
    }
}
