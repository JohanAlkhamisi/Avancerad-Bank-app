using System;

namespace bank
{
    internal class SupportTicket
    {
        // Här sparar jag ärendets nummer.
        public int TicketNumber { get; set; }

        // Här sparar jag kundnumret för kunden som behöver hjälp.
        public int CustomerNumber { get; set; }

        // Här sparar jag kundens beskrivning av ärendet.
        public string Description { get; set; } = "";

        // Här sparar jag när ärendet registrerades.
        public DateTime Date { get; set; }
    }
}