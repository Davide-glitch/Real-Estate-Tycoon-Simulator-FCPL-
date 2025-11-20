using DavidEstateArchitect.Data;
using DavidEstateArchitect.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

namespace DavidEstateArchitect.Pages
{
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _db;
        public IndexModel(AppDbContext db) => _db = db;

        public IList<Property> Properties { get; set; } = new List<Property>();

        [BindProperty(SupportsGet = true)]
        public string? q { get; set; }

        public async Task OnGetAsync()
        {
            var queryable = _db.Properties.AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                var pattern = $"%{q.Trim()}%";
                queryable = queryable.Where(p =>
                    EF.Functions.Like(p.Address, pattern) ||
                    EF.Functions.Like(p.City, pattern) ||
                    EF.Functions.Like(p.County, pattern));
            }

            Properties = await queryable
                .OrderByDescending(p => p.ListedOn)
                .Take(200)
                .ToListAsync();
        }

        public async Task<IActionResult> OnPostResetGameAsync()
        {
            // Ensure the game is paused before resetting
            var state = await _db.GameStates.FirstOrDefaultAsync();
            if (state == null || !state.IsPaused)
            {
                return BadRequest("Game must be paused to reset.");
            }

            // Reset game state
            state.Balance = 10000;
            state.CurrentRound = 1;
            state.TotalNetWorth = 10000;
            state.IsPaused = false;

            // Reset properties
            var properties = await _db.Properties.ToListAsync();
            _db.Properties.RemoveRange(properties);

            _db.Properties.Add(new Property
            {
                Address = "123 Starter St",
                City = "Starter City",
                County = "AR",
                Price = 4500,
                Type = EstateType.Apartment,
                Location = LocationType.OutsideCity,
                Safety = SafetyLevel.Risky,
                Status = EstateStatus.ForSale
            });

            _db.Properties.Add(new Property
            {
                Address = "456 Beginner Ave",
                City = "Beginner Town",
                County = "TM",
                Price = 5500,
                Type = EstateType.Apartment,
                Location = LocationType.AroundCity,
                Safety = SafetyLevel.Moderate,
                Status = EstateStatus.ForSale
            });

            await _db.SaveChangesAsync();

            return RedirectToPage("/Index");
        }
    }
}
