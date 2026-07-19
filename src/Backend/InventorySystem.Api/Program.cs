using System.Text;
using System.Text.Json.Serialization;
using InventorySystem.Api.Middlewares;
using InventorySystem.Application;
using InventorySystem.Infrastructure;
using InventorySystem.Infrastructure.Auth;
using InventorySystem.Infrastructure.SignalR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// --- Logging (Serilog) ---
builder.Host.UseSerilog((context, config) =>
    config.ReadFrom.Configuration(context.Configuration)
          .WriteTo.Console());

// --- Couches applicatives (composition root) ---
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// --- Présentation ---
// JsonStringEnumConverter : les enums (MovementType...) circulent en texte lisible
// ("In"/"Out"/"Transfer") plutôt qu'en nombres magiques, côté Swagger comme côté
// frontend. allowIntegerValues reste true par défaut : la lecture accepte aussi les
// valeurs numériques, donc rien d'existant ne casse.
builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// --- CORS restreint aux origines connues (React dev/prod) ---
const string CorsPolicy = "AllowFrontend";
builder.Services.AddCors(options =>
    options.AddPolicy(CorsPolicy, policy =>
        policy.WithOrigins(
                builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                    ?? new[] { "http://localhost:5173" })
              .AllowAnyHeader()
              .AllowAnyMethod()));

// --- Authentification JWT (voir plan §6) ---
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Résolu manuellement (et non via IOptions<JwtOptions> ici) : ce délégué est appelé
        // par le framework avant que le conteneur DI complet ne soit disponible.
        var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("Section de configuration 'Jwt' manquante.");

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };

        // SignalR (WebSocket) ne peut pas poser d'en-tête Authorization sur la requête
        // d'upgrade depuis un navigateur : le client JS passe le JWT en query string
        // ("access_token"), qu'on relit ici uniquement pour les requêtes visant le hub.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) &&
                    context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            },
        };
    });

// --- Autorisation par rôles (policies plutôt que [Authorize(Roles = "...")] en dur,
// pour centraliser la définition des niveaux d'accès — voir plan §6) ---
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("RequireAdmin", p => p.RequireRole("Admin"))
    .AddPolicy("RequireGestionnaireOrAbove", p => p.RequireRole("Admin", "Gestionnaire"))
    .AddPolicy("RequireEmployeOrAbove", p => p.RequireRole("Admin", "Gestionnaire", "Employe"));

var app = builder.Build();

// --- Pipeline HTTP ---
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors(CorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<StockHub>("/hubs/stock").RequireAuthorization();

// --- Seed des rôles fixes (Admin/Gestionnaire/Employe) au démarrage ---
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
    await RoleSeeder.SeedAsync(roleManager);

    // Amorce le tout premier compte Admin (voir AdminSeeder : sans effet si un Admin
    // existe déjà, ou si Seed:AdminEmail/Seed:AdminPassword ne sont pas configurés).
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    await AdminSeeder.SeedAsync(userManager, roleManager, app.Configuration, logger);
}

app.Run();

// Rendu public pour les tests d'intégration (WebApplicationFactory<Program>).
public partial class Program { }
