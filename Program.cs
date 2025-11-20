using Microsoft.EntityFrameworkCore;
using DavidEstateArchitect.Data;
using DavidEstateArchitect.Models;
using DavidEstateArchitect.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Add Razor Pages (for simpler CRUD pages)
builder.Services.AddRazorPages();

// Configure EF Core SQLite
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Background game engine service
builder.Services.AddHostedService<GameEngineService>();

var app = builder.Build();

// Ensure database is created and seed some sample data on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var rand = new Random();

    // Create DB if not exists
    db.Database.EnsureCreated();

    // For SQL Server, rely on EnsureCreated for quick setup in dev
    // For production scenarios, prefer migrations.

    if (!db.Properties.Any())
    {
        db.Properties.AddRange(
            new Property { Address = "123 Main St", City = "Springfield", Price = 350000, Bedrooms = 3, Bathrooms = 2, SquareFeet = 1800 },
            new Property { Address = "45 Oak Ave", City = "Riverton", Price = 495000, Bedrooms = 4, Bathrooms = 3, SquareFeet = 2400 },
            new Property { Address = "9 Lakeview Dr", City = "Lakeside", Price = 799000, Bedrooms = 5, Bathrooms = 4, SquareFeet = 3200 }
        );
        db.SaveChanges();
    }

    // Ensure a game state exists
    var state = db.GameStates.FirstOrDefault();
    if (state == null)
    {
        state = new GameState
        {
            Balance = 10000m,
            CurrentRound = 1,
            LastRoundTime = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        db.GameStates.Add(state);
        db.SaveChanges();
    }

    // Guarantee at least one affordable starter property at startup (before first round)
    var affordableThreshold = Math.Max(10000m, state.Balance);
    var hasAffordable = db.Properties.Any(p => p.Status == EstateStatus.ForSale && p.Price <= affordableThreshold);
    if (!hasAffordable)
    {
        // Create a simple affordable apartment with basic attributes
        decimal upper = Math.Min(9000m, affordableThreshold);
        if (upper < 3000m) upper = affordableThreshold; // fallback
        var price = Math.Round((decimal)(3000 + rand.Next(0, (int)(upper - 3000m + 1))), 0);

        var counties = GameConstants.Counties;
        var county = counties[rand.Next(counties.Length)];
        var streetNames = new[] { "Maple St", "Oak Ave", "Pine Rd", "Cedar Ln", "Elm St", "Birch Way", "Willow Dr", "Poplar Ct" };
        var cities = new[] { "Springfield", "Riverton", "Lakeside", "Hillview", "Fairview", "Brookfield" };

        db.Properties.Add(new Property
        {
            Address = $"{rand.Next(1, 9999)} {streetNames[rand.Next(streetNames.Length)]}",
            City = cities[rand.Next(cities.Length)],
            County = county,
            Type = EstateType.Apartment,
            Location = LocationType.OutsideCity,
            Safety = rand.NextDouble() < 0.6 ? SafetyLevel.Risky : SafetyLevel.Moderate,
            Price = price,
            MarketValue = price,
            Status = EstateStatus.ForSale,
            Bedrooms = 1,
            Bathrooms = 1,
            SquareFeet = rand.Next(400, 800),
            MonthlyRent = Math.Round(price * 0.01m, 0),
            MonthlyMaintenanceCost = Math.Round(price * 0.004m, 0),
            IsOwnedByPlayer = false
        });
        db.SaveChanges();
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

// Map Razor Pages
app.MapRazorPages();

// Keep MVC route for existing HomeController views
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
