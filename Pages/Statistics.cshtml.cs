
using DavidEstateArchitect.Data;
using DavidEstateArchitect.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DavidEstateArchitect.Pages
{
    public class StatisticsModel : PageModel
    {
        private readonly AppDbContext _db;

        public StatisticsModel(AppDbContext db)
        {
            _db = db;
        }

        public PlayerStatistics? Stats { get; set; }
        public GameState? State { get; set; }
        public List<PropertyTransaction> RecentTransactions { get; set; } = new();
        public List<MarketEvent> ActiveEvents { get; set; } = new();
        public List<Property> TopProperties { get; set; } = new();
        public List<Loan> ActiveLoans { get; set; } = new();
        public decimal TotalPropertyValue { get; set; }
        public decimal TotalDebt { get; set; }
        public int ActiveRentals { get; set; }

        public async Task OnGetAsync()
        {
            Stats = await _db.PlayerStatistics.FirstOrDefaultAsync();
            State = await _db.GameStates.FirstOrDefaultAsync();
            RecentTransactions = await _db.PropertyTransactions
                .OrderByDescending(t => t.TransactionDate)
                .Take(20)
                .ToListAsync();
            ActiveEvents = await _db.MarketEvents
                .Where(e => e.IsActive)
                .OrderByDescending(e => e.OccurredAt)
                .ToListAsync();
            // SQLite cannot translate decimals with conditional expressions in ORDER BY.
            // Load then sort in-memory to avoid provider limitations.
            var ownedForRanking = await _db.Properties
                .AsNoTracking()
                .Where(p => p.IsOwnedByPlayer)
                .ToListAsync();
            TopProperties = ownedForRanking
                .OrderByDescending(p => p.MarketValue > 0 ? p.MarketValue : p.Price)
                .Take(5)
                .ToList();
            ActiveLoans = await _db.Loans
                .Where(l => l.IsActive)
                .ToListAsync();

            var ownedProps = await _db.Properties.Where(p => p.IsOwnedByPlayer).ToListAsync();
            TotalPropertyValue = ownedProps.Sum(p => p.MarketValue > 0 ? p.MarketValue : p.Price);
            TotalDebt = ActiveLoans.Sum(l => l.Principal);
            ActiveRentals = await _db.RentalContracts.CountAsync(r => r.IsActive);
        }
    }
}
