namespace DavidEstateArchitect.Models
{
    public class RentalContract
    {
        public int Id { get; set; }
        public int PropertyId { get; set; }
        public Property? Property { get; set; }
        public int TenantId { get; set; }
        public Person? Tenant { get; set; }
        public decimal MonthlyRent { get; set; }
        public int DurationMonths { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int MonthsRemaining { get; set; }
        public DateTime LastRentPayment { get; set; }
        public bool IsActive { get; set; } = true;
        public double ChanceToLeave { get; set; } = 0.15; // Between 10% and 20%
        public int TotalMonthsStayed { get; set; } = 0;
    }
}
