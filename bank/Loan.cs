namespace bank
{
    internal class Loan
    {
        // Här sparar jag lånet. Lånen är i SEK och räntan visas för ett år.
        public int LoanNumber { get; set; }
        public int CustomerNumber { get; set; }
        public decimal Amount { get; set; }
        public decimal RemainingAmount { get; set; }
        public decimal InterestRate { get; set; }
        public DateTime Date { get; set; }

        // Här räknar jag ett års ränta på lånet och avrundar till två decimaler.
        public decimal YearlyInterest =>
            Math.Round(RemainingAmount * InterestRate / 100m, 2);
    }
}