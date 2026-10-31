using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Essensplan.Web.Services;

/// <summary>
/// Mailversand über SMTP (Abschnitt „Smtp“ wie bei flomiapp: Host, Port, Username, Password, FromAddress,
/// FromName, EnableSsl). Port 465 = TLS direkt, sonst STARTTLS wenn verfügbar.
/// </summary>
public class MailService(IConfiguration config, ILogger<MailService> log)
{
    private IConfigurationSection Smtp => config.GetSection("Smtp");

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Smtp["Host"]) && !string.IsNullOrWhiteSpace(Smtp["FromAddress"]);

    public async Task SendAsync(string toAddress, string toName, string subject, string text, string html)
    {
        if (!IsConfigured) throw new InvalidOperationException("SMTP ist nicht konfiguriert (Smtp__Host, Smtp__FromAddress …).");

        var msg = new MimeMessage();
        msg.From.Add(new MailboxAddress(Smtp["FromName"] ?? "Essensplan", Smtp["FromAddress"]));
        msg.To.Add(new MailboxAddress(toName, toAddress));
        msg.Subject = subject;
        msg.Body = new BodyBuilder { TextBody = text, HtmlBody = html }.ToMessageBody();

        var port = Smtp.GetValue("Port", 465);
        var ssl = Smtp.GetValue("EnableSsl", true);
        var options = !ssl ? SecureSocketOptions.None : port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;

        using var client = new SmtpClient();
        client.Timeout = 20000;
        await client.ConnectAsync(Smtp["Host"], port, options);
        if (!string.IsNullOrEmpty(Smtp["Username"]))
            await client.AuthenticateAsync(Smtp["Username"], Smtp["Password"]);
        await client.SendAsync(msg);
        await client.DisconnectAsync(true);
        log.LogInformation("Mail „{Subject}“ an {To} gesendet.", subject, toAddress);
    }
}
