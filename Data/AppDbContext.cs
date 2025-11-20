using Microsoft.EntityFrameworkCore;

namespace DavidEstateArchitect.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Models.Property> Properties { get; set; } = default!;
        // Add simulator entities
        public DbSet<DavidEstateArchitect.Models.GameState> GameStates { get; set; } = default!;
        public DbSet<DavidEstateArchitect.Models.Person> People { get; set; } = default!;
        public DbSet<DavidEstateArchitect.Models.RentalContract> RentalContracts { get; set; } = default!;
        public DbSet<DavidEstateArchitect.Models.MarketEvent> MarketEvents { get; set; } = default!;
        public DbSet<DavidEstateArchitect.Models.PlayerStatistics> PlayerStatistics { get; set; } = default!;
        public DbSet<DavidEstateArchitect.Models.PropertyTransaction> PropertyTransactions { get; set; } = default!;
        public DbSet<DavidEstateArchitect.Models.Loan> Loans { get; set; } = default!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure decimal precision for SQL Server to avoid truncation warnings
            modelBuilder.Entity<Models.GameState>()
                .Property(g => g.Balance)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Models.Person>(entity =>
            {
                entity.Property(p => p.OfferAmount).HasPrecision(18, 2);
                entity.Property(p => p.AlternativeAmount).HasPrecision(18, 2);
            });

            modelBuilder.Entity<Models.Property>(entity =>
            {
                entity.Property(p => p.Price).HasPrecision(18, 2);
                entity.Property(p => p.MonthlyRent).HasPrecision(18, 2);
            });

            modelBuilder.Entity<Models.RentalContract>()
                .Property(r => r.MonthlyRent)
                .HasPrecision(18, 2);

            // New models precision
            modelBuilder.Entity<Models.GameState>(entity =>
            {
                entity.Property(g => g.TotalNetWorth).HasPrecision(18, 2);
                entity.Property(g => g.BaseInterestRate).HasPrecision(5, 4);
                entity.Property(g => g.PropertyTaxRate).HasPrecision(5, 4);
            });

            modelBuilder.Entity<Models.Property>(entity =>
            {
                entity.Property(p => p.MarketValue).HasPrecision(18, 2);
                entity.Property(p => p.AnnualTaxRate).HasPrecision(5, 4);
                entity.Property(p => p.MonthlyMaintenanceCost).HasPrecision(18, 2);
                entity.Property(p => p.TotalAppreciation).HasPrecision(18, 2);
            });

            modelBuilder.Entity<Models.MarketEvent>()
                .Property(e => e.ImpactMultiplier)
                .HasPrecision(5, 4);

            modelBuilder.Entity<Models.PlayerStatistics>(entity =>
            {
                entity.Property(s => s.TotalMoneySpent).HasPrecision(18, 2);
                entity.Property(s => s.TotalMoneyEarned).HasPrecision(18, 2);
                entity.Property(s => s.TotalRentalIncome).HasPrecision(18, 2);
                entity.Property(s => s.TotalTaxesPaid).HasPrecision(18, 2);
                entity.Property(s => s.TotalMaintenancePaid).HasPrecision(18, 2);
                entity.Property(s => s.HighestBalance).HasPrecision(18, 2);
                entity.Property(s => s.NetWorth).HasPrecision(18, 2);
            });

            modelBuilder.Entity<Models.PropertyTransaction>()
                .Property(t => t.Amount)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Models.Loan>(entity =>
            {
                entity.Property(l => l.Principal).HasPrecision(18, 2);
                entity.Property(l => l.InterestRate).HasPrecision(5, 4);
                entity.Property(l => l.MonthlyPayment).HasPrecision(18, 2);
                entity.Property(l => l.TotalPaid).HasPrecision(18, 2);
            });
        }
    }
}
