namespace DavidEstateArchitect.Models
{
    public enum EventType
    {
        PriceIncrease,
        PriceDecrease,
        SafetyImprovement,
        SafetyDegradation,
        TaxChange,
        InterestRateChange,
        NaturalDisaster,
        EconomicBoom,
        Recession
    }

    public class MarketEvent
    {
        public int Id { get; set; }
        public EventType Type { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? AffectedCounty { get; set; } // null means all counties
        public decimal ImpactMultiplier { get; set; } = 1.0m;
        public int DurationRounds { get; set; }
        public int RoundsRemaining { get; set; }
        public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;
    }
}
