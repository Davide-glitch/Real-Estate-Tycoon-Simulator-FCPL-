using DavidEstateArchitect.Data;
using DavidEstateArchitect.Models;
using Microsoft.EntityFrameworkCore;

namespace DavidEstateArchitect.Services
{
    public class GameEngineService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<GameEngineService> _logger;
        private static readonly Random _rand = new Random();
        private const int ROUNDS_PER_TAX_CYCLE = 12; // Pay taxes every 12 rounds (quarterly)
        private const int ROUNDS_PER_MAINTENANCE = 6; // Pay maintenance every 6 rounds

        public GameEngineService(IServiceScopeFactory scopeFactory, ILogger<GameEngineService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    var state = await db.GameStates.FirstOrDefaultAsync(stoppingToken);
                    if (state == null)
                    {
                        state = new GameState();
                        db.GameStates.Add(state);
                        await db.SaveChangesAsync(stoppingToken);
                    }

                    // Only run the round if the game is not paused
                    if (!state.IsPaused)
                    {
                        await RunRoundAsync(db, state, stoppingToken);
                    }
                    else
                    {
                        _logger.LogInformation("Game is paused. Round {Round} skipped.", state.CurrentRound);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in game engine round");
                }

                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }

        private async Task RunRoundAsync(AppDbContext db, GameState state, CancellationToken ct)
        {
            _logger.LogInformation("Starting round {Round}", state.CurrentRound);

            // Get or create statistics
            var stats = await db.PlayerStatistics.FirstOrDefaultAsync(ct);
            if (stats == null)
            {
                stats = new PlayerStatistics();
                db.PlayerStatistics.Add(stats);
                await db.SaveChangesAsync(ct);
            }

            // Aggressively clean up old and unsold listings - remove properties older than 100 seconds
            var cutoffTime = DateTime.UtcNow.AddSeconds(-100);
            var oldListings = await db.Properties
                .Where(p => p.Status == EstateStatus.ForSale &&
                           !p.IsOwnedByPlayer &&
                           p.ListedOn < cutoffTime)
                .ToListAsync(ct);
            if (oldListings.Any())
            {
                db.Properties.RemoveRange(oldListings);
                _logger.LogInformation("Removed {Count} old listings from market", oldListings.Count);
                await db.SaveChangesAsync(ct);
            }

            // Keep market size reasonable - if too many listings exist, skip property generation
            var currentForSaleCount = await db.Properties
                .CountAsync(p => p.Status == EstateStatus.ForSale, ct);

            // Process market events
            await ProcessMarketEventsAsync(db, state, ct);

            // Generate random events (10% chance per round)
            if (_rand.NextDouble() < 0.1)
            {
                await GenerateRandomEventAsync(db, state, ct);
            }

            // Only generate new properties if market isn't oversaturated (max 20 listings max!)
            if (currentForSaleCount < 20)
            {
                // Generate only 1 property per round
                var p = await GeneratePropertyWithMarketInfluence(db, state, ct);
                db.Properties.Add(p);
            }
            else
            {
                _logger.LogInformation("Market has {Count} listings. Skipping new property generation.", currentForSaleCount);
            }

            // Ensure at least one affordable listing each round (especially at game start)
            var affordableThreshold = Math.Max(10000m, state.Balance);
            var hasAffordable = await db.Properties.AnyAsync(
                p => p.Status == EstateStatus.ForSale && p.Price <= affordableThreshold, ct);
            if (!hasAffordable && currentForSaleCount < 20)
            {
                db.Properties.Add(GenerateStarterProperty(affordableThreshold));
                _logger.LogInformation("Injected affordable starter property (<= ${Price:N0})", affordableThreshold);
            }

            // Generate 1-2 people per round (reduced from 3)
            int peopleToGenerate = _rand.Next(1, 3);
            for (int i = 0; i < peopleToGenerate; i++)
            {
                var person = GeneratePerson(state.CurrentRound);
                db.People.Add(person);
            }

            // Clean up inactive people (older than 5 rounds)
            var inactivePeople = await db.People
                .Where(p => !p.IsActive && p.RoundNumber < state.CurrentRound - 5)
                .ToListAsync(ct);
            if (inactivePeople.Any())
            {
                db.People.RemoveRange(inactivePeople);
            }            // Process active rental contracts monthly progression
            var activeContracts = await db.RentalContracts
                .Include(rc => rc.Property)
                .Include(rc => rc.Tenant)
                .Where(rc => rc.IsActive)
                .ToListAsync(ct);

            foreach (var rc in activeContracts)
            {
                // Pay rent once per round as a simplification for demo
                state.Balance += rc.MonthlyRent;
                stats.TotalRentalIncome += rc.MonthlyRent;

                db.PropertyTransactions.Add(new PropertyTransaction
                {
                    PropertyId = rc.PropertyId,
                    Type = TransactionType.RentalIncome,
                    Amount = rc.MonthlyRent,
                    Details = $"Rent payment from {rc.Tenant?.Name ?? "tenant"}",
                    RoundNumber = state.CurrentRound
                });

                rc.TotalMonthsStayed += 1;
                rc.MonthsRemaining = Math.Max(0, rc.MonthsRemaining - 1);

                // Early leave chance, reduced by renovation level
                var adjustedLeaveChance = Math.Max(0.0, rc.ChanceToLeave - (rc.Property?.RenovationLevel ?? 0) * 0.05);
                if (_rand.NextDouble() < adjustedLeaveChance)
                {
                    // Tenant leaves early
                    rc.IsActive = false;
                    if (rc.Property != null)
                    {
                        rc.Property.Status = EstateStatus.Owned;
                        rc.Property.CurrentTenantId = null;
                    }
                    _logger.LogInformation("Tenant left early from property #{Id}", rc.PropertyId);
                }
                else if (rc.MonthsRemaining <= 0)
                {
                    // Check if tenant wants to buy (after 2+ years)
                    if (rc.TotalMonthsStayed >= 24)
                    {
                        var buyChance = 0.1 + ((rc.TotalMonthsStayed / 12 - 2) * 0.1); // +10% per year after 2
                        if (_rand.NextDouble() < buyChance && rc.Property != null)
                        {
                            // Tenant offers to buy at premium
                            var buyOffer = (rc.Property.MarketValue > 0 ? rc.Property.MarketValue : rc.Property.Price) * 1.15m;
                            _logger.LogInformation("Long-term tenant offers to buy property #{Id} for ${Offer:N0}!",
                                rc.PropertyId, buyOffer);

                            // Auto-accept if good deal
                            if (buyOffer >= rc.Property.Price * 1.1m)
                            {
                                state.Balance += buyOffer;
                                stats.TotalMoneyEarned += buyOffer;
                                stats.TotalPropertiesSold++;
                                rc.Property.IsOwnedByPlayer = false;
                                rc.Property.Status = EstateStatus.ForSale;
                                rc.IsActive = false;

                                db.PropertyTransactions.Add(new PropertyTransaction
                                {
                                    PropertyId = rc.PropertyId,
                                    Type = TransactionType.Sale,
                                    Amount = buyOffer,
                                    Details = $"Sold to long-term tenant at premium",
                                    RoundNumber = state.CurrentRound
                                });

                                _logger.LogInformation("Auto-sold to tenant for ${Amount:N0}", buyOffer);
                                continue;
                            }
                        }
                    }

                    // Contract end: simple renewal rule based on safety and rent
                    var renewChance = 0.2; // base 20%
                    if (rc.Property?.Safety == SafetyLevel.Safe) renewChance += 0.3;
                    if (rc.Property?.Safety == SafetyLevel.Moderate) renewChance += 0.15;
                    renewChance += (rc.Property?.RenovationLevel ?? 0) * 0.05;
                    if (rc.TotalMonthsStayed >= 24) renewChance += 0.1; // after 2 years

                    if (_rand.NextDouble() < renewChance)
                    {
                        rc.MonthsRemaining = rc.DurationMonths;
                        rc.EndDate = DateTime.UtcNow.AddMonths(rc.DurationMonths);
                        _logger.LogInformation("Tenant renewed lease for property #{Id}", rc.PropertyId);
                    }
                    else
                    {
                        rc.IsActive = false;
                        if (rc.Property != null)
                        {
                            rc.Property.Status = EstateStatus.Owned;
                            rc.Property.CurrentTenantId = null;
                        }
                        _logger.LogInformation("Tenant moved out from property #{Id}", rc.PropertyId);
                    }
                }
            }

            // Process taxes and maintenance
            await ProcessTaxesAndMaintenanceAsync(db, state, stats, ct);

            // Process loan payments
            await ProcessLoanPaymentsAsync(db, state, ct);

            // Update property values
            await UpdatePropertyValuesAsync(db, state, ct);

            // Calculate net worth
            await UpdateNetWorthAsync(db, state, stats, ct);

            // Check victory/defeat conditions
            CheckGameEndConditions(state, stats);

            // Match buyers and tenants with properties
            var properties = await db.Properties.Where(p => p.Status == EstateStatus.ForSale).ToListAsync(ct);
            var people = await db.People.Where(p => p.IsActive).ToListAsync(ct);

            var matches = MatchBuyersAndTenants(db, properties, people);
            _logger.LogInformation("Matched {Count} buyers/tenants with properties.", matches.Count);

            // Increment round
            state.CurrentRound++;
            await db.SaveChangesAsync(ct);

            await db.SaveChangesAsync(ct);
            _logger.LogInformation("Round {Round} completed. Balance: ${Balance:N0}, Net Worth: ${NetWorth:N0}",
                state.CurrentRound, state.Balance, state.TotalNetWorth);
        }

        private void CheckGameEndConditions(GameState state, PlayerStatistics stats)
        {
            // Victory condition: $1,000,000 net worth
            if (!state.HasWon && state.TotalNetWorth >= 1000000m)
            {
                state.HasWon = true;
                state.GameEndTime = DateTime.UtcNow;
                state.VictoryMessage = $"🎉 Congratulations! You became a millionaire in {state.CurrentRound} rounds!";
                state.IsPaused = true; // Auto-pause on victory
                _logger.LogInformation("🏆 VICTORY! Player reached $1,000,000 net worth!");
            }

            // Defeat condition: Negative balance for 3 rounds OR net worth below -$50,000
            if (!state.HasLost && (state.TotalNetWorth < -50000m || (state.Balance < 0 && state.CurrentRound > 10)))
            {
                var ownedPropertiesCount = stats.TotalPropertiesBought - stats.TotalPropertiesSold;
                if (ownedPropertiesCount == 0 && state.Balance < -10000m)
                {
                    state.HasLost = true;
                    state.GameEndTime = DateTime.UtcNow;
                    state.VictoryMessage = $"💔 Bankruptcy! Game Over at round {state.CurrentRound}.";
                    state.IsPaused = true; // Auto-pause on defeat
                    _logger.LogWarning("⚠️ BANKRUPTCY! Player has negative balance with no assets.");
                }
            }
        }

        private static Property GenerateProperty()
        {
            var county = GameConstants.Counties[_rand.Next(GameConstants.Counties.Length)];
            var type = (EstateType)_rand.Next(Enum.GetValues(typeof(EstateType)).Length);
            var location = (LocationType)_rand.Next(Enum.GetValues(typeof(LocationType)).Length);
            var safety = (SafetyLevel)_rand.Next(Enum.GetValues(typeof(SafetyLevel)).Length);

            decimal basePrice = type switch
            {
                EstateType.Apartment => 50000m,
                EstateType.House => 150000m,
                EstateType.Mansion => 500000m,
                _ => 100000m
            };

            decimal locationMultiplier = location switch
            {
                LocationType.Center => 1.5m,
                LocationType.AroundCity => 1.1m,
                LocationType.OutsideCity => 0.8m,
                _ => 1m
            };

            decimal safetyMultiplier = safety switch
            {
                SafetyLevel.Safe => 1.3m,
                SafetyLevel.Moderate => 1.0m,
                SafetyLevel.Risky => 0.7m,
                _ => 1m
            };

            var price = basePrice * locationMultiplier * safetyMultiplier * (decimal)(1.0 + _rand.NextDouble() * 0.3);

            return new Property
            {
                Address = $"{_rand.Next(1, 9999)} {_streetNames[_rand.Next(_streetNames.Length)]}",
                City = _cities[_rand.Next(_cities.Length)],
                Price = Math.Round(price, 0),
                Bedrooms = _rand.Next(1, 6),
                Bathrooms = _rand.Next(1, 4),
                SquareFeet = _rand.Next(400, 5000),
                County = county,
                Type = type,
                Location = location,
                Safety = safety,
                Status = EstateStatus.ForSale,
                MonthlyRent = Math.Round(price * 0.008m, 0),
                IsOwnedByPlayer = false
            };
        }

        private static Person GeneratePerson(int round)
        {
            var county = GameConstants.Counties[_rand.Next(GameConstants.Counties.Length)];
            var type = (EstateType)_rand.Next(Enum.GetValues(typeof(EstateType)).Length);
            var safety = (SafetyLevel)_rand.Next(Enum.GetValues(typeof(SafetyLevel)).Length);
            var city = _cities[_rand.Next(_cities.Length)];
            var isBuyer = _rand.NextDouble() < 0.5;

            // Offers depend on type
            decimal baseOffer = type switch
            {
                EstateType.Apartment => 60000m,
                EstateType.House => 200000m,
                EstateType.Mansion => 600000m,
                _ => 100000m
            };

            var offer = baseOffer * (decimal)(0.9 + _rand.NextDouble() * 0.4);

            var hasAlt = _rand.NextDouble() < 0.5;
            SafetyLevel? altSafety = hasAlt ? (SafetyLevel?)((SafetyLevel)_rand.Next(Enum.GetValues(typeof(SafetyLevel)).Length)) : null;
            var altAmount = hasAlt ? offer * (decimal)(0.9 + _rand.NextDouble() * 0.3) : 0m;

            return new Person
            {
                Name = _names[_rand.Next(_names.Length)],
                Type = isBuyer ? PersonType.Buyer : PersonType.Tenant,
                DesiredCounty = county,
                DesiredType = type,
                DesiredSafety = safety,
                OfferAmount = Math.Round(offer, 0),
                HasAlternative = hasAlt,
                AlternativeSafety = altSafety,
                AlternativeAmount = Math.Round(altAmount, 0),
                RoundNumber = round
            };
        }

        private static Property GenerateStarterProperty(decimal maxPrice)
        {
            // Cheap apartment with modest attributes, price between 3,000 and min(9,000, maxPrice)
            var upper = (decimal)Math.Min((double)maxPrice, 9000d);
            if (upper < 3000m) upper = maxPrice; // fallback
            var price = (decimal)(3000 + _rand.Next(0, (int)(upper - 3000m + 1)));

            return new Property
            {
                Address = $"{_rand.Next(1, 9999)} {_streetNames[_rand.Next(_streetNames.Length)]}",
                City = _cities[_rand.Next(_cities.Length)],
                County = GameConstants.Counties[_rand.Next(GameConstants.Counties.Length)],
                Type = EstateType.Apartment,
                Location = LocationType.OutsideCity,
                Safety = _rand.NextDouble() < 0.6 ? SafetyLevel.Risky : SafetyLevel.Moderate,
                Price = Math.Round(price, 0),
                MarketValue = Math.Round(price, 0),
                Status = EstateStatus.ForSale,
                Bedrooms = 1,
                Bathrooms = 1,
                SquareFeet = _rand.Next(400, 800),
                MonthlyRent = Math.Round(price * 0.01m, 0),
                MonthlyMaintenanceCost = Math.Round(price * 0.004m, 0),
                IsOwnedByPlayer = false
            };
        }

        private static readonly string[] _names = new[]
        {
            "Alex", "Sam", "Jordan", "Taylor", "Casey", "Riley", "Morgan", "Jamie", "Avery", "Cameron"
        };

        private static readonly string[] _streetNames = new[]
        {
            "Maple St", "Oak Ave", "Pine Rd", "Cedar Ln", "Elm St", "Birch Way", "Willow Dr", "Poplar Ct"
        };

        private static readonly string[] _cities = new[]
        {
            "Springfield", "Riverton", "Lakeside", "Hillview", "Fairview", "Brookfield"
        };

        // ============ ENHANCED GAME MECHANICS ============

        private async Task ProcessMarketEventsAsync(AppDbContext db, GameState state, CancellationToken ct)
        {
            var activeEvents = await db.MarketEvents.Where(e => e.IsActive).ToListAsync(ct);

            foreach (var evt in activeEvents)
            {
                evt.RoundsRemaining--;
                if (evt.RoundsRemaining <= 0)
                {
                    evt.IsActive = false;
                    _logger.LogInformation("Market event '{Title}' has ended", evt.Title);
                }
            }

            await db.SaveChangesAsync(ct);
        }

        private async Task GenerateRandomEventAsync(AppDbContext db, GameState state, CancellationToken ct)
        {
            var eventType = (EventType)_rand.Next(Enum.GetValues(typeof(EventType)).Length);
            var affectedCounty = _rand.NextDouble() < 0.3 ? GameConstants.Counties[_rand.Next(GameConstants.Counties.Length)] : null;

            var evt = new MarketEvent
            {
                Type = eventType,
                AffectedCounty = affectedCounty,
                DurationRounds = _rand.Next(3, 10),
                RoundsRemaining = _rand.Next(3, 10),
                OccurredAt = DateTime.UtcNow,
                IsActive = true
            };

            switch (eventType)
            {
                case EventType.EconomicBoom:
                    evt.Title = "Economic Boom!";
                    evt.Description = affectedCounty != null
                        ? $"Strong economic growth in {affectedCounty} county boosts property values!"
                        : "National economic boom increases property values across all counties!";
                    evt.ImpactMultiplier = 1.15m;
                    break;

                case EventType.Recession:
                    evt.Title = "Economic Recession";
                    evt.Description = affectedCounty != null
                        ? $"Economic downturn in {affectedCounty} county decreases property values."
                        : "Economic recession affects property market nationwide.";
                    evt.ImpactMultiplier = 0.85m;
                    break;

                case EventType.SafetyImprovement:
                    evt.Title = "Safety Initiative";
                    evt.Description = affectedCounty != null
                        ? $"New police programs in {affectedCounty} improve neighborhood safety!"
                        : "Crime reduction programs improve safety nationwide!";
                    evt.ImpactMultiplier = 1.10m;
                    break;

                case EventType.SafetyDegradation:
                    evt.Title = "Crime Wave";
                    evt.Description = affectedCounty != null
                        ? $"Increased crime in {affectedCounty} affects property desirability."
                        : "Rising crime rates concern potential buyers.";
                    evt.ImpactMultiplier = 0.90m;
                    break;

                case EventType.TaxChange:
                    var increase = _rand.NextDouble() < 0.5;
                    evt.Title = increase ? "Tax Increase" : "Tax Relief";
                    evt.Description = affectedCounty != null
                        ? $"Property tax {(increase ? "increase" : "decrease")} announced for {affectedCounty}."
                        : $"Property tax {(increase ? "increase" : "decrease")} affects all counties.";
                    evt.ImpactMultiplier = increase ? 0.95m : 1.05m;

                    // Actually change tax rate
                    if (affectedCounty == null)
                        state.PropertyTaxRate *= (increase ? 1.1m : 0.9m);
                    break;

                case EventType.InterestRateChange:
                    var rateUp = _rand.NextDouble() < 0.5;
                    evt.Title = rateUp ? "Interest Rate Hike" : "Interest Rate Cut";
                    evt.Description = $"Central bank {(rateUp ? "increases" : "decreases")} interest rates, affecting loan costs.";
                    evt.ImpactMultiplier = 1.0m;
                    state.BaseInterestRate *= (rateUp ? 1.2m : 0.8m);
                    break;

                case EventType.NaturalDisaster:
                    evt.Title = "Natural Disaster";
                    evt.Description = affectedCounty != null
                        ? $"Natural disaster strikes {affectedCounty}! Property values plummet."
                        : "Widespread natural disaster affects multiple regions.";
                    evt.ImpactMultiplier = 0.70m;
                    evt.DurationRounds = 5;
                    evt.RoundsRemaining = 5;
                    break;

                case EventType.PriceIncrease:
                    evt.Title = "Market Rally";
                    evt.Description = affectedCounty != null
                        ? $"High demand drives up prices in {affectedCounty}!"
                        : "Hot real estate market increases prices!";
                    evt.ImpactMultiplier = 1.20m;
                    break;

                case EventType.PriceDecrease:
                    evt.Title = "Market Correction";
                    evt.Description = affectedCounty != null
                        ? $"Oversupply in {affectedCounty} leads to price drops."
                        : "Market correction brings property prices down.";
                    evt.ImpactMultiplier = 0.80m;
                    break;
            }

            db.MarketEvents.Add(evt);
            await db.SaveChangesAsync(ct);
            _logger.LogInformation("New market event: {Title} - {Description}", evt.Title, evt.Description);
        }

        private async Task<Property> GeneratePropertyWithMarketInfluence(AppDbContext db, GameState state, CancellationToken ct)
        {
            var prop = GenerateProperty();

            // Apply market event multipliers
            var activeEvents = await db.MarketEvents
                .Where(e => e.IsActive && (e.AffectedCounty == null || e.AffectedCounty == prop.County))
                .ToListAsync(ct);

            decimal totalMultiplier = 1.0m;
            foreach (var evt in activeEvents)
            {
                totalMultiplier *= evt.ImpactMultiplier;
            }

            prop.Price *= totalMultiplier;
            prop.Price = Math.Round(prop.Price, 0);
            prop.MarketValue = prop.Price;

            // Calculate maintenance cost (0.5% - 1% of property value per month)
            prop.MonthlyMaintenanceCost = Math.Round(prop.Price * (decimal)(0.005 + _rand.NextDouble() * 0.005), 0);

            return prop;
        }

        private async Task ProcessTaxesAndMaintenanceAsync(AppDbContext db, GameState state, PlayerStatistics stats, CancellationToken ct)
        {
            var ownedProperties = await db.Properties.Where(p => p.IsOwnedByPlayer).ToListAsync(ct);

            // Property taxes (quarterly - every 12 rounds)
            if (state.CurrentRound - state.LastTaxRound >= ROUNDS_PER_TAX_CYCLE && ownedProperties.Any())
            {
                decimal totalTax = 0;
                foreach (var prop in ownedProperties)
                {
                    var quarterlyTax = (prop.MarketValue > 0 ? prop.MarketValue : prop.Price) * state.PropertyTaxRate / 4;
                    totalTax += quarterlyTax;
                }

                if (state.Balance >= totalTax)
                {
                    state.Balance -= totalTax;
                    stats.TotalTaxesPaid += totalTax;
                    state.LastTaxRound = state.CurrentRound;

                    db.PropertyTransactions.Add(new PropertyTransaction
                    {
                        PropertyId = 0,
                        Type = TransactionType.Tax,
                        Amount = -totalTax,
                        Details = $"Quarterly property tax on {ownedProperties.Count} properties",
                        RoundNumber = state.CurrentRound
                    });

                    _logger.LogInformation("Property taxes paid: ${Tax:N0}", totalTax);
                }
                else
                {
                    _logger.LogWarning("Insufficient funds for property tax! Tax skipped.");
                }
            }

            // Maintenance costs (every 6 rounds)
            if (state.CurrentRound - state.LastMaintenanceRound >= ROUNDS_PER_MAINTENANCE && ownedProperties.Any())
            {
                decimal totalMaintenance = 0;
                foreach (var prop in ownedProperties)
                {
                    totalMaintenance += prop.MonthlyMaintenanceCost * ROUNDS_PER_MAINTENANCE;
                }

                if (state.Balance >= totalMaintenance)
                {
                    state.Balance -= totalMaintenance;
                    stats.TotalMaintenancePaid += totalMaintenance;
                    state.LastMaintenanceRound = state.CurrentRound;

                    db.PropertyTransactions.Add(new PropertyTransaction
                    {
                        PropertyId = 0,
                        Type = TransactionType.Maintenance,
                        Amount = -totalMaintenance,
                        Details = $"Maintenance for {ownedProperties.Count} properties",
                        RoundNumber = state.CurrentRound
                    });

                    _logger.LogInformation("Maintenance costs paid: ${Cost:N0}", totalMaintenance);
                }
                else
                {
                    _logger.LogWarning("Insufficient funds for maintenance! Properties may deteriorate.");
                    // Reduce safety level of properties if maintenance not paid
                    foreach (var prop in ownedProperties.Take(2))
                    {
                        if (prop.Safety == SafetyLevel.Safe)
                            prop.Safety = SafetyLevel.Moderate;
                        else if (prop.Safety == SafetyLevel.Moderate)
                            prop.Safety = SafetyLevel.Risky;
                    }
                }
            }
        }

        private async Task ProcessLoanPaymentsAsync(AppDbContext db, GameState state, CancellationToken ct)
        {
            var activeLoans = await db.Loans.Where(l => l.IsActive).ToListAsync(ct);

            foreach (var loan in activeLoans)
            {
                if (state.Balance >= loan.MonthlyPayment)
                {
                    state.Balance -= loan.MonthlyPayment;
                    loan.TotalPaid += loan.MonthlyPayment;
                    loan.MonthsRemaining--;

                    if (loan.MonthsRemaining <= 0)
                    {
                        loan.IsActive = false;
                        _logger.LogInformation("Loan paid off! Total paid: ${Total:N0}", loan.TotalPaid);
                    }
                }
                else
                {
                    _logger.LogWarning("Missed loan payment! Penalties may apply.");
                    // Add penalty
                    loan.Principal *= 1.05m; // 5% penalty
                }
            }
        }

        private async Task UpdatePropertyValuesAsync(AppDbContext db, GameState state, CancellationToken ct)
        {
            var ownedProperties = await db.Properties.Where(p => p.IsOwnedByPlayer).ToListAsync(ct);

            foreach (var prop in ownedProperties)
            {
                // Annual appreciation (simplified to per round)
                var appreciationRate = prop.Type switch
                {
                    EstateType.Apartment => 0.002m, // 0.2% per round
                    EstateType.House => 0.003m,
                    EstateType.Mansion => 0.004m,
                    _ => 0.002m
                };

                // Safety affects appreciation
                appreciationRate *= prop.Safety switch
                {
                    SafetyLevel.Safe => 1.2m,
                    SafetyLevel.Moderate => 1.0m,
                    SafetyLevel.Risky => 0.8m,
                    _ => 1.0m
                };

                var oldValue = prop.MarketValue > 0 ? prop.MarketValue : prop.Price;
                prop.MarketValue = oldValue * (1 + appreciationRate);
                prop.TotalAppreciation += (prop.MarketValue - oldValue);
                prop.YearsOwned = (int)((DateTime.UtcNow - prop.PurchaseDate).TotalDays / 365);
            }
        }

        private async Task UpdateNetWorthAsync(AppDbContext db, GameState state, PlayerStatistics stats, CancellationToken ct)
        {
            var ownedProperties = await db.Properties.Where(p => p.IsOwnedByPlayer).ToListAsync(ct);
            var activeLoans = await db.Loans.Where(l => l.IsActive).ToListAsync(ct);

            decimal propertyValue = ownedProperties.Sum(p => p.MarketValue > 0 ? p.MarketValue : p.Price);
            decimal loanDebt = activeLoans.Sum(l => l.Principal);

            state.TotalNetWorth = state.Balance + propertyValue - loanDebt;
            stats.NetWorth = state.TotalNetWorth;

            if (state.TotalNetWorth > stats.HighestBalance)
                stats.HighestBalance = state.TotalNetWorth;
        }

        private List<Person> MatchBuyersAndTenants(AppDbContext db, List<Property> properties, List<Person> people)
        {
            var matches = new List<Person>();

            foreach (var person in people)
            {
                if (person.Type != PersonType.Buyer)
                {
                    continue;
                }

                var potentialMatches = properties.Where(p =>
                    p.County == person.DesiredCounty &&
                    p.Type == person.DesiredType &&
                    p.Safety == person.DesiredSafety &&
                    p.Status == EstateStatus.ForSale &&
                    p.Price <= person.OfferAmount).ToList();

                if (potentialMatches.Any())
                {
                    var selectedProperty = potentialMatches.First(); // Select the first match (can be randomized)
                    matches.Add(person);

                    // Simulate NPC purchase so listings rotate without gifting assets to the player
                    properties.Remove(selectedProperty);
                    db.Properties.Remove(selectedProperty);

                    person.IsActive = false; // Mark person as inactive after matching
                    db.People.Update(person);
                }
            }

            return matches;
        }
    }
}
