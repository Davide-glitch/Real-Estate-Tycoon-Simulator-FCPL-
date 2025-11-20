namespace DavidEstateArchitect.Models
{
    public class Loan
    {
        public int Id { get; set; }
        public decimal Principal { get; set; }
        public decimal InterestRate { get; set; }
        public int DurationMonths { get; set; }
        public int MonthsRemaining { get; set; }
        public decimal MonthlyPayment { get; set; }
        public decimal TotalPaid { get; set; }
        public DateTime StartDate { get; set; } = DateTime.UtcNow;
        public DateTime EndDate { get; set; }
        public bool IsActive { get; set; } = true;
        public string Purpose { get; set; } = string.Empty;
    }
}
