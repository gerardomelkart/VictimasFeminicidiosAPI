using System.Threading.RateLimiting;
using Serilog;
using Serilog.Events;
using VictimasFeminicidiosAPI.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using VictimasFeminicidiosAPI.Configuration;
using VictimasFeminicidiosAPI.Data;
using VictimasFeminicidiosAPI.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(ProxyConfiguration.Configurar);
// Ruta relativa al proyecto, independiente del directorio desde el que se inicie.
var logsDirectory = builder.Configuration["Logs:Directory"] ?? "logs";
logsDirectory = Path.GetFullPath(logsDirectory, builder.Environment.ContentRootPath);
Directory.CreateDirectory(logsDirectory);
builder.Services.AddSerilog((services, configuration) => configuration
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Aplicacion", "VictimasFeminicidiosAPI")
    .Enrich.WithProperty("Ambiente", builder.Environment.EnvironmentName)
    .WriteTo.Console()
    .WriteTo.File(Path.Combine(logsDirectory, "api-feminicidios-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: null,
        fileSizeLimitBytes: null,
        buffered: false,
        shared: true,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"));
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 16 * 1024);
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();
var key = jwt.GetSigningKey(); // Sin claves de respaldo ni secretos versionados.
if (string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("SiiidDb")))
    throw new InvalidOperationException("Configure ConnectionStrings:SiiidDb fuera del repositorio.");

builder.Services.AddSingleton(jwt);
builder.Services.AddSingleton<PasswordService>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddScoped<ICuentaRepository, CuentaRepository>();
builder.Services.AddScoped<IBitacoraRepository, BitacoraRepository>();
builder.Services.AddScoped<IFeminicidiosRepository, FeminicidiosRepository>();
builder.Services.AddScoped<FeminicidiosService>();
builder.Services.AddSingleton<FeminicidiosExcel>();
builder.Services.AddScoped<CuentaTokenEvents>();
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.MapInboundClaims = false;
    options.IncludeErrorDetails = false;
    options.EventsType = typeof(CuentaTokenEvents);
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = jwt.Issuer,
        ValidateAudience = true, ValidAudience = jwt.Audience,
        ValidateLifetime = true, RequireExpirationTime = true,
        ValidateIssuerSigningKey = true, RequireSignedTokens = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidAlgorithms = [SecurityAlgorithms.HmacSha512],
        ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = "sub", RoleClaimType = "role"
    };
});
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser()
        .RequireRole("SUPER_USUARIO", "CONSULTA_FEMINICIDIOS").Build();
    options.AddPolicy("AdministrarUsuarios", policy => policy.RequireAuthenticatedUser()
        .RequireRole("SUPER_USUARIO").RequireClaim("origen", "SIIID"));
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    // Antes de autenticar: limita trabajo SQL/BCrypt. No confiar en X-Forwarded-For del cliente.
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(_ =>
        RateLimitPartition.GetFixedWindowLimiter("api", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0
        }));
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "desconocida", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5, Window = TimeSpan.FromMinutes(1), QueueLimit = 0
        }));
});

var app = builder.Build();
app.UseForwardedHeaders();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<BitacoraMiddleware>();
app.UseExceptionHandler();
if (!app.Environment.IsDevelopment()) app.UseHsts();
app.UseHttpsRedirection();
app.Use(async (context, next) =>
{
    context.Response.Headers.CacheControl = "no-store";
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    await next();
});
app.UseRouting();
app.Use((context, next) =>
{
    // Conservar la plantilla aunque el manejador de errores limpie el endpoint.
    context.Items[RequestLoggingMiddleware.RouteKey] =
        (context.GetEndpoint() as Microsoft.AspNetCore.Routing.RouteEndpoint)?.RoutePattern.RawText;
    return next(context);
});
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
if (app.Environment.IsDevelopment()) app.MapOpenApi("/apiFeminicidios/openapi/{documentName}.json").AllowAnonymous();
app.MapControllers();
app.Logger.LogInformation("Iniciando API. Logs diarios en {Directorio}.", logsDirectory);
app.Run();

public partial class Program { }
