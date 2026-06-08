using Microsoft.EntityFrameworkCore;
using Termometriya.Server.Models;

namespace Termometriya.Server.Data;

public static class SeedData
{
    public static async Task Initialize(AppDbContext db)
    {
        await db.Database.EnsureCreatedAsync();

        if (await db.Cultures.AnyAsync()) return;

        var cultures = new List<Culture>
        {
            new() { Name = "Пшеница", NormTemp = 25, WarnTemp = 30, CriticalTemp = 35, GradientWarn = 1.0, GradientCritical = 2.0, DeviationThreshold = 3.0, HighTempThreshold = 30, HighTempGradient = 5.0, CriticalHighTemp50 = 50 },
            new() { Name = "Кукуруза", NormTemp = 25, WarnTemp = 30, CriticalTemp = 35, GradientWarn = 1.0, GradientCritical = 2.0, DeviationThreshold = 3.0, HighTempThreshold = 30, HighTempGradient = 5.0, CriticalHighTemp50 = 50 },
            new() { Name = "Ячмень", NormTemp = 25, WarnTemp = 30, CriticalTemp = 35, GradientWarn = 1.0, GradientCritical = 2.0, DeviationThreshold = 3.0, HighTempThreshold = 30, HighTempGradient = 5.0, CriticalHighTemp50 = 50 },
            new() { Name = "Подсолнечник", NormTemp = 20, WarnTemp = 25, CriticalTemp = 30, GradientWarn = 0.8, GradientCritical = 1.5, DeviationThreshold = 2.0, HighTempThreshold = 28, HighTempGradient = 4.0, CriticalHighTemp50 = 45 },
            new() { Name = "Гречиха", NormTemp = 22, WarnTemp = 28, CriticalTemp = 34, GradientWarn = 1.0, GradientCritical = 2.0, DeviationThreshold = 3.0, HighTempThreshold = 28, HighTempGradient = 5.0, CriticalHighTemp50 = 48 },
            new() { Name = "Овёс", NormTemp = 25, WarnTemp = 32, CriticalTemp = 38, GradientWarn = 1.2, GradientCritical = 2.5, DeviationThreshold = 3.5, HighTempThreshold = 32, HighTempGradient = 5.0, CriticalHighTemp50 = 50 },
        };
        db.Cultures.AddRange(cultures);
        await db.SaveChangesAsync();

        var lines = new List<ElevatorLine>
        {
            new() { Name = "Линия 1", DisplayOrder = 1 },
            new() { Name = "Линия 2", DisplayOrder = 2 },
            new() { Name = "Линия 3", DisplayOrder = 3 },
        };
        db.ElevatorLines.AddRange(lines);
        await db.SaveChangesAsync();

        var silos = new List<Silo>();
        int siloId = 1;
        foreach (var line in lines)
        {
            for (int n = 1; n <= 4; n++)
            {
                silos.Add(new Silo
                {
                    Id = siloId,
                    LineId = line.Id,
                    Number = n,
                    CultureId = cultures[(siloId - 1) % cultures.Count].Id,
                    FillLevel = 75 + (siloId * 3) % 20,
                    Capacity = 1000
                });
                siloId++;
            }
        }
        db.Silos.AddRange(silos);
        await db.SaveChangesAsync();

        var pendants = new List<Thermopendant>();
        foreach (var silo in silos)
        {
            int basePos = (silo.Number % 2 == 1) ? 0 : 6;
            for (int p = 0; p < 6; p++)
            {
                bool isCentral = (p == 0);
                pendants.Add(new Thermopendant
                {
                    SiloId = silo.Id,
                    PositionIndex = basePos + p,
                    DisplayOrder = basePos + p,
                    PointCount = isCentral ? 18 : 16,
                    IsActive = true,
                    IsCentral = isCentral
                });
            }
        }
        db.Thermopendants.AddRange(pendants);
        await db.SaveChangesAsync();
    }
}
