using System.Security.Cryptography;
using System.Text.Json.Serialization;

namespace bank
{
    internal class Kund
    {
        // Här sparar jag kundens uppgifter och om kunden är administratör.
        // JsonInclude låter sparfilen läsa tillbaka uppgifter som har private set.
        [JsonInclude] public int CustomerNumber { get; private set; }
        [JsonInclude] public string Username { get; private set; } = "";
        [JsonInclude] public string FirstName { get; private set; } = "";
        [JsonInclude] public string LastName { get; private set; } = "";
        [JsonInclude] public string EmailAddress { get; private set; } = "";
        [JsonInclude] public string PhoneNumber { get; private set; } = "";
        [JsonInclude] public bool IsAdmin { get; private set; }
        [JsonInclude] public int CreditRating { get; private set; } = 2;

        // Här sparar jag en hash av lösenordet och räknar felaktiga försök.
        [JsonInclude] public string PasswordHash { get; private set; } = "";
        [JsonInclude] public int FailedLoginAttempts { get; private set; }

        // Här kollar jag om kunden har misslyckats med inloggningen tre gånger.
        [JsonIgnore] public bool IsLocked => FailedLoginAttempts >= 3;

        // Här sparar jag kundens bankkonton.
        public List<Bankkonto> Accounts { get; set; } = new List<Bankkonto>();

        // Här behöver sparfilen en tom konstruktor när uppgifterna läses in.
        public Kund() { }

        // Här fyller jag kunden med uppgifterna som angavs vid registreringen.
        public Kund(
            int customerNumber,
            string username,
            string firstName,
            string lastName,
            string emailAddress,
            string phoneNumber,
            string password,
            bool isAdmin)
        {
            CustomerNumber = customerNumber;
            Username = username;
            FirstName = firstName;
            LastName = lastName;
            EmailAddress = emailAddress;
            PhoneNumber = phoneNumber;
            IsAdmin = isAdmin;

            SetPassword(password);
        }

        // Här gör jag lösenordet till en hash med ett eget slumpvärde.
        private void SetPassword(string password)
        {
            // Här gör slumpvärdet att samma lösenord kan få olika hash för olika kunder.
            byte[] salt = RandomNumberGenerator.GetBytes(16);

            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                210000,
                HashAlgorithmName.SHA256,
                32);

            PasswordHash =
                Convert.ToBase64String(salt) + ":" +
                Convert.ToBase64String(hash);
        }

        // Här kollar jag lösenordet och spärrar kunden efter tre fel.
        public bool Authenticate(string password)
        {
            if (IsLocked)
            {
                return false;
            }

            // Här delar jag upp det sparade slumpvärdet och lösenordets hash.
            string[] parts = PasswordHash.Split(':');

            if (parts.Length != 2)
            {
                return false;
            }

            byte[] salt = Convert.FromBase64String(parts[0]);
            byte[] expected = Convert.FromBase64String(parts[1]);

            byte[] actual = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                210000,
                HashAlgorithmName.SHA256,
                32);

            // Här jämför jag den nya hashen med den som sparades vid registreringen.
            if (CryptographicOperations.FixedTimeEquals(actual, expected))
            {
                FailedLoginAttempts = 0;
                return true;
            }

            FailedLoginAttempts++;

            return false;
        }

        // Här ändrar jag uppgifterna efter att Bank har kontrollerat dem.
        internal void UpdateDetails(string emailAddress, string phoneNumber)
        {
            EmailAddress = emailAddress;
            PhoneNumber = phoneNumber;
        }

        // Här skapar jag en ny hash när kunden byter lösenord.
        internal void UpdatePassword(string password) => SetPassword(password);

        // Här nollställer jag försöken när administratören låser upp kunden.
        internal void Unlock() => FailedLoginAttempts = 0;

        // Här sparar jag kreditvärdigheten som administratören har valt.
        internal void SetCreditRating(int creditRating) =>
            CreditRating = creditRating;
    }
}