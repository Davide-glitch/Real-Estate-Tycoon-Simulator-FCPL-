using System;
using System.Linq;
using DavidEstateArchitect;
using DavidEstateArchitect.Data;
using DavidEstateArchitect.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DavidEstateArchitect.Pages
{
    public class CreateModel : PageModel
    {
        private readonly AppDbContext _db;
        public CreateModel(AppDbContext db) => _db = db;

        [BindProperty]
        public Property Property { get; set; } = new();

        public void OnGet()
        {
            Property.Status = EstateStatus.ForSale;
            Property.Type = EstateType.Apartment;
            Property.Location = LocationType.OutsideCity;
            Property.Safety = SafetyLevel.Moderate;
            Property.County = GameConstants.Counties.FirstOrDefault() ?? string.Empty;
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrWhiteSpace(Property.County))
            {
                ModelState.AddModelError("Property.County", "County is required.");
            }

            if (!ModelState.IsValid)
            {
                return Page();
            }

            Property.Status = EstateStatus.ForSale;
            Property.IsOwnedByPlayer = false;
            Property.ListedOn = DateTime.UtcNow.AddYears(5); // keep user listings on market
            Property.PurchaseDate = DateTime.UtcNow;
            Property.LastMaintenancePaid = DateTime.UtcNow;

            if (Property.Price > 0)
            {
                Property.MarketValue = Property.MarketValue > 0 ? Property.MarketValue : Property.Price;
                if (Property.MonthlyRent <= 0)
                {
                    Property.MonthlyRent = Math.Round(Property.Price * 0.008m, 0);
                }
                if (Property.MonthlyMaintenanceCost <= 0)
                {
                    Property.MonthlyMaintenanceCost = Math.Round(Property.Price * 0.004m, 0);
                }
            }

            _db.Properties.Add(Property);
            await _db.SaveChangesAsync();
            return RedirectToPage("Index");
        }
    }
}
