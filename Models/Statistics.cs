namespace DavidEstateArchitect.Models
{
    public class PlayerStatistics
    {
        public int Id { get; set; }
        public int TotalPropertiesBought { get; set; }
        public int TotalPropertiesSold { get; set; }
        public int TotalPropertiesRented { get; set; }
        public decimal TotalMoneySpent { get; set; }
        public decimal TotalMoneyEarned { get; set; }
        public decimal TotalRentalIncome { get; set; }
        public decimal TotalTaxesPaid { get; set; }
        public decimal TotalMaintenancePaid { get; set; }
        public decimal HighestBalance { get; set; }
        public decimal NetWorth { get; set; }
        public int RoundsPlayed { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }

    public class PropertyTransaction
    {
        public int Id { get; set; }
        public int PropertyId { get; set; }
        public TransactionType Type { get; set; }
        public decimal Amount { get; set; }
        public string Details { get; set; } = string.Empty;
        public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
        public int RoundNumber { get; set; }
    }

    public enum TransactionType
    {
        Purchase,
        Sale,
        RentalIncome,
        Renovation,
        Tax,
        Maintenance,
        EventImpact
    }
}
