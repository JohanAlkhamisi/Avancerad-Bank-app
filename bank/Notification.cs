using System.Net;
using System.Net.Mail;

namespace bank
{
    internal class Notification
    {
        // Här sparar jag e-postmeddelandet tills det har skickats.
        public int NotificationNumber { get; set; }
        public int CustomerNumber { get; set; }
        public string EmailAddress { get; set; } = "";
        public string Subject { get; set; } =
            "Stor transaktion på ditt konto";
        public string Message { get; set; } = "";

        // Här håller jag koll på om meddelandet är skickat och antalet försök.
        public bool IsSent { get; set; }
        public int Attempts { get; set; }
        public DateTime LastAttemptAt { get; set; }
        public string Error { get; set; } = "";

        // Här använder jag bankens e-postserver utan att skriva lösenord i koden.
        public bool TrySendEmail(out string error)
        {
            // Här hämtar jag e-postserverns inställningar från datorns miljövariabler.
            string host =
                Environment.GetEnvironmentVariable("GLOBEN_SMTP_HOST") ?? "";

            string from =
                Environment.GetEnvironmentVariable("GLOBEN_SMTP_FROM") ?? "";

            error = "";

            // Här stoppar jag leveransen om banken inte har en server
            // och en avsändaradress.
            if (host == "" || !MailAddress.TryCreate(from, out _))
            {
                error = "Bankens e-postserver är inte konfigurerad.";
                return false;
            }

            try
            {
                int port = int.TryParse(
                    Environment.GetEnvironmentVariable("GLOBEN_SMTP_PORT"),
                    out int value)
                    ? value
                    : 587;

                // Här öppnar jag anslutningen till e-postservern.
                using SmtpClient client = new SmtpClient(host, port);

                client.EnableSsl =
                    Environment.GetEnvironmentVariable("GLOBEN_SMTP_SSL") != "false";

                client.Timeout = 10000;

                string username =
                    Environment.GetEnvironmentVariable("GLOBEN_SMTP_USER") ?? "";

                if (username != "")
                {
                    client.Credentials = new NetworkCredential(
                        username,
                        Environment.GetEnvironmentVariable("GLOBEN_SMTP_PASSWORD"));
                }

                // Här skapar jag meddelandet och skickar det till kundens e-postadress.
                using MailMessage mail =
                    new MailMessage(from, EmailAddress, Subject, Message);

                client.Send(mail);

                return true;
            }
            catch (Exception exception) when (
                exception is SmtpException ||
                exception is ArgumentException ||
                exception is InvalidOperationException)
            {
                // Här lämnar jag tillbaka ett fel så Bank kan försöka igen senare.
                error =
                    "E-postleveransen misslyckades. Banken försöker igen senare.";

                return false;
            }
        }
    }
}