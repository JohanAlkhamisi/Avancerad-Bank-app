using System.Globalization;
using System.Text;
using System.Text.Json;

namespace bank
{
    internal class Menu
    {
        // Här sparar jag banken som alla menyer använder under den här körningen.
        private static Bank bank = null!;

        // Här ställer jag in svensk talformatering och öppnar bankens sparfil.
        public static void StartMenu()
        {
            CultureInfo.CurrentCulture = new CultureInfo("sv-SE");

            string path =
                Environment.GetEnvironmentVariable("GLOBEN_BANK_DATA_FILE") ??
                Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData),
                    "GlobenBank",
                    "bank.json");

            try
            {
                using (bank = Bank.Load(path))
                {
                    if (bank.Customers.Count == 0)
                    {
                        ShowPage("SKAPA BANKENS FÖRSTA ADMINISTRATÖR");

                        Pause(
                            "Första gången behöver banken en administratör. " +
                            "Tryck på valfri tangent.");

                        if (RegisterCustomer(true, null) == null)
                        {
                            return;
                        }
                    }

                    bank.StartProcessors();

                    while (true)
                    {
                        ShowPage("Välkommen till Globen Bank");

                        Console.WriteLine(
                            "1. Logga in\n" +
                            "2. Bli kund\n" +
                            "3. Kontakta oss\n" +
                            "0. Avsluta");

                        switch (ReadMenuChoice("1", "2", "3", "0"))
                        {
                            case "1":
                                LoginCustomer();
                                break;

                            case "2":
                                RegisterCustomer(false, null);
                                break;

                            case "3":
                                CustomerSupport.ShowSupportMenu(bank, null);
                                break;

                            case "0":
                                return;
                        }
                    }
                }
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is JsonException ||
                exception is FormatException ||
                exception is InvalidDataException)
            {
                Console.WriteLine(
                    "Banken kunde inte öppna eller spara uppgifterna. " +
                    "Kontrollera sparfilen och att appen inte redan körs.");

                Pause("Tryck på valfri tangent för att avsluta.");
            }
            finally
            {
                Console.ResetColor();
            }
        }

        // Här visar jag bankens logga och färger.
        public static void ShowPage(string title)
        {
            if (!Console.IsOutputRedirected)
            {
                Console.Clear();
            }

            Console.ForegroundColor = ConsoleColor.DarkCyan;

            Console.WriteLine(
                "          /\\\n" +
                "         /__\\\n" +
                "        | || |\n" +
                "       _|_||_|_\n" +
                "       GLOBEN BANK");

            Console.ForegroundColor = ConsoleColor.Cyan;

            Console.WriteLine($"\n{title}\n");
        }

        // Här väntar jag så att kunden hinner läsa innan nästa sida visas.
        public static void Pause(
            string message = "Tryck på valfri tangent för att gå tillbaka.")
        {
            Console.WriteLine("\n" + message);

            if (!Console.IsInputRedirected)
            {
                Console.ReadKey(true);
            }
        }

        // Här behandlar jag stängd inmatning som 0
        // så att appen kan gå tillbaka och avsluta.
        public static string ReadInput() =>
            Console.ReadLine() ?? "0";

        // Här godkänner jag bara val som finns i den aktuella menyn.
        public static string ReadMenuChoice(params string[] choices)
        {
            while (true)
            {
                Console.Write("Ange ditt val: ");

                string value = ReadInput().Trim();

                if (choices.Contains(value))
                {
                    return value;
                }

                if (value == "0")
                {
                    return "0";
                }

                Console.WriteLine(
                    "Ogiltigt menyval. Försök igen " +
                    "eller ange 0 för att gå tillbaka.");
            }
        }

        // Här läser jag text, tar bort mellanrum i början och slutet
        // och stoppar tomma svar.
        public static string ReadRequiredText(string prompt)
        {
            while (true)
            {
                Console.Write(prompt);

                string value = ReadInput().Trim();

                if (value == "0")
                {
                    return "";
                }

                if (value != "")
                {
                    return value;
                }

                Console.WriteLine(
                    "Uppgiften får inte vara tom. Ange 0 för att avbryta.");
            }
        }

        // Här behåller jag den gamla uppgiften när kunden bara trycker Enter.
        private static string ReadUpdatedText(
            string prompt,
            string currentValue)
        {
            Console.Write(prompt);

            string value = ReadInput().Trim();

            return value == "0"
                ? ""
                : value == ""
                    ? currentValue
                    : value;
        }

        // Här döljer jag lösenordet och låter kunden använda Backspace.
        public static string ReadPassword()
        {
            if (Console.IsInputRedirected)
            {
                return ReadInput();
            }

            StringBuilder password = new StringBuilder();

            while (true)
            {
                ConsoleKeyInfo key = Console.ReadKey(true);

                if (key.Key == ConsoleKey.Enter)
                {
                    Console.WriteLine();
                    return password.ToString();
                }

                if (key.Key == ConsoleKey.Backspace &&
                    password.Length > 0)
                {
                    password.Length--;
                    Console.Write("\b \b");
                }
                else if (!char.IsControl(key.KeyChar))
                {
                    password.Append(key.KeyChar);
                    Console.Write("*");
                }
            }
        }

        // Här läser jag pengar med komma som decimaltecken
        // och högst två decimaler.
        private static decimal? ReadDecimal(
            string prompt,
            bool allowZero = false,
            bool money = true)
        {
            while (true)
            {
                Console.Write(prompt);

                string input = ReadInput().Trim();

                if (input.Equals(
                        "avbryt",
                        StringComparison.OrdinalIgnoreCase) ||
                    (!allowZero && input == "0"))
                {
                    return null;
                }

                if (decimal.TryParse(
                        input,
                        NumberStyles.AllowDecimalPoint,
                        CultureInfo.CurrentCulture,
                        out decimal amount) &&
                    (allowZero ? amount >= 0 : amount > 0) &&
                    amount <= Bankkonto.MaximumBalance &&
                    (!money || amount == Math.Round(amount, 2)))
                {
                    return amount;
                }

                Console.WriteLine(
                    "Ogiltigt belopp. Använd komma som decimaltecken. " +
                    "Skriv avbryt för att gå tillbaka.");

                if (Console.IsInputRedirected &&
                    input == "0" &&
                    allowZero)
                {
                    return 0;
                }
            }
        }

        // Här kan kunden gå tillbaka, behålla en uppgift
        // och rätta före registreringen.
        private static Kund? RegisterCustomer(
            bool isAdmin,
            Kund? administrator)
        {
            string[] details = new string[6];

            Array.Fill(details, "");

            string[] labels =
            {
                "Användarnamn",
                "Förnamn",
                "Efternamn",
                "E-postadress",
                "Telefonnummer",
                "Lösenord"
            };

            int step = 0;
            bool editing = false;

            while (true)
            {
                if (step < 6)
                {
                    ShowPage(
                        $"REGISTRERA {(isAdmin ? "ADMINISTRATÖR" : "KUND")} " +
                        $"— steg {step + 1} av 6");

                    Console.WriteLine(
                        "Ange 0 för att gå tillbaka. " +
                        "Enter behåller en tidigare uppgift.");

                    if (step == 0)
                    {
                        Console.WriteLine(
                            "Användarnamn: 3–30 tecken, börja med a–z. " +
                            "Bokstäver, siffror, . - _ tillåts.");
                    }

                    if (details[step] != "" && step != 5)
                    {
                        Console.WriteLine(
                            "Nuvarande uppgift: " + details[step]);
                    }

                    Console.Write(labels[step] + ": ");

                    string value = step == 5
                        ? ReadPassword()
                        : ReadInput().Trim();

                    if (value == "0")
                    {
                        if (editing)
                        {
                            step = 6;
                        }
                        else if (step == 0)
                        {
                            return null;
                        }
                        else
                        {
                            step--;
                        }

                        continue;
                    }

                    if (value == "")
                    {
                        value = details[step];
                    }

                    bool valid =
                        !string.IsNullOrWhiteSpace(value);

                    if (step == 0)
                    {
                        valid =
                            Bank.IsValidUsername(value) &&
                            !bank.Customers.Any(customer =>
                                customer.Username.Equals(
                                    value,
                                    StringComparison.OrdinalIgnoreCase));
                    }

                    if (step == 3)
                    {
                        valid = Bank.IsValidEmail(value);
                    }

                    if (step == 4)
                    {
                        valid = Bank.IsValidPhone(value);
                    }

                    if (!valid)
                    {
                        Console.WriteLine(
                            "Uppgiften är ogiltig " +
                            "eller användarnamnet är upptaget.");

                        Pause();

                        continue;
                    }

                    details[step] = value;

                    step = editing ? 6 : step + 1;

                    continue;
                }

                ShowPage("KONTROLLERA UPPGIFTERNA");

                for (int i = 0; i < labels.Length; i++)
                {
                    Console.WriteLine(
                        labels[i] + ": " +
                        (i == 5 ? "valt och dolt" : details[i]));
                }

                Console.WriteLine(
                    "1. Bekräfta\n" +
                    "2. Ändra uppgifter\n" +
                    "0. Avbryt");

                string choice =
                    ReadMenuChoice("1", "2", "0");

                if (choice == "0")
                {
                    return null;
                }

                if (choice == "2")
                {
                    for (int i = 0; i < labels.Length; i++)
                    {
                        Console.WriteLine(
                            $"{i + 1}. {labels[i]}");
                    }

                    Console.WriteLine(
                        "0. Tillbaka till sammanställningen");

                    string field =
                        ReadMenuChoice(
                            "1", "2", "3", "4", "5", "6", "0");

                    if (field != "0")
                    {
                        step = int.Parse(field) - 1;
                        editing = true;
                    }

                    continue;
                }

                Kund? customer = bank.CreateCustomer(
                    details,
                    isAdmin,
                    administrator,
                    out string error);

                Console.WriteLine(
                    customer == null
                        ? error
                        : $"Registreringen är klar. " +
                          $"Kundnummer: {customer.CustomerNumber}. " +
                          $"Användarnamn: {customer.Username}.");

                Pause();

                return customer;
            }
        }

        // Här läser jag kundens inloggning
        // och öppnar rätt meny efter en godkänd inloggning.
        private static void LoginCustomer()
        {
            while (true)
            {
                ShowPage("LOGGA IN");

                string identifier = ReadRequiredText(
                    "Användarnamn eller kundnummer " +
                    "(0 för att gå tillbaka): ");

                if (identifier == "")
                {
                    return;
                }

                Console.Write(
                    "Lösenord (0 för att gå tillbaka): ");

                string password = ReadPassword();

                if (password == "0")
                {
                    return;
                }

                Kund? customer = bank.Login(
                    identifier,
                    password,
                    out string message);

                if (customer != null)
                {
                    if (customer.IsAdmin)
                    {
                        AdminMenu(customer);
                    }
                    else
                    {
                        MainMenu(customer);
                    }

                    return;
                }

                Console.WriteLine(message);

                Pause();
            }
        }

        // Här visar jag kundens val
        // och nästa tid då transaktionerna genomförs.
        public static void MainMenu(Kund customer)
        {
            while (true)
            {
                ShowPage($"Välkommen, {customer.FirstName}!");

                Console.WriteLine(
                    $"Nästa transaktionskörning: " +
                    $"{Bank.NextQuarter(DateTime.UtcNow).ToLocalTime():HH:mm}");

                Console.WriteLine(
                    "1. Kontoöversikt\n" +
                    "2. Överför pengar\n" +
                    "3. Bankomat\n" +
                    "4. Mina uppgifter\n" +
                    "5. Skapa konto\n" +
                    "6. Lån\n" +
                    "7. Väntande transaktioner och notifieringar\n" +
                    "8. Kundservice\n" +
                    "0. Logga ut / gå tillbaka");

                switch (ReadMenuChoice(
                    "1", "2", "3", "4", "5", "6", "7", "8", "0"))
                {
                    case "1":
                        ShowAccountOverview(customer);
                        break;

                    case "2":
                        TransferMenu(customer);
                        break;

                    case "3":
                        AtmMenu(customer);
                        break;

                    case "4":
                        SettingsMenu(customer);
                        break;

                    case "5":
                        CreateAccountMenu(customer);
                        break;

                    case "6":
                        LoanMenu(customer);
                        break;

                    case "7":
                        PendingMenu(customer);
                        break;

                    case "8":
                        CustomerSupport.ShowSupportMenu(bank, customer);
                        break;

                    case "0":
                        return;
                }
            }
        }

        // Här visar jag kundens konton
        // och gör menyvalet till rätt plats i listan.
        private static Bankkonto? SelectAccount(
            Kund customer,
            bool sekOnly = false)
        {
            List<Bankkonto> accounts = customer.Accounts
                .Where(account =>
                    !sekOnly || account.Currency == "SEK")
                .ToList();

            lock (bank.SyncRoot)
            {
                for (int i = 0; i < accounts.Count; i++)
                {
                    Console.WriteLine(
                        $"{i + 1}. {accounts[i].AccountType} " +
                        $"{accounts[i].DisplayAccountNumber} | " +
                        $"Saldo: {accounts[i].Balance:F2} | " +
                        $"Tillgängligt: {bank.AvailableBalance(accounts[i]):F2} " +
                        $"{accounts[i].Currency}");
                }
            }

            if (accounts.Count == 0)
            {
                Console.WriteLine("Du har inget konto att välja.");
                return null;
            }

            Console.WriteLine("0. Tillbaka");

            while (true)
            {
                string choice =
                    ReadRequiredText("Välj konto: ");

                if (choice == "")
                {
                    return null;
                }

                if (int.TryParse(choice, out int number) &&
                    number >= 1 &&
                    number <= accounts.Count)
                {
                    return accounts[number - 1];
                }

                Console.WriteLine(
                    "Välj ett konto som finns i listan.");
            }
        }

        // Här visar jag saldot och alla genomförda transaktioner
        // på det valda kontot.
        private static void ShowAccountOverview(Kund customer)
        {
            ShowPage("KONTOÖVERSIKT");

            Bankkonto? account = SelectAccount(customer);

            if (account == null)
            {
                return;
            }

            lock (bank.SyncRoot)
            {
                Console.WriteLine(
                    $"\nSaldo: {account.Balance:F2} " +
                    $"{account.Currency}\nALLA TRANSAKTIONER");

                foreach (Transaktion transaction in
                    account.Transactions
                        .OrderByDescending(transaction => transaction.Date))
                {
                    Console.WriteLine(
                        $"{transaction.Date.ToLocalTime():yyyy-MM-dd HH:mm} | " +
                        $"{transaction.Type} | " +
                        $"{transaction.Amount:F2} {account.Currency} | " +
                        $"{transaction.Message}");
                }

                if (account.Transactions.Count == 0)
                {
                    Console.WriteLine("Det finns inga transaktioner.");
                }

                if (account.AccountType == "Sparkonto")
                {
                    Console.WriteLine(
                        "Beräknad ränta på nuvarande saldo under ett år: " +
                        $"{Math.Round(account.Balance * bank.SavingsInterestRate / 100m, 2):F2} " +
                        $"{account.Currency} " +
                        $"({bank.SavingsInterestRate}% årsränta).");
                }
            }

            Pause();
        }

        // Här låter jag kunden bekräfta eller avbryta
        // innan något skickas vidare.
        private static bool Confirm()
        {
            Console.WriteLine(
                "1. Bekräfta\n" +
                "0. Avbryt");

            return ReadMenuChoice("1", "0") == "1";
        }

        // Här visar jag sparräntan innan pengar köas till ett sparkonto.
        private static void ShowSavingsInterest(
            Bankkonto account,
            decimal amount)
        {
            if (account.AccountType == "Sparkonto")
            {
                Console.WriteLine(
                    "Beräknad ränta på insättningen under ett år: " +
                    $"{Math.Round(amount * bank.SavingsInterestRate / 100m, 2):F2} " +
                    $"{account.Currency} " +
                    $"({bank.SavingsInterestRate}% årsränta).");
            }
        }

        // Här lämnar jag kundens begäran till banken
        // och visar om den lades i kön.
        private static void Queue(
            Kund customer,
            string type,
            Bankkonto? from,
            Bankkonto? to,
            decimal amount,
            string message)
        {
            if (bank.QueueTransaction(
                customer,
                type,
                from,
                to,
                amount,
                message,
                out string error))
            {
                Console.WriteLine(
                    $"Transaktionen väntar till " +
                    $"{Bank.NextQuarter(DateTime.UtcNow).ToLocalTime():HH:mm}. " +
                    "Saldot ändras först då.");
            }
            else
            {
                Console.WriteLine(error);
            }

            Pause();
        }

        // Här väljer kunden avsändare, mottagare och belopp
        // och ser växlingen före bekräftelsen.
        private static void TransferMenu(Kund customer)
        {
            ShowPage("ÖVERFÖR PENGAR");

            Bankkonto? from = SelectAccount(customer);

            if (from == null)
            {
                return;
            }

            string number = ReadRequiredText(
                "Mottagarens kontonummer (0 för att gå tillbaka): ");

            if (number == "")
            {
                return;
            }

            Bankkonto? to = bank.FindAccount(number);

            if (to == null || to == from)
            {
                Console.WriteLine(
                    "Välj ett annat giltigt mottagarkonto.");

                Pause();

                return;
            }

            decimal? amount = ReadDecimal(
                $"Belopp i {from.Currency} (0 för att avbryta): ");

            if (amount == null)
            {
                return;
            }

            if (!bank.TryConvert(
                amount.Value,
                from.Currency,
                to.Currency,
                out decimal received,
                out string error))
            {
                Console.WriteLine(error);
                Pause();

                return;
            }

            Console.Write("Meddelande: ");

            string message = ReadInput();

            Console.WriteLine(
                $"Från: {from.DisplayAccountNumber}\n" +
                $"Till: {to.DisplayAccountNumber}\n" +
                $"Belopp: {amount:F2} {from.Currency}");

            Console.WriteLine(
                $"Beräknat mottagarbelopp: {received:F2} {to.Currency}. " +
                "Dagens kurs vid körningen används.");

            ShowSavingsInterest(to, received);

            if (Confirm())
            {
                Queue(
                    customer,
                    "Överföring",
                    from,
                    to,
                    amount.Value,
                    message);
            }
        }

        // Här väljer kunden uttag eller insättning och bekräftar beloppet.
        private static void AtmMenu(Kund customer)
        {
            ShowPage("BANKOMAT");

            Console.WriteLine(
                "1. Kontantuttag\n" +
                "2. Sedelinsättning\n" +
                "0. Tillbaka");

            string choice = ReadMenuChoice("1", "2", "0");

            if (choice == "0")
            {
                return;
            }

            Bankkonto? account = SelectAccount(customer);

            if (account == null)
            {
                return;
            }

            decimal? amount = ReadDecimal(
                $"Belopp i {account.Currency} (0 för att avbryta): ");

            if (amount == null)
            {
                return;
            }

            Console.WriteLine(
                $"{(choice == "1" ? "Uttag" : "Insättning")}: " +
                $"{amount:F2} {account.Currency}");

            if (choice == "2")
            {
                ShowSavingsInterest(account, amount.Value);
            }

            if (Confirm())
            {
                Queue(
                    customer,
                    choice == "1" ? "Uttag" : "Insättning",
                    choice == "1" ? account : null,
                    choice == "2" ? account : null,
                    amount.Value,
                    "Bankomat");
            }
        }

        // Här väljer kunden kontotyp och valuta
        // innan det nya kontot skapas.
        private static void CreateAccountMenu(Kund customer)
        {
            ShowPage("SKAPA KONTO");

            Console.WriteLine(
                "1. Privatkonto\n" +
                "2. Sparkonto\n" +
                "0. Tillbaka");

            string type = ReadMenuChoice("1", "2", "0");

            if (type == "0")
            {
                return;
            }

            List<string> currencies =
                bank.ExchangeRates.Keys
                    .OrderBy(currency => currency)
                    .ToList();

            for (int i = 0; i < currencies.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {currencies[i]}");
            }

            string choice = ReadRequiredText(
                "Välj valuta (0 för att avbryta): ");

            if (!int.TryParse(choice, out int number) ||
                number < 1 ||
                number > currencies.Count)
            {
                return;
            }

            Console.WriteLine(
                $"Kontotyp: {(type == "1" ? "Privatkonto" : "Sparkonto")} | " +
                $"Valuta: {currencies[number - 1]} | " +
                "Startsaldo: 0,00");

            if (Confirm())
            {
                Console.WriteLine(
                    bank.CreateAccount(
                        customer,
                        type == "1" ? "Privatkonto" : "Sparkonto",
                        currencies[number - 1])
                        ? "Kontot är skapat."
                        : "Kontot kunde inte skapas.");

                Pause();
            }
        }

        // Här visar jag kunduppgifterna och låter kunden ändra dem
        // eller behålla tidigare värden.
        private static void SettingsMenu(Kund customer)
        {
            ShowPage("MINA UPPGIFTER");

            Console.WriteLine(
                $"{customer.FirstName} {customer.LastName} | " +
                $"{customer.Username} | " +
                $"Kundnummer: {customer.CustomerNumber}");

            Console.WriteLine(
                $"E-post: {customer.EmailAddress}\n" +
                $"Telefonnummer: {customer.PhoneNumber}");

            string email = ReadUpdatedText(
                "Ny e-postadress (Enter behåller, 0 avbryter): ",
                customer.EmailAddress);

            if (email == "")
            {
                return;
            }

            string phone = ReadUpdatedText(
                "Nytt telefonnummer (Enter behåller, 0 avbryter): ",
                customer.PhoneNumber);

            if (phone == "")
            {
                return;
            }

            Console.Write(
                "Nytt lösenord " +
                "(Enter behåller det gamla, 0 avbryter): ");

            string password = ReadPassword();

            if (password == "0")
            {
                return;
            }

            if (Confirm())
            {
                Console.WriteLine(
                    bank.UpdateDetails(
                        customer,
                        email,
                        phone,
                        password)
                        ? "Uppgifterna är uppdaterade."
                        : "Ogiltiga uppgifter.");

                Pause();
            }
        }

        // Här visar jag tidigare lån, kundens lånegräns
        // och beräknad ränta på ett nytt lån.
        private static void LoanMenu(Kund customer)
        {
            ShowPage("LÅN");

            lock (bank.SyncRoot)
            {
                foreach (Loan loan in bank.Loans.Where(loan =>
                    loan.CustomerNumber == customer.CustomerNumber))
                {
                    Console.WriteLine(
                        $"Lån {loan.LoanNumber}: " +
                        $"{loan.RemainingAmount:F2} SEK | " +
                        $"{loan.InterestRate}% | " +
                        $"Beräknad årsränta: {loan.YearlyInterest:F2} SEK");
                }
            }

            if (!bank.TryLoanLimit(
                customer,
                out decimal limit,
                out string error))
            {
                Console.WriteLine(error);
                Pause();

                return;
            }

            decimal rate =
                bank.LoanInterestRules[customer.CreditRating];

            Console.WriteLine(
                $"Du kan låna ytterligare högst {limit:F2} SEK. " +
                $"Din årsränta är {rate}%.");

            Bankkonto? to = SelectAccount(customer, true);

            if (to == null)
            {
                return;
            }

            decimal? amount = ReadDecimal(
                "Lånebelopp i SEK (0 för att avbryta): ");

            if (amount == null)
            {
                return;
            }

            Console.WriteLine(
                "Beräknad ränta under ett år: " +
                $"{Math.Round(amount.Value * rate / 100m, 2):F2} SEK. " +
                "Kapital plus ett års ränta: " +
                $"{amount.Value + Math.Round(amount.Value * rate / 100m, 2):F2} SEK.");

            Console.WriteLine(
                "Beräkningen antar oförändrat lånebelopp under ett år.");

            if (Confirm())
            {
                Queue(
                    customer,
                    "Lån",
                    null,
                    to,
                    amount.Value,
                    "Låneutbetalning");
            }
        }

        // Här visar jag transaktionernas status
        // och låter kunden avbryta en väntande transaktion.
        private static void PendingMenu(Kund customer)
        {
            ShowPage("TRANSAKTIONER OCH NOTIFIERINGAR");

            lock (bank.SyncRoot)
            {
                foreach (PendingTransaction transaction in
                    bank.PendingTransactions.Where(transaction =>
                        transaction.CustomerNumber == customer.CustomerNumber))
                {
                    Console.WriteLine(
                        $"{transaction.TransactionNumber}. " +
                        $"{transaction.Type} | " +
                        $"{transaction.Amount:F2} {transaction.Currency} | " +
                        $"{transaction.ExecuteAt.ToLocalTime():yyyy-MM-dd HH:mm} | " +
                        $"{transaction.Status} | " +
                        $"{transaction.Result}");
                }

                foreach (Notification notification in
                    bank.Notifications.Where(notification =>
                        notification.CustomerNumber == customer.CustomerNumber))
                {
                    Console.WriteLine(
                        $"E-post {notification.NotificationNumber}: " +
                        $"{(notification.IsSent ? "Skickad" : "Väntar på leverans")} | " +
                        $"{notification.Message}");
                }
            }

            string input = ReadRequiredText(
                "Ange nummer på väntande transaktion att avbryta " +
                "(0 för att gå tillbaka): ");

            if (input == "")
            {
                return;
            }

            if (int.TryParse(input, out int number) && Confirm())
            {
                Console.WriteLine(
                    bank.CancelTransaction(customer, number)
                        ? "Transaktionen är avbruten."
                        : "Transaktionen kunde inte avbrytas.");

                Pause();
            }
        }

        // Här visar jag funktionerna
        // som bara bankens administratörer får använda.
        private static void AdminMenu(Kund administrator)
        {
            while (true)
            {
                ShowPage("ADMINISTRATÖR");

                if (bank.BackgroundError != "")
                {
                    Console.WriteLine(bank.BackgroundError);
                }

                Console.WriteLine(
                    "1. Skapa kund eller administratör\n" +
                    "2. Uppdatera dagens växelkurs\n" +
                    "3. Ändra låneränta\n" +
                    "4. Statistik\n" +
                    "5. Kreditvärdighet och lås upp kund\n" +
                    "6. Hantera supportärende\n" +
                    "7. Sparränta och notifieringsgräns\n" +
                    "8. Transaktioner och e-poststatus\n" +
                    "9. Mina kundfunktioner\n" +
                    "0. Logga ut");

                string choice = ReadMenuChoice(
                    "1", "2", "3", "4", "5",
                    "6", "7", "8", "9", "0");

                if (choice == "0")
                {
                    return;
                }

                // Här skapar administratören en vanlig kund eller en administratör.
                if (choice == "1")
                {
                    Console.WriteLine(
                        "1. Kund\n" +
                        "2. Administratör\n" +
                        "0. Tillbaka");

                    string role =
                        ReadMenuChoice("1", "2", "0");

                    if (role != "0")
                    {
                        RegisterCustomer(
                            role == "2",
                            administrator);
                    }
                }

                // Här uppdaterar administratören dagens valutakurs.
                if (choice == "2")
                {
                    foreach (var exchangeRate in bank.ExchangeRates)
                    {
                        Console.WriteLine(
                            $"{exchangeRate.Key}: {exchangeRate.Value} SEK | " +
                            $"Uppdaterad: " +
                            $"{(bank.RateDates.TryGetValue(exchangeRate.Key, out DateTime date) ? date.ToString("yyyy-MM-dd") : "SEK är grundvaluta")}");
                    }

                    string currency = ReadRequiredText(
                        "Valutakod, exempelvis EUR (0 avbryter): ")
                        .ToUpperInvariant();

                    if (currency == "")
                    {
                        continue;
                    }

                    decimal? rate = ReadDecimal(
                        "Antal SEK för 1 enhet av valutan (0 avbryter): ",
                        false,
                        false);

                    if (rate != null && Confirm())
                    {
                        Console.WriteLine(
                            bank.UpdateRate(
                                administrator,
                                currency,
                                rate.Value)
                                ? "Dagens växelkurs är sparad."
                                : "Ogiltig valutakod eller kurs.");

                        Pause();
                    }
                }

                // Här ändrar administratören räntan för en kreditvärdighet.
                if (choice == "3")
                {
                    Console.WriteLine(
                        "Kreditvärdighet: " +
                        "1 = låg, 2 = medel, 3 = hög.\n" +
                        "0. Tillbaka");

                    string rating =
                        ReadMenuChoice("1", "2", "3", "0");

                    if (rating == "0")
                    {
                        continue;
                    }

                    decimal? rate = ReadDecimal(
                        "Årsränta i procent " +
                        "(0 tillåts, skriv avbryt för att gå tillbaka): ",
                        true,
                        false);

                    if (rate != null && Confirm())
                    {
                        Console.WriteLine(
                            bank.UpdateInterestRule(
                                administrator,
                                int.Parse(rating),
                                rate.Value)
                                ? "Låneregeln är sparad."
                                : "Räntan ska vara 0–100%.");

                        Pause();
                    }
                }

                // Här visar jag bankens statistik.
                if (choice == "4")
                {
                    Console.WriteLine(
                        bank.Statistics(administrator));

                    Pause();
                }

                // Här ändrar administratören kreditvärdighet
                // och kan samtidigt låsa upp kunden.
                if (choice == "5")
                {
                    foreach (Kund customer in bank.Customers)
                    {
                        Console.WriteLine(
                            $"{customer.CustomerNumber}. " +
                            $"{customer.Username} | " +
                            $"Kreditvärdighet: {customer.CreditRating} | " +
                            $"Spärrad: {customer.IsLocked}");
                    }

                    string number = ReadRequiredText(
                        "Kundnummer (0 avbryter): ");

                    if (!int.TryParse(
                        number,
                        out int customerNumber))
                    {
                        continue;
                    }

                    Console.WriteLine(
                        "Välj kreditvärdighet: " +
                        "1 = låg, 2 = medel, 3 = hög.\n" +
                        "0. Tillbaka");

                    string rating =
                        ReadMenuChoice("1", "2", "3", "0");

                    if (rating == "0")
                    {
                        continue;
                    }

                    Console.WriteLine(
                        "Lås också upp kunden?\n" +
                        "1. Ja\n" +
                        "2. Nej\n" +
                        "0. Tillbaka");

                    string unlock =
                        ReadMenuChoice("1", "2", "0");

                    if (unlock != "0" && Confirm())
                    {
                        Console.WriteLine(
                            bank.UpdateCustomer(
                                administrator,
                                customerNumber,
                                int.Parse(rating),
                                unlock == "1")
                                ? "Kunden är uppdaterad."
                                : "Kunden kunde inte uppdateras.");

                        Pause();
                    }
                }

                // Här hanterar administratören nästa ärende i supportkön.
                if (choice == "6")
                {
                    lock (bank.SyncRoot)
                    {
                        foreach (SupportTicket ticket in bank.SupportTickets)
                        {
                            Console.WriteLine(
                                $"{ticket.TicketNumber}. " +
                                $"Kund {ticket.CustomerNumber}: " +
                                $"{ticket.Description}");
                        }
                    }

                    Console.WriteLine(
                        "1. Markera nästa ärende som hanterat\n" +
                        "0. Tillbaka");

                    if (ReadMenuChoice("1", "0") == "1")
                    {
                        SupportTicket? ticket =
                            bank.HandleSupportTicket(administrator);

                        Console.WriteLine(
                            ticket == null
                                ? "Kön är tom."
                                : $"Ärende {ticket.TicketNumber} är hanterat.");

                        Pause();
                    }
                }

                // Här ändrar administratören sparräntan
                // och beloppsgränsen för notifieringar.
                if (choice == "7")
                {
                    decimal? savingsRate = ReadDecimal(
                        "Sparränta i procent " +
                        "(0 tillåts, avbryt går tillbaka): ",
                        true,
                        false);

                    if (savingsRate == null)
                    {
                        continue;
                    }

                    decimal? threshold = ReadDecimal(
                        "Notifieringsgräns i SEK (0 avbryter): ");

                    if (threshold != null && Confirm())
                    {
                        Console.WriteLine(
                            bank.UpdateBankRules(
                                administrator,
                                savingsRate.Value,
                                threshold.Value)
                                ? "Bankreglerna är sparade."
                                : "Ogiltiga bankregler.");

                        Pause();
                    }
                }

                // Här visar jag status för transaktioner
                // och bankens e-postleveranser.
                if (choice == "8")
                {
                    lock (bank.SyncRoot)
                    {
                        foreach (PendingTransaction transaction in
                            bank.PendingTransactions)
                        {
                            Console.WriteLine(
                                $"{transaction.TransactionNumber}: " +
                                $"{transaction.Type} | " +
                                $"{transaction.Status} | " +
                                $"{transaction.Result}");
                        }

                        foreach (Notification notification in bank.Notifications)
                        {
                            Console.WriteLine(
                                $"E-post {notification.NotificationNumber}: " +
                                $"{(notification.IsSent ? "Skickad" : "Väntar")} | " +
                                $"Försök: {notification.Attempts} | " +
                                $"{notification.Error}");
                        }
                    }

                    Pause();
                }

                // Här får administratören använda sina egna kundfunktioner.
                if (choice == "9")
                {
                    MainMenu(administrator);
                }
            }
        }
    }
}