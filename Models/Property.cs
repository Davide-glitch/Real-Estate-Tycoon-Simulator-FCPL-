namespace DavidEstateArchitect.Models
{
    public enum EstateType
    {
        Apartment,
        House,
        Mansion
    }

    public enum LocationType
    {
        Center,
        AroundCity,
        OutsideCity
    }

    public enum SafetyLevel
    {
        Safe,
        Moderate,
        Risky
    }

    public enum EstateStatus
    {
        ForSale,
        Owned,
        Rented
    }

    public class Property
    {
        public int Id { get; set; }
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Bedrooms { get; set; }
        public int Bathrooms { get; set; }
        public int SquareFeet { get; set; }
        public DateTime ListedOn { get; set; } = DateTime.UtcNow;

        // Game properties
        public string County { get; set; } = string.Empty;
        public EstateType Type { get; set; }
        public LocationType Location { get; set; }
        public SafetyLevel Safety { get; set; }
        public EstateStatus Status { get; set; } = EstateStatus.ForSale;
        public int RenovationLevel { get; set; } = 0;
        public decimal MonthlyRent { get; set; }
        public bool IsOwnedByPlayer { get; set; } = false;
        public int? CurrentTenantId { get; set; }

        // Enhanced features
        public decimal MarketValue { get; set; } // Can differ from original price
        public decimal AnnualTaxRate { get; set; } = 0.012m; // 1.2% default
        public decimal MonthlyMaintenanceCost { get; set; }
        public DateTime LastMaintenancePaid { get; set; } = DateTime.UtcNow;
        public DateTime PurchaseDate { get; set; } = DateTime.UtcNow;
        public int YearsOwned { get; set; } = 0;
        public decimal TotalAppreciation { get; set; } = 0m;
    }
}
