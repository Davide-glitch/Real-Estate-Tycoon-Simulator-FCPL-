namespace DavidEstateArchitect.Models
{
    public class GameState
    {
        public int Id { get; set; }
        public decimal Balance { get; set; } = 10000m;
        public int CurrentRound { get; set; } = 1;
        public DateTime LastRoundTime { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Enhanced tracking
        public decimal TotalNetWorth { get; set; } = 10000m;
        public decimal BaseInterestRate { get; set; } = 0.05m; // 5% base rate
        public decimal PropertyTaxRate { get; set; } = 0.012m; // 1.2% annual
        public int LastTaxRound { get; set; } = 0;
        public int LastMaintenanceRound { get; set; } = 0;
        public bool IsPaused { get; set; } = false; // Game pause state

        // Victory/Defeat tracking
        public bool HasWon { get; set; } = false;
        public bool HasLost { get; set; } = false;
        public DateTime? GameEndTime { get; set; }
        public string? VictoryMessage { get; set; }
    }
}
