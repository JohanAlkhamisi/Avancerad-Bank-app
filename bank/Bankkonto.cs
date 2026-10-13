using System.Text.Json.Serialization;

namespace bank
{
    internal class Bankkonto
    {
        // Här sätter jag en gräns så orimligt stora belopp inte godkänns.
        public const decimal MaximumBalance = 1000000000m;

        // Här sparar jag saldot. Bara Bankkonto kan ändra det direkt.
        [JsonInclude] public decimal Balance { get; private set; }

        // Här sparar jag kontonummer, kontotyp och valutan som kontot använder.
        public string AccountNumber { get; set; } = "";
        public string AccountType { get; set; } = "Privatkonto";
        public string Currency { get; set; } = "SEK";

        // Här sparar jag alla genomförda händelser på det här kontot.
        public List<Transaktion> Transactions { get; set; } =
            new List<Transaktion>();

        // Här lägger jag in skiljetecken så kontonumret blir lättare att läsa.
        [JsonIgnore]
        public string DisplayAccountNumber =>
            AccountNumber.Substring(0, 4) + ", " +
            AccountNumber.Substring(4, 2) + "-" +
            AccountNumber.Substring(6);

        // Här använder sparfilen en tom konstruktor när kontot läses tillbaka.
        public Bankkonto() { }

        // Här skapar jag ett konto med numret, kontotypen och valutan som har valts.
        public Bankkonto(
            string accountNumber,
            string accountType,
            string currency)
        {
            AccountNumber = accountNumber;
            AccountType = accountType;
            Currency = currency;
        }

        // Här ändrar jag bara saldot. Bank sparar sedan transaktionsloggen.
        internal bool Deposit(decimal amount)
        {
            if (!IsValidAmount(amount) ||
                amount > MaximumBalance - Balance)
            {
                return false;
            }

            Balance += amount;

            return true;
        }

        // Här drar jag bort pengarna om beloppet är giltigt och saldot räcker.
        internal bool Withdraw(decimal amount)
        {
            if (!IsValidAmount(amount) || amount > Balance)
            {
                return false;
            }

            Balance -= amount;

            return true;
        }

        // Här kontrollerar jag beloppet innan båda saldona ändras.
        internal bool Transfer(
            Bankkonto recipient,
            decimal amount,
            decimal receivedAmount)
        {
            if (recipient == this ||
                !IsValidAmount(amount) ||
                !IsValidAmount(receivedAmount) ||
                amount > Balance ||
                receivedAmount > MaximumBalance - recipient.Balance)
            {
                return false;
            }

            Balance -= amount;
            recipient.Balance += receivedAmount;

            return true;
        }

        // Här kollar jag att beloppet är positivt, ryms inom gränsen
        // och har högst två decimaler.
        public static bool IsValidAmount(decimal amount) =>
            amount > 0 &&
            amount <= MaximumBalance &&
            amount == Math.Round(amount, 2);

        // Här tar jag bort tillåtna skiljetecken när kontonummer jämförs.
        public static string NormalizeAccountNumber(string accountNumber)
        {
            string digits = "";

            foreach (char character in accountNumber)
            {
                if (char.IsAsciiDigit(character))
                {
                    digits += character;
                }
                else if (!char.IsWhiteSpace(character) &&
                         character != ',' &&
                         character != '-')
                {
                    return "";
                }
            }

            return digits;
        }
    }
}