using System.Net.Mail;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace bank
{
    internal class Bank : IDisposable
    {
        // Här samlar jag bankens uppgifter så de kan sparas tillsammans.
        public List<Kund> Customers { get; set; } = new List<Kund>();

        public List<PendingTransaction> PendingTransactions { get; set; } =
            new List<PendingTransaction>();

        public List<Loan> Loans { get; set; } = new List<Loan>();

        public List<Notification> Notifications { get; set; } =
            new List<Notification>();

        public Queue<SupportTicket> SupportTickets { get; set; } =
            new Queue<SupportTicket>();

        // Här sparar jag valutakurserna.
        // Värdet visar hur många SEK en valutaenhet kostar.
        public Dictionary<string, decimal> ExchangeRates { get; set; } =
            new Dictionary<string, decimal>
            {
                { "SEK", 1m }
            };

        // Här sparar jag dagen då varje valutakurs senast uppdaterades.
        public Dictionary<string, DateTime> RateDates { get; set; } =
            new Dictionary<string, DateTime>();

        // Här kopplar jag kreditvärdighet 1, 2 och 3 till bankens låneräntor.
        public Dictionary<int, decimal> LoanInterestRules { get; set; } =
            new Dictionary<int, decimal>
            {
                { 1, 8m },
                { 2, 5m },
                { 3, 3m }
            };

        // Här sparar jag sparräntan i procent
        // och gränsen för stora transaktioner i SEK.
        public decimal SavingsInterestRate { get; set; } = 2m;
        public decimal NotificationThreshold { get; set; } = 10000m;

        // Här håller jag koll på nästa nummer så nya kunder
        // och händelser får egna nummer.
        public int NextCustomerNumber { get; set; } = 100000;
        public int NextAccountNumber { get; set; } = 1000;
        public int NextTransactionNumber { get; set; }
        public int NextLoanNumber { get; set; }
        public int NextTicketNumber { get; set; }
        public int NextNotificationNumber { get; set; }

        // Här hindrar jag menyerna och bakgrundskörningen från att ändra samtidigt.
        [JsonIgnore] public object SyncRoot { get; } = new object();

        [JsonIgnore] public string BackgroundError { get; private set; } = "";

        // Här sparar jag filens sökväg, tidtagarna
        // och om banken håller på att stängas.
        private string dataFile = "";
        private FileStream? fileLock;
        private Timer? transactionTimer;
        private Timer? notificationTimer;
        private bool sendingNotifications;
        private bool disposed;

        // Här läser jag sparfilen och stoppar en andra app
        // från att använda samma fil.
        public static Bank Load(string path)
        {
            path = Path.GetFullPath(path);

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            FileStream fileLock = new FileStream(
                path + ".lock",
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);

            try
            {
                Bank bank = File.Exists(path)
                    ? JsonSerializer.Deserialize<Bank>(File.ReadAllText(path))
                        ?? throw new InvalidDataException("Sparfilen är tom.")
                    : new Bank();

                bank.dataFile = path;
                bank.fileLock = fileLock;
                bank.ValidateData();

                return bank;
            }
            catch
            {
                fileLock.Dispose();
                throw;
            }
        }

        // Här kollar jag att sparfilens kunder, konton
        // och regler går att använda.
        private void ValidateData()
        {
            if (Customers == null ||
                PendingTransactions == null ||
                Loans == null ||
                Notifications == null ||
                SupportTickets == null ||
                ExchangeRates == null ||
                RateDates == null ||
                LoanInterestRules == null)
            {
                throw new InvalidDataException(
                    "Sparfilen saknar bankens listor eller regler.");
            }

            HashSet<string> usernames =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            HashSet<int> numbers = new HashSet<int>();
            HashSet<string> accounts = new HashSet<string>();

            foreach (Kund customer in Customers)
            {
                if (customer == null ||
                    customer.Accounts == null ||
                    string.IsNullOrWhiteSpace(customer.PasswordHash) ||
                    !IsValidEmail(customer.EmailAddress) ||
                    !IsValidPhone(customer.PhoneNumber))
                {
                    throw new InvalidDataException(
                        "Sparfilen saknar kunduppgifter.");
                }

                if (!IsValidUsername(customer.Username) ||
                    !usernames.Add(customer.Username) ||
                    !numbers.Add(customer.CustomerNumber) ||
                    customer.CustomerNumber < 100001 ||
                    customer.CustomerNumber > 999999 ||
                    customer.CreditRating < 1 ||
                    customer.CreditRating > 3 ||
                    customer.FailedLoginAttempts < 0)
                {
                    throw new InvalidDataException(
                        "Sparfilen innehåller ogiltiga kunduppgifter.");
                }

                string[] password = customer.PasswordHash.Split(':');

                if (password.Length != 2 ||
                    Convert.FromBase64String(password[0]).Length != 16 ||
                    Convert.FromBase64String(password[1]).Length != 32)
                {
                    throw new InvalidDataException(
                        "Sparfilen innehåller en ogiltig lösenordshash.");
                }

                foreach (Bankkonto account in customer.Accounts)
                {
                    if (account == null ||
                        account.Transactions == null ||
                        account.AccountNumber == null ||
                        account.AccountNumber.Length != 11 ||
                        !account.AccountNumber.All(char.IsAsciiDigit) ||
                        !accounts.Add(account.AccountNumber) ||
                        account.Balance < 0 ||
                        account.Balance > Bankkonto.MaximumBalance ||
                        account.Balance != Math.Round(account.Balance, 2) ||
                        !ExchangeRates.ContainsKey(account.Currency) ||
                        (account.AccountType != "Privatkonto" &&
                         account.AccountType != "Sparkonto"))
                    {
                        throw new InvalidDataException(
                            "Sparfilen innehåller ogiltiga kontouppgifter.");
                    }
                }
            }

            if (!ExchangeRates.TryGetValue("SEK", out decimal sek) ||
                sek != 1m ||
                ExchangeRates.Any(rate =>
                    rate.Key.Length != 3 ||
                    !rate.Key.All(character =>
                        character >= 'A' && character <= 'Z') ||
                    rate.Value <= 0 ||
                    rate.Value > 1000000m) ||
                Enumerable.Range(1, 3).Any(rating =>
                    !LoanInterestRules.ContainsKey(rating) ||
                    LoanInterestRules[rating] < 0 ||
                    LoanInterestRules[rating] > 100) ||
                SavingsInterestRate < 0 ||
                SavingsInterestRate > 100 ||
                NotificationThreshold <= 0 ||
                (Customers.Count > 0 &&
                 !Customers.Any(customer => customer.IsAdmin)))
            {
                throw new InvalidDataException(
                    "Sparfilen innehåller ogiltiga bankregler.");
            }

            if (NextCustomerNumber <
                    Customers.Select(customer => customer.CustomerNumber)
                        .DefaultIfEmpty(100000).Max() ||
                NextAccountNumber <
                    Customers.SelectMany(customer => customer.Accounts)
                        .Select(account =>
                            int.Parse(account.AccountNumber.Substring(4)))
                        .DefaultIfEmpty(1000).Max() ||
                NextTransactionNumber <
                    PendingTransactions.Select(transaction =>
                            transaction.TransactionNumber)
                        .DefaultIfEmpty(0).Max() ||
                NextLoanNumber <
                    Loans.Select(loan => loan.LoanNumber)
                        .DefaultIfEmpty(0).Max() ||
                NextTicketNumber <
                    SupportTickets.Select(ticket => ticket.TicketNumber)
                        .DefaultIfEmpty(0).Max() ||
                NextNotificationNumber <
                    Notifications.Select(notification =>
                            notification.NotificationNumber)
                        .DefaultIfEmpty(0).Max())
            {
                throw new InvalidDataException(
                    "Sparfilens nummerserier stämmer inte.");
            }
        }

        // Här skriver jag först till en tillfällig fil
        // så en avbruten skrivning inte förstör sparfilen.
        public void Save()
        {
            lock (SyncRoot)
            {
                string json = JsonSerializer.Serialize(
                    this,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });

                File.WriteAllText(dataFile + ".tmp", json);
                File.Move(dataFile + ".tmp", dataFile, true);
            }
        }

        // Här räknar jag fram nästa klockslag som är en hel kvart.
        public static DateTime NextQuarter(DateTime now)
        {
            long interval = TimeSpan.FromMinutes(15).Ticks;

            return new DateTime(
                (now.Ticks / interval + 1) * interval,
                now.Kind);
        }

        // Här startar jag transaktionerna var femtonde minut
        // och e-postens återförsök.
        public void StartProcessors()
        {
            ProcessPending(DateTime.UtcNow);

            DateTime now = DateTime.UtcNow;

            transactionTimer = new Timer(
                _ => RunInBackground(
                    () => ProcessPending(DateTime.UtcNow)),
                null,
                NextQuarter(now) - now,
                TimeSpan.FromMinutes(15));

            notificationTimer = new Timer(
                _ => RunInBackground(SendNotifications),
                null,
                TimeSpan.Zero,
                TimeSpan.FromMinutes(1));
        }

        // Här kör jag en bakgrundsuppgift och fångar fel
        // om sparfilen inte går att skriva.
        private void RunInBackground(Action action)
        {
            try
            {
                action();
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException)
            {
                BackgroundError =
                    "Bankens uppgifter kunde inte sparas. Kontrollera sparfilen.";
            }
        }

        // Här stoppar jag tidtagarna och släpper filen när appen stängs.
        public void Dispose()
        {
            lock (SyncRoot)
            {
                disposed = true;

                transactionTimer?.Dispose();
                notificationTimer?.Dispose();
                fileLock?.Dispose();
            }
        }

        // Här kollar jag att användarnamnet börjar med en bokstav
        // och har tillåtna tecken.
        public static bool IsValidUsername(string value) =>
            !string.IsNullOrWhiteSpace(value) &&
            value.Length >= 3 &&
            value.Length <= 30 &&
            char.IsAsciiLetter(value[0]) &&
            value.All(character =>
                char.IsAsciiLetterOrDigit(character) ||
                character == '_' ||
                character == '-' ||
                character == '.');

        // Här kollar jag att e-postadressen går att använda som en e-postadress.
        public static bool IsValidEmail(string value) =>
            !string.IsNullOrWhiteSpace(value) &&
            MailAddress.TryCreate(value, out var address) &&
            address.Address == value;

        // Här kollar jag antalet siffror och att plustecknet
        // bara står först i telefonnumret.
        public static bool IsValidPhone(string value) =>
            !string.IsNullOrWhiteSpace(value) &&
            value.Count(char.IsAsciiDigit) >= 7 &&
            value.Count(char.IsAsciiDigit) <= 15 &&
            value.Select((character, index) =>
                char.IsAsciiDigit(character) ||
                character == ' ' ||
                character == '-' ||
                character == '(' ||
                character == ')' ||
                (character == '+' && index == 0))
                .All(valid => valid);

        // Här kollar jag att kunden tillhör banken,
        // är administratör och inte är spärrad.
        private bool IsAdmin(Kund? customer) =>
            customer != null &&
            Customers.Contains(customer) &&
            customer.IsAdmin &&
            !customer.IsLocked;

        // Här kollar jag att kunden tillhör banken och inte är spärrad.
        private bool IsCustomer(Kund customer) =>
            Customers.Contains(customer) &&
            !customer.IsLocked;

        // Här skapar jag kunden efter kontroll.
        // Den allra första kunden måste vara administratör.
        public Kund? CreateCustomer(
            string[] details,
            bool isAdmin,
            Kund? administrator,
            out string error)
        {
            lock (SyncRoot)
            {
                error =
                    "Uppgifterna är ogiltiga eller användarnamnet är upptaget.";

                if (details.Length != 6 ||
                    !IsValidUsername(details[0]) ||
                    string.IsNullOrWhiteSpace(details[1]) ||
                    string.IsNullOrWhiteSpace(details[2]) ||
                    !IsValidEmail(details[3]) ||
                    !IsValidPhone(details[4]) ||
                    string.IsNullOrWhiteSpace(details[5]) ||
                    details[5] == "0" ||
                    Customers.Any(customer =>
                        customer.Username.Equals(
                            details[0],
                            StringComparison.OrdinalIgnoreCase)))
                {
                    return null;
                }

                if ((Customers.Count == 0 && !isAdmin) ||
                    (isAdmin &&
                     Customers.Count > 0 &&
                     !IsAdmin(administrator)) ||
                    (administrator != null &&
                     !IsAdmin(administrator)))
                {
                    error = "Administratörsbehörighet saknas.";
                    return null;
                }

                if (NextCustomerNumber >= 999999)
                {
                    error = "Alla kundnummer är använda.";
                    return null;
                }

                Kund customer = new Kund(
                    ++NextCustomerNumber,
                    details[0],
                    details[1],
                    details[2],
                    details[3],
                    details[4],
                    details[5],
                    isAdmin);

                customer.Accounts.Add(new Bankkonto(
                    "7000" + (++NextAccountNumber).ToString("D7"),
                    "Privatkonto",
                    "SEK"));

                Customers.Add(customer);

                Save();

                error = "";

                return customer;
            }
        }

        // Här hittar jag rätt kund och sparar även misslyckade inloggningar.
        public Kund? Login(
            string identifier,
            string password,
            out string message)
        {
            lock (SyncRoot)
            {
                Kund? customer = Customers.FirstOrDefault(customer =>
                    customer.Username.Equals(
                        identifier,
                        StringComparison.OrdinalIgnoreCase) ||
                    customer.CustomerNumber.ToString() == identifier);

                message =
                    "Fel användarnamn, kundnummer eller lösenord.";

                if (customer == null)
                {
                    return null;
                }

                bool success = customer.Authenticate(password);

                Save();

                if (customer.IsLocked)
                {
                    message =
                        "Kunden är spärrad efter tre fel. Kontakta administratören.";
                }
                else if (!success)
                {
                    message +=
                        $" Försök kvar: {3 - customer.FailedLoginAttempts}.";
                }

                return success ? customer : null;
            }
        }

        // Här kontrollerar jag de nya kunduppgifterna
        // innan de ändras och sparas.
        public bool UpdateDetails(
            Kund customer,
            string email,
            string phone,
            string password)
        {
            lock (SyncRoot)
            {
                if (!IsCustomer(customer) ||
                    !IsValidEmail(email) ||
                    !IsValidPhone(phone) ||
                    password == "0" ||
                    (password != "" &&
                     string.IsNullOrWhiteSpace(password)))
                {
                    return false;
                }

                customer.UpdateDetails(email, phone);

                if (password != "")
                {
                    customer.UpdatePassword(password);
                }

                Save();

                return true;
            }
        }

        // Här låter jag administratören ändra kreditvärdighet
        // och låsa upp en kund.
        public bool UpdateCustomer(
            Kund administrator,
            int number,
            int rating,
            bool unlock)
        {
            lock (SyncRoot)
            {
                Kund? customer = Customers.Find(customer =>
                    customer.CustomerNumber == number);

                if (!IsAdmin(administrator) ||
                    customer == null ||
                    rating < 1 ||
                    rating > 3)
                {
                    return false;
                }

                customer.SetCreditRating(rating);

                if (unlock)
                {
                    customer.Unlock();
                }

                Save();

                return true;
            }
        }

        // Här letar jag efter kontonumret bland alla kunders konton.
        public Bankkonto? FindAccount(string number) =>
            Customers.SelectMany(customer => customer.Accounts)
                .FirstOrDefault(account =>
                    account.AccountNumber ==
                    Bankkonto.NormalizeAccountNumber(number));

        // Här kontrollerar jag kontotypen och valutan
        // innan ett nytt konto skapas.
        public bool CreateAccount(
            Kund customer,
            string type,
            string currency)
        {
            lock (SyncRoot)
            {
                if (!IsCustomer(customer) ||
                    (type != "Privatkonto" && type != "Sparkonto") ||
                    !ExchangeRates.ContainsKey(currency) ||
                    NextAccountNumber >= 9999999)
                {
                    return false;
                }

                customer.Accounts.Add(new Bankkonto(
                    "7000" + (++NextAccountNumber).ToString("D7"),
                    type,
                    currency));

                Save();

                return true;
            }
        }

        // Här anger administratören hur många SEK
        // en enhet av valutan är värd idag.
        public bool UpdateRate(
            Kund administrator,
            string currency,
            decimal rate)
        {
            lock (SyncRoot)
            {
                if (!IsAdmin(administrator) ||
                    currency.Length != 3 ||
                    !currency.All(character =>
                        character >= 'A' && character <= 'Z') ||
                    rate <= 0 ||
                    rate > 1000000m ||
                    (currency == "SEK" && rate != 1m))
                {
                    return false;
                }

                ExchangeRates[currency] = rate;
                RateDates[currency] = DateTime.Today;

                Save();

                return true;
            }
        }

        // Här låter jag administratören välja räntan
        // för en viss kreditvärdighet.
        public bool UpdateInterestRule(
            Kund administrator,
            int rating,
            decimal rate)
        {
            lock (SyncRoot)
            {
                if (!IsAdmin(administrator) ||
                    rating < 1 ||
                    rating > 3 ||
                    rate < 0 ||
                    rate > 100)
                {
                    return false;
                }

                LoanInterestRules[rating] = rate;

                Save();

                return true;
            }
        }

        // Här låter jag administratören ändra sparräntan
        // och notifieringsgränsen.
        public bool UpdateBankRules(
            Kund administrator,
            decimal savingsRate,
            decimal threshold)
        {
            lock (SyncRoot)
            {
                if (!IsAdmin(administrator) ||
                    savingsRate < 0 ||
                    savingsRate > 100 ||
                    !Bankkonto.IsValidAmount(threshold))
                {
                    return false;
                }

                SavingsInterestRate = savingsRate;
                NotificationThreshold = threshold;

                Save();

                return true;
            }
        }

        // Här växlar jag via SEK. Gårdagens kurs godkänns inte.
        public bool TryConvert(
            decimal amount,
            string from,
            string to,
            out decimal converted,
            out string error)
        {
            lock (SyncRoot)
            {
                converted = 0;

                error =
                    "Administratören behöver uppdatera dagens växelkurs.";

                foreach (string currency in new[] { from, to })
                {
                    if (!ExchangeRates.ContainsKey(currency) ||
                        (currency != "SEK" &&
                         (!RateDates.TryGetValue(
                              currency,
                              out DateTime date) ||
                          date.Date != DateTime.Today)))
                    {
                        return false;
                    }
                }

                try
                {
                    converted = Math.Round(
                        amount * ExchangeRates[from] / ExchangeRates[to],
                        2,
                        MidpointRounding.AwayFromZero);
                }
                catch (OverflowException)
                {
                    error = "Beloppet är för stort för att växlas.";
                    return false;
                }

                error = "";

                return true;
            }
        }

        // Här räknar jag bort pengarna som redan
        // är reserverade för väntande uttag.
        public decimal AvailableBalance(Bankkonto account)
        {
            lock (SyncRoot)
            {
                return account.Balance -
                    PendingTransactions.Where(transaction =>
                        transaction.Status == "Väntar" &&
                        transaction.FromAccountNumber ==
                            account.AccountNumber)
                        .Sum(transaction => transaction.Amount);
            }
        }

        // Här räknar jag egna pengar utan lånade pengar
        // och utan redan reserverade belopp.
        public bool TryLoanLimit(
            Kund customer,
            out decimal limit,
            out string error)
        {
            lock (SyncRoot)
            {
                limit = 0;
                decimal total = 0;
                error = "";

                foreach (Bankkonto account in customer.Accounts)
                {
                    decimal available = AvailableBalance(account);

                    if (available == 0)
                    {
                        continue;
                    }

                    if (!TryConvert(
                        available,
                        account.Currency,
                        "SEK",
                        out decimal amount,
                        out error))
                    {
                        return false;
                    }

                    total += amount;
                }

                // Här summerar jag lånen som kunden redan har fått utbetalda.
                decimal debt = Loans.Where(loan =>
                    loan.CustomerNumber == customer.CustomerNumber)
                    .Sum(loan => loan.RemainingAmount);

                // Här räknar jag också med lån
                // som kunden har begärt men ännu inte fått.
                decimal pendingLoans = PendingTransactions.Where(transaction =>
                    transaction.CustomerNumber == customer.CustomerNumber &&
                    transaction.Type == "Lån" &&
                    transaction.Status == "Väntar")
                    .Sum(transaction => transaction.Amount);

                // Här räknar jag fem gånger egna pengar
                // och tar bort befintliga och väntande lån.
                limit = Math.Max(
                    0,
                    Math.Min(
                        Bankkonto.MaximumBalance,
                        Math.Max(0, total - debt) * 5m -
                        debt -
                        pendingLoans));

                return true;
            }
        }

        // Här kontrollerar jag transaktionen
        // och lägger den i kön utan att ändra saldot.
        public bool QueueTransaction(
            Kund customer,
            string type,
            Bankkonto? from,
            Bankkonto? to,
            decimal amount,
            string message,
            out string error)
        {
            lock (SyncRoot)
            {
                error =
                    "Kontot, beloppet eller transaktionstypen är ogiltig.";

                if (!IsCustomer(customer) ||
                    !Bankkonto.IsValidAmount(amount))
                {
                    return false;
                }

                if (type == "Uttag" || type == "Överföring")
                {
                    if (from == null ||
                        !customer.Accounts.Contains(from))
                    {
                        return false;
                    }

                    if (amount > AvailableBalance(from))
                    {
                        error = "Tillgängligt saldo räcker inte.";
                        return false;
                    }
                }

                if (type == "Insättning" || type == "Lån")
                {
                    if (from != null ||
                        to == null ||
                        !customer.Accounts.Contains(to))
                    {
                        return false;
                    }
                }

                if (type == "Uttag" && to != null)
                {
                    return false;
                }

                if (type == "Överföring" &&
                    (to == null ||
                     to == from ||
                     FindAccount(to.AccountNumber) != to))
                {
                    return false;
                }

                if (type != "Uttag" &&
                    type != "Överföring" &&
                    type != "Insättning" &&
                    type != "Lån")
                {
                    return false;
                }

                Bankkonto account = from ?? to!;

                if (!TryConvert(
                    amount,
                    account.Currency,
                    to?.Currency ?? account.Currency,
                    out decimal received,
                    out error))
                {
                    return false;
                }

                if (!TryConvert(
                    amount,
                    account.Currency,
                    "SEK",
                    out _,
                    out error))
                {
                    return false;
                }

                decimal incoming = to == null
                    ? 0
                    : PendingTransactions.Where(transaction =>
                        transaction.Status == "Väntar" &&
                        transaction.ToAccountNumber == to.AccountNumber &&
                        transaction.Type != "Överföring")
                        .Sum(transaction => transaction.Amount);

                if (to != null &&
                    (received <= 0 ||
                     received >
                        Bankkonto.MaximumBalance - to.Balance - incoming))
                {
                    error =
                        "Mottagarkontots maxsaldo skulle överskridas " +
                        "eller växlingen blir mindre än ett öre.";

                    return false;
                }

                if (type == "Lån")
                {
                    if (to!.Currency != "SEK")
                    {
                        error = "Lån betalas ut till ett SEK-konto.";
                        return false;
                    }

                    if (!TryLoanLimit(
                        customer,
                        out decimal limit,
                        out error))
                    {
                        return false;
                    }

                    if (amount > limit)
                    {
                        error =
                            "Lånet överskrider gränsen på fem gånger dina egna pengar.";

                        return false;
                    }
                }

                // Här sparar jag begäran.
                // Själva saldot ändras först vid nästa kvart.
                PendingTransactions.Add(new PendingTransaction
                {
                    TransactionNumber = ++NextTransactionNumber,
                    CustomerNumber = customer.CustomerNumber,
                    Type = type,
                    FromAccountNumber = from?.AccountNumber ?? "",
                    ToAccountNumber = to?.AccountNumber ?? "",
                    Amount = amount,
                    Currency = account.Currency,
                    LoanInterestRate = type == "Lån"
                        ? LoanInterestRules[customer.CreditRating]
                        : 0,
                    Message = message,
                    RequestedAt = DateTime.UtcNow,
                    ExecuteAt = NextQuarter(DateTime.UtcNow)
                });

                Save();

                error = "";

                return true;
            }
        }

        // Här låter jag kunden avbryta sin egen transaktion
        // om den fortfarande väntar.
        public bool CancelTransaction(Kund customer, int number)
        {
            lock (SyncRoot)
            {
                PendingTransaction? transaction =
                    PendingTransactions.Find(transaction =>
                        transaction.TransactionNumber == number);

                if (!IsCustomer(customer) ||
                    transaction == null ||
                    transaction.CustomerNumber != customer.CustomerNumber ||
                    transaction.Status != "Väntar")
                {
                    return false;
                }

                transaction.Status = "Avbruten";

                Save();

                return true;
            }
        }

        // Här kör jag bara transaktioner vars kvart har kommit.
        // Varje resultat sparas.
        public void ProcessPending(DateTime now)
        {
            lock (SyncRoot)
            {
                if (disposed)
                {
                    return;
                }

                foreach (PendingTransaction transaction in
                    PendingTransactions.Where(transaction =>
                        transaction.Status == "Väntar" &&
                        transaction.ExecuteAt <= now)
                    .OrderBy(transaction => transaction.ExecuteAt)
                    .ThenBy(transaction => transaction.TransactionNumber)
                    .ToList())
                {
                    transaction.Status = "Behandlas";

                    bool success =
                        ExecuteTransaction(transaction, now, out string error);

                    transaction.Status =
                        success ? "Genomförd" : "Misslyckad";

                    transaction.Result = success
                        ? "Transaktionen är genomförd."
                        : error;

                    Save();
                }
            }
        }

        // Här gör jag en sista kontroll innan pengar flyttas
        // och transaktioner loggas.
        private bool ExecuteTransaction(
            PendingTransaction transaction,
            DateTime now,
            out string error)
        {
            error =
                "Transaktionen kunde inte genomföras. Kontrollera konto och saldo.";

            Kund? customer = Customers.Find(customer =>
                customer.CustomerNumber == transaction.CustomerNumber);

            Bankkonto? from =
                FindAccount(transaction.FromAccountNumber);

            Bankkonto? to =
                FindAccount(transaction.ToAccountNumber);

            if (customer == null ||
                !Bankkonto.IsValidAmount(transaction.Amount))
            {
                return false;
            }

            if ((transaction.Type == "Uttag" ||
                 transaction.Type == "Överföring") &&
                (from == null ||
                 !customer.Accounts.Contains(from)))
            {
                return false;
            }

            if ((transaction.Type == "Insättning" ||
                 transaction.Type == "Lån") &&
                (to == null ||
                 !customer.Accounts.Contains(to)))
            {
                return false;
            }

            if ((transaction.Type == "Insättning" ||
                 transaction.Type == "Lån") &&
                from != null)
            {
                return false;
            }

            if (transaction.Type == "Uttag" && to != null)
            {
                return false;
            }

            Bankkonto? account = from ?? to;

            if (account == null ||
                !TryConvert(
                    transaction.Amount,
                    account.Currency,
                    "SEK",
                    out decimal sek,
                    out error) ||
                !TryConvert(
                    transaction.Amount,
                    account.Currency,
                    to?.Currency ?? account.Currency,
                    out decimal received,
                    out error))
            {
                return false;
            }

            bool success = false;

            if (transaction.Type == "Insättning" && to != null)
            {
                success = to.Deposit(received);
            }

            if (transaction.Type == "Uttag" && from != null)
            {
                success = from.Withdraw(transaction.Amount);
            }

            if (transaction.Type == "Överföring" &&
                from != null &&
                to != null)
            {
                success = from.Transfer(
                    to,
                    transaction.Amount,
                    received);
            }

            // Här kollar jag lånegränsen igen
            // eftersom kundens pengar kan ha ändrats.
            if (transaction.Type == "Lån" &&
                to != null &&
                to.Currency == "SEK")
            {
                if (!TryLoanLimit(
                        customer,
                        out decimal limit,
                        out error) ||
                    transaction.Amount > limit)
                {
                    error =
                        "Lånegränsen räcker inte längre vid utbetalningen.";

                    return false;
                }

                success = to.Deposit(transaction.Amount);

                if (success)
                {
                    Loans.Add(new Loan
                    {
                        LoanNumber = ++NextLoanNumber,
                        CustomerNumber = customer.CustomerNumber,
                        Amount = transaction.Amount,
                        RemainingAmount = transaction.Amount,
                        InterestRate = transaction.LoanInterestRate,
                        Date = now
                    });
                }
            }

            if (!success)
            {
                error = "Saldo eller maxsaldo tillåter inte transaktionen.";
                return false;
            }

            // Här sparar jag händelsen på de konton
            // som pengarna gick från och till.
            if (from != null)
            {
                from.Transactions.Add(new Transaktion(
                    transaction.TransactionNumber,
                    transaction.Amount,
                    sek,
                    now,
                    transaction.Type == "Överföring"
                        ? "Utgående överföring"
                        : transaction.Type,
                    transaction.Message));
            }

            if (to != null)
            {
                to.Transactions.Add(new Transaktion(
                    transaction.TransactionNumber,
                    received,
                    sek,
                    now,
                    transaction.Type == "Överföring"
                        ? "Inkommande överföring"
                        : transaction.Type,
                    transaction.Message));
            }

            // Här skapar jag e-post till de berörda kunderna
            // om beloppet är över gränsen.
            if (sek > NotificationThreshold)
            {
                foreach (Kund owner in Customers.Where(owner =>
                    owner.Accounts.Contains(from!) ||
                    owner.Accounts.Contains(to!)))
                {
                    Bankkonto ownAccount =
                        owner.Accounts.Contains(to!) ? to! : from!;

                    decimal ownAmount =
                        ownAccount == to ? received : transaction.Amount;

                    Notifications.Add(new Notification
                    {
                        NotificationNumber = ++NextNotificationNumber,
                        CustomerNumber = owner.CustomerNumber,
                        EmailAddress = owner.EmailAddress,
                        Message =
                            $"Globen Bank: {transaction.Type} på " +
                            $"{ownAmount:F2} {ownAccount.Currency} " +
                            $"genomfördes {now.ToLocalTime():yyyy-MM-dd HH:mm}. " +
                            $"Transaktionsnummer: {transaction.TransactionNumber}."
                    });
                }
            }

            error = "";

            return true;
        }

        // Här skickar jag väntande e-post.
        // Ett fel sparas och leveransen försöks igen senare.
        public void SendNotifications()
        {
            List<Notification> pending;

            lock (SyncRoot)
            {
                if (disposed || sendingNotifications)
                {
                    return;
                }

                sendingNotifications = true;

                pending = Notifications.Where(notification =>
                    !notification.IsSent &&
                    notification.LastAttemptAt.AddMinutes(
                        Math.Min(
                            60,
                            Math.Pow(
                                2,
                                Math.Min(notification.Attempts, 6))))
                    <= DateTime.UtcNow)
                    .ToList();
            }

            try
            {
                foreach (Notification notification in pending)
                {
                    lock (SyncRoot)
                    {
                        if (disposed)
                        {
                            return;
                        }

                        notification.Attempts++;
                        notification.LastAttemptAt = DateTime.UtcNow;

                        Save();
                    }

                    bool sent =
                        notification.TrySendEmail(out string error);

                    lock (SyncRoot)
                    {
                        if (disposed)
                        {
                            return;
                        }

                        notification.IsSent = sent;
                        notification.Error = error;

                        Save();
                    }
                }
            }
            finally
            {
                lock (SyncRoot)
                {
                    sendingNotifications = false;
                }
            }
        }

        // Här lägger jag kundens supportärende sist i kön och sparar det.
        public bool AddSupportTicket(
            Kund customer,
            string description)
        {
            lock (SyncRoot)
            {
                if (!IsCustomer(customer) ||
                    string.IsNullOrWhiteSpace(description))
                {
                    return false;
                }

                SupportTickets.Enqueue(new SupportTicket
                {
                    TicketNumber = ++NextTicketNumber,
                    CustomerNumber = customer.CustomerNumber,
                    Description = description,
                    Date = DateTime.UtcNow
                });

                Save();

                return true;
            }
        }

        // Här tar administratören det äldsta supportärendet först.
        public SupportTicket? HandleSupportTicket(Kund administrator)
        {
            lock (SyncRoot)
            {
                if (!IsAdmin(administrator) ||
                    SupportTickets.Count == 0)
                {
                    return null;
                }

                SupportTicket ticket = SupportTickets.Dequeue();

                Save();

                return ticket;
            }
        }

        // Här räknar jag bara utgående överföringar
        // så samma pengar inte räknas två gånger.
        public string Statistics(Kund administrator)
        {
            lock (SyncRoot)
            {
                if (!IsAdmin(administrator))
                {
                    return "Behörighet saknas.";
                }

                List<Bankkonto> accounts =
                    Customers.SelectMany(customer => customer.Accounts)
                        .ToList();

                List<Transaktion> transfers =
                    accounts.SelectMany(account => account.Transactions)
                        .Where(transaction =>
                            transaction.Type == "Utgående överföring")
                        .ToList();

                StringBuilder result = new StringBuilder();

                result.AppendLine(
                    $"Kunder: {Customers.Count} | Konton: {accounts.Count}");

                result.AppendLine(
                    $"Genomförda överföringar: {transfers.Count}");

                result.AppendLine(
                    $"Totalt överfört: " +
                    $"{transfers.Sum(transaction => transaction.AmountSek):F2} SEK");

                result.AppendLine(
                    $"Väntande transaktioner: " +
                    $"{PendingTransactions.Count(transaction => transaction.Status == "Väntar")}");

                result.AppendLine(
                    "MEST ANVÄNDA KONTON (antal genomförda kontohändelser)");

                foreach (Bankkonto account in accounts
                    .OrderByDescending(account => account.Transactions.Count)
                    .Take(5))
                {
                    result.AppendLine(
                        $"{account.DisplayAccountNumber}: " +
                        $"{account.Transactions.Count}");
                }

                return result.ToString();
            }
        }
    }
}