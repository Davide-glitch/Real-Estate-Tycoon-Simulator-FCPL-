using DavidEstateArchitect.Data;
using DavidEstateArchitect.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DavidEstateArchitect.Pages
{
    public class DeleteModel : PageModel
    {
        private readonly AppDbContext _db;
        public DeleteModel(AppDbContext db) => _db = db;

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
            var entity = await _db.Properties.FindAsync(Property.Id);
            if (entity == null)
            {
                return NotFound();
            }
            _db.Properties.Remove(entity);
            await _db.SaveChangesAsync();
            return RedirectToPage("Index");
        }
    }
}
