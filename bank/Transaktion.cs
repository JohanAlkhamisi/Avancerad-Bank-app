namespace bank
{
    internal class Transaktion
    {
        // Här sparar jag beloppet i kontots valuta och i SEK för statistiken.
        public int TransactionNumber { get; set; }
        public decimal Amount { get; set; }
        public decimal AmountSek { get; set; }
        public DateTime Date { get; set; }
        public string Type { get; set; } = "";
        public string Message { get; set; } = "";

        // Här använder sparfilen en tom konstruktor när transaktionen läses tillbaka.
        public Transaktion() { }

        // Här fyller jag transaktionen med uppgifterna från den genomförda händelsen.
        public Transaktion(
            int transactionNumber,
            decimal amount,
            decimal amountSek,
            DateTime date,
            string type,
            string message)
        {
            TransactionNumber = transactionNumber;
            Amount = amount;
            AmountSek = amountSek;
            Date = date;
            Type = type;
            Message = message;
        }
    }
}