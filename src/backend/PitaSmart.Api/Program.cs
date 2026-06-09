#nullable enable
using System.Text;
using Fido2NetLib;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using PitaSmart.Api.Middleware;
using PitaSmart.Application;
using PitaSmart.Infrastructure;
using PitaSmart.Infrastructure.Realtime;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Serilog — configurado desde appsettings.json
// ---------------------------------------------------------------------------
builder.Host.UseSerilog((context, loggerConfig) =>
    loggerConfig.ReadFrom.Configuration(context.Configuration));

// ---------------------------------------------------------------------------
// Capas Application e Infrastructure (DI)
// ---------------------------------------------------------------------------
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// ---------------------------------------------------------------------------
// Autenticacion JWT Bearer
// ---------------------------------------------------------------------------
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection["Key"]
    ?? throw new InvalidOperationException("Jwt:Key no esta configurada en appsettings.");

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        // Permitir token JWT en la query string para conexiones SignalR.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ---------------------------------------------------------------------------
// WebAuthn / Fido2
// ---------------------------------------------------------------------------
var webAuthnSection = builder.Configuration.GetSection("WebAuthn");
builder.Services.AddFido2(options =>
{
    options.ServerDomain = webAuthnSection["RpId"] ?? "pitasmart.ec";
    options.ServerName = webAuthnSection["RpName"] ?? "PitaSmart";
    options.Origins = webAuthnSection.GetSection("Origins").Get<HashSet<string>>()
        ?? ["https://pitasmart.ec"];
});

// ---------------------------------------------------------------------------
// CORS — origenes configurables para Angular PWA
// ---------------------------------------------------------------------------
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? ["http://localhost:4200"];

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials(); // Requerido para SignalR
    });
});

// ---------------------------------------------------------------------------
// Controllers + SignalR + Swagger
// ---------------------------------------------------------------------------
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition =
            System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddSignalR();
builder.Services.AddHttpContextAccessor();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "PitaSmart API",
        Version = "v1",
        Description = "API para trazabilidad Agrocalidad y gestion de rentabilidad agricola."
    });

    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Ingrese el token JWT. Ejemplo: eyJhbGciOiJIUzI1NiIs..."
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    // Incluir comentarios XML de todos los proyectos.
    var xmlFiles = Directory.GetFiles(AppContext.BaseDirectory, "PitaSmart.*.xml", SearchOption.TopDirectoryOnly);
    foreach (var xmlFile in xmlFiles)
    {
        options.IncludeXmlComments(xmlFile, includeControllerXmlComments: true);
    }
});

// ---------------------------------------------------------------------------
// Health Checks
// ---------------------------------------------------------------------------
builder.Services.AddHealthChecks()
    .AddSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection no configurada."),
        name: "sqlserver",
        timeout: TimeSpan.FromSeconds(5),
        tags: ["db", "ready"])
    .AddSignalRHub(
        $"{(builder.Environment.IsDevelopment() ? "https://localhost:5001" : "https://api.pitasmart.ec")}/hubs/precios-mercado",
        name: "signalr-hub",
        tags: ["signalr", "ready"]);

// ---------------------------------------------------------------------------
// Build App
// ---------------------------------------------------------------------------
var app = builder.Build();

// ---------------------------------------------------------------------------
// Middleware Pipeline (orden importa)
// ---------------------------------------------------------------------------

// 1. Exception handler global — primero para capturar todo.
app.UseMiddleware<GlobalExceptionMiddleware>();

// 2. Correlation ID — propagacion de trazabilidad.
app.UseMiddleware<CorrelationIdMiddleware>();

// 3. Serilog request logging.
app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
        diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
    };
});

// 4. Swagger solo en desarrollo.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "PitaSmart API v1");
        options.RoutePrefix = "swagger";
    });
}

// 5. HTTPS redirection (solo en produccion; en dev HTTP local no necesita redireccion).
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// 6. CORS antes de auth.
app.UseCors();

// 7. Authentication & Authorization.
app.UseAuthentication();
app.UseAuthorization();

// 8. Endpoints.
app.MapControllers();
app.MapHub<PreciosMercadoHub>("/hubs/precios-mercado");
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                duration = e.Value.Duration.TotalMilliseconds
            }),
            totalDuration = report.TotalDuration.TotalMilliseconds
        });
        await context.Response.WriteAsync(result);
    }
});

// ---------------------------------------------------------------------------
// Run
// ---------------------------------------------------------------------------
Log.Information("PitaSmart API iniciando en {Environment}", app.Environment.EnvironmentName);

try
{
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "PitaSmart API termino inesperadamente.");
}
finally
{
    Log.CloseAndFlush();
}
