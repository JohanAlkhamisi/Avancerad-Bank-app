namespace bank
{
    internal class PendingTransaction
    {
        // Här sparar jag transaktionen tills nästa kvart ska köras.
        public int TransactionNumber { get; set; }
        public int CustomerNumber { get; set; }
        public string Type { get; set; } = "";

        // Här sparar jag vilka konton pengarna ska gå från och till.
        public string FromAccountNumber { get; set; } = "";
        public string ToAccountNumber { get; set; } = "";

        // Här sparar jag beloppet och valutan som beloppet anges i.
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "SEK";

        // Här sparar jag räntan som gäller om transaktionen är ett lån.
        public decimal LoanInterestRate { get; set; }

        public string Message { get; set; } = "";

        // Här sparar jag när kunden begärde transaktionen och när den ska köras.
        public DateTime RequestedAt { get; set; }
        public DateTime ExecuteAt { get; set; }

        // Här sparar jag om transaktionen väntar, är genomförd eller misslyckades.
        public string Status { get; set; } = "Väntar";
        public string Result { get; set; } = "";
    }
}