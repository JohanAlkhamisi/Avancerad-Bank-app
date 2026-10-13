namespace bank
{
    internal class CustomerSupport
    {
        // Här visar jag hjälp och låter en inloggad kund registrera ett ärende.
        public static void ShowSupportMenu(Bank bank, Kund? customer)
        {
            while (true)
            {
                Menu.ShowPage("KUNDSERVICE");

                Console.WriteLine(
                    "1. Överföringar\n" +
                    "2. Insättningar och uttag\n" +
                    "3. Lån och sparkonton\n" +
                    "4. Registrera supportärende\n" +
                    "0. Tillbaka");

                string choice =
                    Menu.ReadMenuChoice("1", "2", "3", "4", "0");

                if (choice == "0")
                {
                    return;
                }

                if (choice == "1")
                {
                    Console.WriteLine(
                        "Välj avsändarkonto, skriv mottagarens kontonummer " +
                        "och belopp i avsändarkontots valuta.\n" +
                        "Kontrollera och bekräfta. Pengarna flyttas vid nästa kvart. " +
                        "Administratören uppdaterar valutakurser dagligen.");
                }

                if (choice == "2")
                {
                    Console.WriteLine(
                        "Välj Bankomat, konto och belopp. Efter bekräftelse " +
                        "väntar transaktionen till nästa kvart.\n" +
                        "Ett väntande uttag reserverar pengar. Du kan avbryta " +
                        "i menyn för väntande transaktioner.");
                }

                if (choice == "3")
                {
                    Console.WriteLine(
                        "Sparkontot visar beräknad ränta för ett år. " +
                        "Lån visar också ett års ränta och betalas ut i SEK.\n" +
                        "Lånegränsen bygger på dina egna pengar, utan lånade pengar. " +
                        "Räntan beror på din kreditvärdighet.");
                }

                if (choice == "4")
                {
                    if (customer == null)
                    {
                        Console.WriteLine(
                            "Logga in först för att registrera ett supportärende.");
                    }
                    else
                    {
                        string description = Menu.ReadRequiredText(
                            "Beskriv ärendet utan lösenord (0 avbryter): ");

                        if (description != "")
                        {
                            Console.WriteLine(
                                bank.AddSupportTicket(customer, description)
                                    ? "Ditt ärende är registrerat."
                                    : "Ärendet kunde inte registreras.");
                        }
                    }
                }

                Menu.Pause();
            }
        }
    }
}