using DavidEstateArchitect.Data;
using DavidEstateArchitect.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Linq;

namespace DavidEstateArchitect.Pages
{
    public class GameModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly ILogger<GameModel> _logger;

        public GameModel(AppDbContext db, ILogger<GameModel> logger)
        {
            _db = db;
            _logger = logger;
        }

        public decimal Balance { get; set; }
        public int Round { get; set; }
        public decimal NetWorth { get; set; }
        public decimal TotalPropertyValue { get; set; }
        public List<Property> ForSale { get; set; } = new();
        public List<Property> Owned { get; set; } = new();
        public List<Person> Offers { get; set; } = new();
        public List<MarketEvent> ActiveEvents { get; set; } = new();
        public int ActiveRentals { get; set; }
        public bool IsPaused { get; set; }
        [BindProperty(SupportsGet = true)]
        public string? SearchCity { get; set; }
        [BindProperty(SupportsGet = true)]
        public string? SearchCounty { get; set; }
        [BindProperty(SupportsGet = true)]
        public LocationType? SearchLocation { get; set; }
        public IEnumerable<SelectListItem> CountyOptions => GameConstants.Counties
            .Select(c => new SelectListItem { Value = c, Text = c });
        public IEnumerable<SelectListItem> LocationOptions => Enum.GetValues(typeof(LocationType))
            .Cast<LocationType>()
            .Select(loc => new SelectListItem { Value = loc.ToString(), Text = loc.ToString() });

        public async Task OnGet()
        {
            var state = await _db.GameStates.FirstOrDefaultAsync();
            if (state == null)
            {
                state = new GameState { Balance = 10000, CurrentRound = 1, LastRoundTime = DateTime.UtcNow, CreatedAt = DateTime.UtcNow };
                _db.GameStates.Add(state);
                await _db.SaveChangesAsync();
            }
            Balance = state.Balance;
            Round = state.CurrentRound;
            NetWorth = state.TotalNetWorth;
            IsPaused = state.IsPaused;

            // Show cheapest available properties first so starter deals are visible
            var forSaleQuery = _db.Properties
                .Where(p => p.Status == EstateStatus.ForSale);

            if (!string.IsNullOrWhiteSpace(SearchCity))
            {
                var cityPattern = $"%{SearchCity.Trim()}%";
                forSaleQuery = forSaleQuery.Where(p => EF.Functions.Like(p.City, cityPattern));
            }

            if (!string.IsNullOrWhiteSpace(SearchCounty))
            {
                forSaleQuery = forSaleQuery.Where(p => p.County == SearchCounty);
            }

            if (SearchLocation.HasValue)
            {
                var location = SearchLocation.Value;
                forSaleQuery = forSaleQuery.Where(p => p.Location == location);
            }

            ForSale = await forSaleQuery
                .OrderBy(p => (double)p.Price) // Convert decimal to double for SQLite compatibility
                .ThenByDescending(p => p.ListedOn)
                .Take(60)
                .ToListAsync();
            Owned = await _db.Properties.Where(p => p.IsOwnedByPlayer).OrderBy(p => p.Id).ToListAsync();
            Offers = await _db.People.Where(p => p.IsActive).OrderByDescending(p => p.Id).Take(20).ToListAsync();
            ActiveEvents = await _db.MarketEvents.Where(e => e.IsActive).OrderByDescending(e => e.OccurredAt).Take(5).ToListAsync();
            ActiveRentals = await _db.RentalContracts.CountAsync(r => r.IsActive);

            TotalPropertyValue = Owned.Sum(p => p.MarketValue > 0 ? p.MarketValue : p.Price);
        }

        public async Task<IActionResult> OnPostBuyAsync(int id)
        {
            _logger.LogInformation("OnPostBuyAsync called with id: {Id}", id);
            var state = await _db.GameStates.FirstOrDefaultAsync();
            if (state == null || state.IsPaused)
            {
                _logger.LogWarning("GameState not found or game is paused");
                return RedirectToPage();
            }
            var prop = await _db.Properties.FindAsync(id);
            if (prop == null || prop.Status != EstateStatus.ForSale)
            {
                _logger.LogWarning("Property not found or not for sale. Id: {Id}", id);
                return RedirectToPage();
            }
            if (state.Balance < prop.Price)
            {
                _logger.LogWarning("Insufficient balance. Balance: {Balance}, Price: {Price}", state.Balance, prop.Price);
                return RedirectToPage();
            }

            state.Balance -= prop.Price;
            prop.IsOwnedByPlayer = true;
            prop.Status = EstateStatus.Owned;
            prop.MarketValue = prop.Price;
            prop.PurchaseDate = DateTime.UtcNow;

            // Update statistics
            var stats = await _db.PlayerStatistics.FirstOrDefaultAsync();
            if (stats != null)
            {
                stats.TotalPropertiesBought++;
                stats.TotalMoneySpent += prop.Price;
            }

            // Record transaction
            _db.PropertyTransactions.Add(new PropertyTransaction
            {
                PropertyId = prop.Id,
                Type = TransactionType.Purchase,
                Amount = -prop.Price,
                Details = $"Purchased {prop.Type} in {prop.County}",
                RoundNumber = state.CurrentRound
            });

            await _db.SaveChangesAsync();
            _logger.LogInformation("Property purchased successfully. Id: {Id}, Price: {Price}", id, prop.Price);
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostRenovateAsync(int id)
        {
            var state = await _db.GameStates.FirstOrDefaultAsync();
            if (state == null || state.IsPaused) return RedirectToPage();
            var prop = await _db.Properties.FindAsync(id);
            if (prop == null || !prop.IsOwnedByPlayer) return RedirectToPage();
            if (state.Balance < 5) return RedirectToPage();

            state.Balance -= 5;
            prop.RenovationLevel += 1;

            // Update statistics
            var stats = await _db.PlayerStatistics.FirstOrDefaultAsync();
            if (stats != null)
            {
                stats.TotalMoneySpent += 5;
            }

            // Record transaction
            _db.PropertyTransactions.Add(new PropertyTransaction
            {
                PropertyId = prop.Id,
                Type = TransactionType.Renovation,
                Amount = -5,
                Details = $"Renovation level {prop.RenovationLevel}",
                RoundNumber = state.CurrentRound
            });

            await _db.SaveChangesAsync();
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostSellAsync(int id, int propertyId)
        {
            var state = await _db.GameStates.FirstOrDefaultAsync();
            if (state == null || state.IsPaused) return RedirectToPage();
            var offer = await _db.People.FindAsync(id);
            var prop = await _db.Properties.FindAsync(propertyId);
            if (offer == null || prop == null) return RedirectToPage();
            if (offer.Type != PersonType.Buyer) return RedirectToPage();
            if (!prop.IsOwnedByPlayer || prop.Status != EstateStatus.Owned) return RedirectToPage();
            if (prop.County != offer.DesiredCounty || prop.Type != offer.DesiredType || prop.Safety != offer.DesiredSafety) return RedirectToPage();

            // sell
            state.Balance += offer.OfferAmount;
            prop.IsOwnedByPlayer = false;
            prop.Status = EstateStatus.ForSale;
            offer.IsActive = false;

            // Update statistics
            var stats = await _db.PlayerStatistics.FirstOrDefaultAsync();
            if (stats != null)
            {
                stats.TotalPropertiesSold++;
                stats.TotalMoneyEarned += offer.OfferAmount;
            }

            // Record transaction
            _db.PropertyTransactions.Add(new PropertyTransaction
            {
                PropertyId = prop.Id,
                Type = TransactionType.Sale,
                Amount = offer.OfferAmount,
                Details = $"Sold to {offer.Name}",
                RoundNumber = state.CurrentRound
            });

            await _db.SaveChangesAsync();
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostRentAsync(int id, int propertyId, int duration)
        {
            var state = await _db.GameStates.FirstOrDefaultAsync();
            if (state == null || state.IsPaused) return RedirectToPage();
            var offer = await _db.People.FindAsync(id);
            var prop = await _db.Properties.FindAsync(propertyId);
            if (offer == null || prop == null) return RedirectToPage();
            if (offer.Type != PersonType.Tenant) return RedirectToPage();
            if (!prop.IsOwnedByPlayer || prop.Status != EstateStatus.Owned) return RedirectToPage();
            if (prop.County != offer.DesiredCounty || prop.Type != offer.DesiredType || prop.Safety != offer.DesiredSafety) return RedirectToPage();
            if (!GameConstants.RentalDurations.Contains(duration)) return RedirectToPage();

            var now = DateTime.UtcNow;
            var contract = new RentalContract
            {
                PropertyId = prop.Id,
                TenantId = offer.Id,
                MonthlyRent = Math.Max(prop.MonthlyRent, offer.OfferAmount),
                DurationMonths = duration,
                StartDate = now,
                EndDate = now.AddMonths(duration),
                MonthsRemaining = duration,
                LastRentPayment = now,
                IsActive = true,
                ChanceToLeave = 0.1 + new Random().NextDouble() * 0.1
            };

            prop.Status = EstateStatus.Rented;
            prop.CurrentTenantId = offer.Id;
            offer.IsActive = false;

            _db.RentalContracts.Add(contract);
            await _db.SaveChangesAsync();
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostTogglePauseAsync()
        {
            _logger.LogInformation("OnPostTogglePauseAsync called");
            var state = await _db.GameStates.FirstOrDefaultAsync();
            if (state == null) return RedirectToPage();

            state.IsPaused = !state.IsPaused;
            await _db.SaveChangesAsync();
            _logger.LogInformation("Game pause state toggled to: {IsPaused}", state.IsPaused);
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostSetRentAsync(int id, decimal rentPrice)
        {
            _logger.LogInformation("OnPostSetRentAsync called with id: {Id}, rentPrice: {RentPrice}", id, rentPrice);
            var state = await _db.GameStates.FirstOrDefaultAsync();
            if (state == null || state.IsPaused) return RedirectToPage();

            var prop = await _db.Properties.FindAsync(id);
            if (prop == null || !prop.IsOwnedByPlayer) return RedirectToPage();

            if (rentPrice < 10 || rentPrice > 50000)
            {
                _logger.LogWarning("Invalid rent price: {RentPrice}", rentPrice);
                return RedirectToPage();
            }

            prop.MonthlyRent = rentPrice;
            await _db.SaveChangesAsync();
            _logger.LogInformation("Monthly rent set to {RentPrice} for property {Id}", rentPrice, id);
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostSetSalePriceAsync(int id, decimal salePrice)
        {
            _logger.LogInformation("OnPostSetSalePriceAsync called with id: {Id}, salePrice: {SalePrice}", id, salePrice);
            var state = await _db.GameStates.FirstOrDefaultAsync();
            if (state == null || state.IsPaused) return RedirectToPage();

            var prop = await _db.Properties.FindAsync(id);
            if (prop == null || !prop.IsOwnedByPlayer) return RedirectToPage();

            if (salePrice < 1000 || salePrice > 1000000)
            {
                _logger.LogWarning("Invalid sale price: {SalePrice}", salePrice);
                return RedirectToPage();
            }

            prop.Price = salePrice;
            prop.MarketValue = salePrice;
            await _db.SaveChangesAsync();
            _logger.LogInformation("Sale price set to {SalePrice} for property {Id}", salePrice, id);
            return RedirectToPage();
        }

        // Quick buy: create and purchase a starter property directly (useful when no cheap listings are visible)
        public async Task<IActionResult> OnPostQuickBuyAsync()
        {
            var state = await _db.GameStates.FirstOrDefaultAsync();
            if (state == null || state.IsPaused) return RedirectToPage();

            // determine affordable maximum
            var maxPrice = Math.Min(9000m, state.Balance);
            if (maxPrice < 1000m)
            {
                // nothing affordable
                return RedirectToPage();
            }

            var rand = new Random();
            var price = Math.Round((decimal)(3000 + rand.Next(0, (int)Math.Max(0, (double)(maxPrice - 3000m + 1)))), 0);

            var streetNames = new[] { "Maple St", "Oak Ave", "Pine Rd", "Cedar Ln", "Elm St", "Birch Way", "Willow Dr", "Poplar Ct" };
            var cities = new[] { "Springfield", "Riverton", "Lakeside", "Hillview", "Fairview", "Brookfield" };

            var prop = new Property
            {
                Address = $"{rand.Next(1, 9999)} {streetNames[rand.Next(streetNames.Length)]}",
                City = cities[rand.Next(cities.Length)],
                County = GameConstants.Counties[rand.Next(GameConstants.Counties.Length)],
                Type = EstateType.Apartment,
                Location = LocationType.OutsideCity,
                Safety = rand.NextDouble() < 0.6 ? SafetyLevel.Risky : SafetyLevel.Moderate,
                Price = price,
                MarketValue = price,
                Status = EstateStatus.Owned,
                Bedrooms = 1,
                Bathrooms = 1,
                SquareFeet = rand.Next(400, 800),
                MonthlyRent = Math.Round(price * 0.01m, 0),
                MonthlyMaintenanceCost = Math.Round(price * 0.004m, 0),
                IsOwnedByPlayer = true,
                PurchaseDate = DateTime.UtcNow
            };

            // Deduct balance and save
            state.Balance -= prop.Price;

            var stats = await _db.PlayerStatistics.FirstOrDefaultAsync();
            if (stats != null)
            {
                stats.TotalPropertiesBought++;
                stats.TotalMoneySpent += prop.Price;
            }

            _db.Properties.Add(prop);
            _db.PropertyTransactions.Add(new PropertyTransaction
            {
                PropertyId = prop.Id,
                Type = TransactionType.Purchase,
                Amount = -prop.Price,
                Details = "Quick starter purchase",
                RoundNumber = state.CurrentRound
            });

            await _db.SaveChangesAsync();
            _logger.LogInformation("Quick starter property purchased for {Price}", prop.Price);
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostResetGame()
        {
            // Delete all game data and reset to initial state
            var state = await _db.GameStates.FirstOrDefaultAsync();
            if (state != null)
            {
                // Delete all properties
                var properties = await _db.Properties.ToListAsync();
                _db.Properties.RemoveRange(properties);

                // Delete all offers/people
                var people = await _db.People.ToListAsync();
                _db.People.RemoveRange(people);

                // Delete all rental contracts
                var contracts = await _db.RentalContracts.ToListAsync();
                _db.RentalContracts.RemoveRange(contracts);

                // Delete all market events
                var events = await _db.MarketEvents.ToListAsync();
                _db.MarketEvents.RemoveRange(events);

                // Delete all player statistics
                var stats = await _db.PlayerStatistics.ToListAsync();
                _db.PlayerStatistics.RemoveRange(stats);

                // Delete all property transactions
                var transactions = await _db.PropertyTransactions.ToListAsync();
                _db.PropertyTransactions.RemoveRange(transactions);

                // Reset game state
                state.Balance = 10000;
                state.CurrentRound = 1;
                state.LastRoundTime = DateTime.UtcNow;
                state.IsPaused = false;
                state.TotalNetWorth = 10000;

                await _db.SaveChangesAsync();
                _logger.LogInformation("Game reset successfully");
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostSellPropertyAsync(int id)
        {
            _logger.LogInformation("OnPostSellPropertyAsync called with id: {Id}", id);
            var state = await _db.GameStates.FirstOrDefaultAsync();
            if (state == null || state.IsPaused) return RedirectToPage();

            var prop = await _db.Properties.FindAsync(id);
            if (prop == null || !prop.IsOwnedByPlayer) return RedirectToPage();

            // Sell at current market value
            var salePrice = prop.MarketValue > 0 ? prop.MarketValue : prop.Price;
            state.Balance += salePrice;
            prop.IsOwnedByPlayer = false;
            prop.Status = EstateStatus.ForSale;
            // mark listing time so cleanup and ordering work correctly
            prop.ListedOn = DateTime.UtcNow;

            // Update statistics
            var stats = await _db.PlayerStatistics.FirstOrDefaultAsync();
            if (stats != null)
            {
                stats.TotalPropertiesSold++;
                stats.TotalMoneyEarned += salePrice;
            }

            // Record transaction
            _db.PropertyTransactions.Add(new PropertyTransaction
            {
                PropertyId = prop.Id,
                Type = TransactionType.Sale,
                Amount = salePrice,
                Details = "Sold by player",
                RoundNumber = state.CurrentRound
            });

            await _db.SaveChangesAsync();
            _logger.LogInformation("Property sold for {Price}", salePrice);
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostRentPropertyAsync(int id, int duration, string tenantName)
        {
            _logger.LogInformation("OnPostRentPropertyAsync called with id: {Id}, duration: {Duration}", id, duration);
            var state = await _db.GameStates.FirstOrDefaultAsync();
            if (state == null || state.IsPaused) return RedirectToPage();

            var prop = await _db.Properties.FindAsync(id);
            if (prop == null || !prop.IsOwnedByPlayer) return RedirectToPage();
            if (!GameConstants.RentalDurations.Contains(duration)) return RedirectToPage();

            // Create a tenant person
            var tenant = new Person
            {
                Name = string.IsNullOrWhiteSpace(tenantName) ? "Tenant #" + id : tenantName,
                Type = PersonType.Tenant,
                DesiredCounty = prop.County,
                DesiredType = prop.Type,
                DesiredSafety = prop.Safety,
                OfferAmount = prop.MonthlyRent,
                IsActive = true,
                RoundNumber = state.CurrentRound
            };

            _db.People.Add(tenant);
            await _db.SaveChangesAsync(); // Save tenant to get ID

            var now = DateTime.UtcNow;
            var contract = new RentalContract
            {
                PropertyId = prop.Id,
                TenantId = tenant.Id,
                MonthlyRent = prop.MonthlyRent,
                DurationMonths = duration,
                StartDate = now,
                EndDate = now.AddMonths(duration),
                MonthsRemaining = duration,
                LastRentPayment = now,
                IsActive = true,
                ChanceToLeave = 0.1 + new Random().NextDouble() * 0.1
            };

            prop.Status = EstateStatus.Rented;
            prop.CurrentTenantId = tenant.Id;

            _db.RentalContracts.Add(contract);
            await _db.SaveChangesAsync();
            _logger.LogInformation("Property rented out to {TenantName} for {Duration} months", tenantName, duration);
            return RedirectToPage();
        }
    }
}
