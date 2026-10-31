using System.Collections.Concurrent;
using System.Text;
using System.Text.Encodings.Web;
using Essensplan.Web.Models;
using Essensplan.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace Essensplan.Web.Pages.Account;

public class ForgotPasswordModel(UserManager<AppUser> users, MailService mail, ILogger<ForgotPasswordModel> log) : PageModel
{
    // Einfache Bremse: max. 5 Anfragen pro IP in 15 Minuten
    private static readonly ConcurrentDictionary<string, List<DateTime>> Attempts = new();

    [BindProperty] public string Email { get; set; } = "";
    public bool Sent { get; set; }
    public string? ErrorMessage { get; set; }
    public bool MailAvailable => mail.IsConfigured;

    public void OnGet(string? email) => Email = email ?? "";

    public async Task<IActionResult> OnPostAsync()
    {
        if (!MailAvailable) return Page();
        if (string.IsNullOrWhiteSpace(Email)) { ErrorMessage = "Bitte E-Mail eingeben."; return Page(); }

        var ip = Request.Headers["Cf-Connecting-Ip"].FirstOrDefault()
                 ?? HttpContext.Connection.RemoteIpAddress?.ToString() ?? "?";
        var now = DateTime.UtcNow;
        var list = Attempts.GetOrAdd(ip, _ => []);
        lock (list)
        {
            list.RemoveAll(t => t < now.AddMinutes(-15));
            if (list.Count >= 5) { ErrorMessage = "Zu viele Anfragen. Bitte in 15 Minuten nochmals versuchen."; return Page(); }
            list.Add(now);
        }

        var user = await users.FindByEmailAsync(Email.Trim());
        if (user is not null && user.IsActive)
        {
            var code = await users.GeneratePasswordResetTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
            var link = $"{Request.Scheme}://{Request.Host}/passwort-zuruecksetzen?email={Uri.EscapeDataString(user.Email!)}&code={code}";
            var name = string.IsNullOrWhiteSpace(user.DisplayName) ? user.Email! : user.DisplayName;
            try
            {
                await mail.SendAsync(user.Email!, name, "Essensplan: Passwort zurücksetzen",
                    $"Hallo {name}\n\nÜber diesen Link kannst du ein neues Passwort setzen (1 Tag gültig):\n{link}\n\nFalls du das nicht angefordert hast, kannst du diese Mail ignorieren.",
                    $"<p>Hallo {HtmlEncoder.Default.Encode(name)}</p><p>Über diesen Link kannst du ein neues Passwort setzen (1 Tag gültig):</p><p><a href=\"{HtmlEncoder.Default.Encode(link)}\">Neues Passwort setzen</a></p><p style=\"color:#777\">Falls du das nicht angefordert hast, kannst du diese Mail ignorieren.</p>");
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Mail zum Zurücksetzen konnte nicht gesendet werden.");
                ErrorMessage = "Die Mail konnte nicht gesendet werden. Bitte später nochmals versuchen.";
                return Page();
            }
        }
        // Immer dieselbe Antwort – verrät nicht, ob es das Konto gibt
        Sent = true;
        return Page();
    }
}
