namespace DavidEstateArchitect.Models
{
    public enum PersonType
    {
        Buyer,
        Tenant
    }

    public class Person
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public PersonType Type { get; set; }
        public string DesiredCounty { get; set; } = string.Empty;
        public EstateType DesiredType { get; set; }
        public SafetyLevel DesiredSafety { get; set; }
        public decimal OfferAmount { get; set; }

        // Alternative offer
        public bool HasAlternative { get; set; }
        public SafetyLevel? AlternativeSafety { get; set; }
        public decimal AlternativeAmount { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime AppearedOn { get; set; } = DateTime.UtcNow;
        public int RoundNumber { get; set; }
    }
}
