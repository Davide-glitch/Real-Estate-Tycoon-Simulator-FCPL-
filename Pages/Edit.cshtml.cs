using System;
using System.Threading.Tasks;
using DavidEstateArchitect;
using DavidEstateArchitect.Data;
using DavidEstateArchitect.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DavidEstateArchitect.Pages
{
    public class EditModel : PageModel
    {
        private readonly AppDbContext _db;
        public EditModel(AppDbContext db) => _db = db;

        [BindProperty]
        public Property Property { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var entity = await _db.Properties.FindAsync(id);
            if (entity == null)
            {
                return NotFound();
            }
            Property = entity;
            return Page();
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

            var existing = await _db.Properties.FindAsync(Property.Id);
            if (existing == null)
            {
                return NotFound();
            }

            existing.Address = Property.Address;
            existing.City = Property.City;
            existing.County = Property.County;
            existing.Price = Property.Price;
            existing.MarketValue = Property.MarketValue > 0 ? Property.MarketValue : Property.Price;
            existing.Bedrooms = Property.Bedrooms;
            existing.Bathrooms = Property.Bathrooms;
            existing.SquareFeet = Property.SquareFeet;
            existing.Type = Property.Type;
            existing.Location = Property.Location;
            existing.Safety = Property.Safety;
            existing.MonthlyRent = Property.MonthlyRent;
            existing.MonthlyMaintenanceCost = Property.MonthlyMaintenanceCost;

            if (existing.Status == EstateStatus.ForSale && existing.ListedOn < DateTime.UtcNow)
            {
                // keep edited listings visible
                existing.ListedOn = DateTime.UtcNow.AddYears(5);
            }

            await _db.SaveChangesAsync();
            return RedirectToPage("Index");
        }
    }
}
