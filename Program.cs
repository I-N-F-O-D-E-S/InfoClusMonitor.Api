using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using InfoClusMonitor.Api.Data;
using InfoClusMonitor.Api.Hubs;
using InfoClusMonitor.Api.Models;
using InfoClusMonitor.Api.Services;
using InfoClusMonitor.Api.Services.Auth;

var builder = WebApplication.CreateBuilder(args);

// Controllers & API Explorer
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();

// Swagger / OpenAPI with JWT Bearer configuration
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "InfoClusMonitor API",
        Version = "v1",
        Description = "API de administración de máquinas Linux con RabbitMQ, SignalR y Autenticación JWT"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Ingrese el token JWT Bearer en el campo de texto.\r\nEjemplo: \"eyJhbGciOi...\""
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// SignalR & MediatR
builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

// Auth & Token Service
builder.Services.AddScoped<TokenService>();

var securityKey = builder.Configuration["Auth:SecurityKey"]
    ?? throw new InvalidOperationException("Auth:SecurityKey no está configurado en appsettings.json.");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(securityKey)),
        ValidateIssuer = false,
        ValidateAudience = false,
        ClockSkew = TimeSpan.Zero
    };

    // Soporte para autenticación en SignalR vía query string "access_token"
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/machines"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

// Database
builder.Services.AddDbContext<AppDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrEmpty(connectionString))
    {
        options.UseInMemoryDatabase("InfoClusMonitor");
    }
    else
    {
        var serverVersion = ServerVersion.AutoDetect(connectionString);
        options.UseMySql(connectionString, serverVersion);
    }
});

// RabbitMQ, Minio & File services
builder.Services.AddSingleton<IMinioService, MinioService>();
builder.Services.AddSingleton<IFileBrowseManager, FileBrowseManager>();
builder.Services.AddSingleton<IRabbitMqService, RabbitMqService>();
builder.Services.AddHostedService<AgentMessageProcessor>();
builder.Services.AddHostedService<ScheduledTaskSchedulerService>();

// CORS
var corsOrigins = builder.Configuration["Cors:AllowedOrigins"]?
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (corsOrigins != null && corsOrigins.Length > 0)
        {
            policy.WithOrigins(corsOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        }
        else
        {
            policy.SetIsOriginAllowed(_ => true)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        }
    });
});

var app = builder.Build();

// Swagger UI middleware
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "InfoClusMonitor API v1");
    c.RoutePrefix = "swagger";
});

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<MachineHub>("/hubs/machines");

// Inicialización y seed de base de datos
await DbInitializer.SeedAsync(app.Services);

// Sincronización automática de releases de agente (agent.py + install.sh) a MinIO
using (var scope = app.Services.CreateScope())
{
    try
    {
        var minio = scope.ServiceProvider.GetRequiredService<IMinioService>();
        var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<InfoClusMonitor.Api.Features.Agents.UpdateAgentHandler>>();
        var releaseHandler = new InfoClusMonitor.Api.Features.Agents.UpdateAgentHandler(null!, minio, null!, env, logger);
        await releaseHandler.EnsureReleasePackageInMinioAsync();
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "No se pudo sincronizar automáticamente el paquete de release a MinIO en el arranque: {Message}", ex.Message);
    }
}

app.Run();
