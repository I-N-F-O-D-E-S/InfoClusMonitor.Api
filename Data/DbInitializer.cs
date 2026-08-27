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

        const int maxRetries = 10;
        var retryDelay = TimeSpan.FromSeconds(3);

        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                logger.LogInformation("Verificando y aplicando migraciones de base de datos (intento {Attempt}/{MaxRetries})...", attempt, maxRetries);

                if (db.Database.IsRelational())
                {
                    var pendingMigrations = (await db.Database.GetPendingMigrationsAsync()).ToList();
                    if (pendingMigrations.Count > 0)
                    {
                        logger.LogInformation("Se encontraron {Count} migraciones pendientes: {Migrations}. Aplicando...",
                            pendingMigrations.Count, string.Join(", ", pendingMigrations));
                        await db.Database.MigrateAsync();
                        logger.LogInformation("Migraciones aplicadas exitosamente a la base de datos.");
                    }
                    else
                    {
                        logger.LogInformation("La base de datos ya está al día. No hay migraciones pendientes.");
                    }
                }
                else
                {
                    await db.Database.EnsureCreatedAsync();
                    logger.LogInformation("Base de datos en memoria inicializada.");
                }

                // Sembrar usuario admin si no existe
                if (!await db.Users.AnyAsync())
                {
                    logger.LogInformation("Sembrando usuario administrador por defecto...");
                    var defaultAdmin = new User
                    {
                        UserId = Guid.NewGuid().ToString("N"),
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

                // Éxito: salir del bucle de reintentos
                return;
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                logger.LogWarning(ex, "No se pudo conectar o migrar la base de datos en el intento {Attempt}/{MaxRetries}. Reintentando en {Delay}s...",
                    attempt, maxRetries, retryDelay.TotalSeconds);
                await Task.Delay(retryDelay);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error crítico al inicializar y migrar la base de datos tras {MaxRetries} intentos.", maxRetries);
                throw;
            }
        }
    }
}
