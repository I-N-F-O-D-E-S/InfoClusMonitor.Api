using Microsoft.EntityFrameworkCore;
using InfoClusMonitor.Api.Models.Entities;

namespace InfoClusMonitor.Api.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DbInitializer");
        var db = services.GetRequiredService<AppDbContext>();

        try
        {
            logger.LogInformation("Verificando y aplicando estructura de base de datos...");
            if (db.Database.IsRelational())
            {
                await db.Database.MigrateAsync();
            }
            else
            {
                await db.Database.EnsureCreatedAsync();
            }

            if (!await db.Users.AnyAsync())
            {
                logger.LogInformation("Sembrando usuario administrador por defecto...");
                var defaultAdmin = new User
                {
                    UserId = Guid.NewGuid().ToString(),
                    Username = "admin",
                    Email = "admin@infoclus.local",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("45bldGBkM9d4"),
                    Role = "Admin",
                    CreatedAt = DateTime.UtcNow
                };

                await db.Users.AddAsync(defaultAdmin);
                await db.SaveChangesAsync();
                logger.LogInformation("Usuario admin creado exitosamente (Usuario: admin, Password: admin123).");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ocurrió un error al inicializar y sembrar la base de datos.");
            throw;
        }
    }
}
