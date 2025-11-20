# Real Estate Tycoon Simulator (FCPL)

Use ./start.bat to run the project.
A real-estate management and tycoon simulator built in C#/.NET. You start with a small balance and grow your net worth by buying, renting, and selling properties while managing taxes, maintenance, loans, and market events.

## Tech Stack

- C# / .NET (ASP.NET Core + BackgroundService)
- Entity Framework Core with a local database (`estate.db`)
- Razor Pages / MVC-style structure (Pages, Views, Controllers)

## Main Features

- Dynamic property generation with type, location, safety, and price.
- Rental system with tenants, contracts, monthly rent, and chance-based renewal/early leave.
- Loans with interest and penalties when you miss payments.
- Property taxes, maintenance costs, and safety degradation if you skip maintenance.
- Global and regional market events (booms, recessions, disasters, tax/interest changes) affecting prices.
- Background game loop that runs rounds automatically and tracks statistics like total net worth.

## Running the Project

1. Install the .NET SDK (version 8.0 or compatible).
2. From the `DavidEstateArchitect` folder, run:

   ```bash
   dotnet run
   ```

   Or use the provided `start.bat` script to build and launch on the configured HTTPS port.

3. Open the printed URL (e.g. `https://localhost:7227`) in your browser.

## Repository Structure (key items)

- `EstateArchitect.sln` / `EstateArchitect.csproj` – Solution and project files.
- `Program.cs` – App startup and configuration.
- `Data/` – EF Core `AppDbContext` and database setup.
- `Models/` – Game and domain models (properties, people, game state, contracts, loans, etc.).
- `Services/GameEngineService.cs` – Main game loop and simulation logic.
- `Pages/`, `Views/`, `Controllers/` – UI and routing.
- `wwwroot/` – Static assets.

## Notes

- The simulation is designed as a **game**, not as a perfect economic model.
- The default starting balance and victory/defeat conditions can be tuned in the models and game engine.
