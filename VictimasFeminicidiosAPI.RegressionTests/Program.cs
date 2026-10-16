using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using VictimasFeminicidiosAPI.Configuration;
using VictimasFeminicidiosAPI.Data;
using VictimasFeminicidiosAPI.Models;
using VictimasFeminicidiosAPI.Services;

var logsDirectory = Path.Combine(Path.GetTempPath(), "feminicidios-logs-" + Guid.NewGuid().ToString("N"));
Environment.SetEnvironmentVariable("Logs__Directory", logsDirectory);
var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
Environment.SetEnvironmentVariable("Jwt__SecretKey", secret);
Environment.SetEnvironmentVariable("ConnectionStrings__SiiidDb", "Server=unused;Database=unused");
var settings = new JwtSettings { Issuer = "VictimasFeminicidiosAPI", Audience = "VictimasFeminicidiosAPI.Clients", SecretKey = secret, MinutesToExpire = 15 };
var checks = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    checks++;
}
void BadConfig(JwtSettings config)
{
    try { config.GetSigningKey(); throw new Exception("Aceptó configuración inválida"); }
    catch (InvalidOperationException) { checks++; }
}
BadConfig(new JwtSettings());
BadConfig(new JwtSettings { Issuer = "i", Audience = "a", MinutesToExpire = 15, SecretKey = Convert.ToBase64String(new byte[32]) });
Check(!PasswordService.EsNuevaPasswordValida(new string('ñ', 37)), "BCrypt no debe truncar UTF-8");
Check(!PasswordService.EsNuevaPasswordValida("corta"), "Contraseña demasiado corta");

await using var factory = new ApiFactory();
using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
async Task<HttpResponseMessage> Login(string origen, string usuario, string password) =>
    await client.PostAsJsonAsync("/apiFeminicidios/auth/token", new { origen, usuario, password });
async Task<string> Token(HttpResponseMessage response)
{
    Check(response.IsSuccessStatusCode, "Login válido rechazado");
    using var data = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    Check(data.RootElement.GetProperty("expiresIn").GetInt32() == 900, "Vencimiento inesperado");
    return data.RootElement.GetProperty("accessToken").GetString()!;
}
async Task<HttpResponseMessage> Get(string path, string? token = null)
{
    using var request = new HttpRequestMessage(HttpMethod.Get, path);
    if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    return await client.SendAsync(request);
}
Check((await Get("/apiFeminicidios/auth/me")).StatusCode == HttpStatusCode.Unauthorized, "Permite acceso anónimo");
Check((await Get("/apiFeminicidios/openapi/v1.json")).IsSuccessStatusCode, "Falla OpenAPI con prefijo");
var admin = await Token(await Login("SIIID", "admin", FakeRepository.Password));
var external = await Token(await Login("EXTERNO", "dif", FakeRepository.Password));
Check((await Get("/apiFeminicidios/auth/me", admin)).IsSuccessStatusCode, "Superusuario rechazado");
Check((await Get("/apiFeminicidios/auth/me", external)).IsSuccessStatusCode, "DIF rechazado");
Check((await Get("/apiFeminicidios/usuarios", external)).StatusCode == HttpStatusCode.Forbidden, "DIF puede administrar usuarios");
var listing = await Get("/apiFeminicidios/usuarios", admin);
Check(listing.IsSuccessStatusCode && !(await listing.Content.ReadAsStringAsync()).Contains("password", StringComparison.OrdinalIgnoreCase), "Lista revela contraseñas o rechaza admin");
Check((await Login("EXTERNO", "dif", "incorrecta")).StatusCode == HttpStatusCode.Unauthorized, "Acepta password incorrecta");
Check((await Login("SIIID", "enlace", FakeRepository.Password)).StatusCode == HttpStatusCode.Unauthorized, "Acepta enlace SIIID");
Check((await Login("SIIID", "cambio", FakeRepository.Password)).StatusCode == HttpStatusCode.Unauthorized, "Ignora cambio obligatorio");
Check((await Login("EXTERNO", "dif", FakeRepository.Password)).StatusCode == HttpStatusCode.TooManyRequests, "No limita login");

string Forged(string issuer, string audience, DateTime expires, string algorithm = SecurityAlgorithms.HmacSha512, string? key = null)
{
    var token = new JwtSecurityToken(issuer, audience,
        [new Claim("sub", "1"), new Claim("origen", "SIIID"), new Claim("role", "SUPER_USUARIO"),
         new Claim("credencial", TokenService.Huella(factory.Repository.Admin))],
        notBefore: DateTime.UtcNow.AddHours(-2), expires: expires,
        signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Convert.FromBase64String(key ?? secret)), algorithm));
    return new JwtSecurityTokenHandler().WriteToken(token);
}
foreach (var invalid in new[]
{
    Forged("otro", settings.Audience, DateTime.UtcNow.AddMinutes(5)),
    Forged(settings.Issuer, "otra", DateTime.UtcNow.AddMinutes(5)),
    Forged(settings.Issuer, settings.Audience, DateTime.UtcNow.AddMinutes(-5)),
    Forged(settings.Issuer, settings.Audience, DateTime.UtcNow.AddMinutes(5), SecurityAlgorithms.HmacSha256),
    Forged(settings.Issuer, settings.Audience, DateTime.UtcNow.AddMinutes(5), key: Convert.ToBase64String(RandomNumberGenerator.GetBytes(64))),
    "eyJhbGciOiJub25lIn0.eyJzdWIiOiIxIn0."
}) Check((await Get("/apiFeminicidios/auth/me", invalid)).StatusCode == HttpStatusCode.Unauthorized, "Acepta JWT inválido");

using (var request = new HttpRequestMessage(HttpMethod.Post, "/apiFeminicidios/usuarios"))
{
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", admin);
    request.Content = JsonContent.Create(new { usuario = "DIF_NUEVO", nombre = "DIF", institucion = "DIF", password = FakeRepository.Password });
    Check((await client.SendAsync(request)).StatusCode == HttpStatusCode.Created, "Superusuario no puede crear externo");
}
using (var request = new HttpRequestMessage(HttpMethod.Post, "/apiFeminicidios/usuarios"))
{
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", external);
    request.Content = JsonContent.Create(new { usuario = "OTRO", nombre = "DIF", institucion = "DIF", password = FakeRepository.Password });
    Check((await client.SendAsync(request)).StatusCode == HttpStatusCode.Forbidden, "DIF puede crear usuarios");
}
using (var request = new HttpRequestMessage(HttpMethod.Post, "/apiFeminicidios/usuarios"))
{
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", admin);
    request.Content = JsonContent.Create(new { usuario = "OTRO", nombre = "DIF", institucion = "DIF", password = new string('ñ', 37) });
    Check((await client.SendAsync(request)).StatusCode == HttpStatusCode.BadRequest, "Acepta password BCrypt truncada");
}

// Cambiar habilitación y versión revoca el token, incluso al reactivar después.
using (var request = new HttpRequestMessage(HttpMethod.Patch, "/apiFeminicidios/usuarios/2/habilitado"))
{
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", admin);
    request.Content = JsonContent.Create(new { habilitado = false });
    Check((await client.SendAsync(request)).StatusCode == HttpStatusCode.NoContent, "No deshabilita externo");
}
Check((await Get("/apiFeminicidios/auth/me", external)).StatusCode == HttpStatusCode.Unauthorized, "Token deshabilitado sigue válido");
await factory.Repository.HabilitarAsync(2, true, default);
Check((await Get("/apiFeminicidios/auth/me", external)).StatusCode == HttpStatusCode.Unauthorized, "Reactivar revive token viejo");
var newExternal = new TokenService(settings).Crear(factory.Repository.External).AccessToken;
Check((await Get("/apiFeminicidios/auth/me", newExternal)).IsSuccessStatusCode, "Token nuevo rechazado");
await factory.Repository.CambiarPasswordAsync(2, PasswordService.CrearHash("OtraPasswordDePrueba2026!"), default);
Check((await Get("/apiFeminicidios/auth/me", newExternal)).StatusCode == HttpStatusCode.Unauthorized, "Cambio de contraseña no revoca");
Check((await Get("/apiFeminicidios/auth/me", admin)).Headers.CacheControl?.NoStore == true, "Respuesta se puede cachear");
Check((await Get("/api/v1/auth/me", admin)).StatusCode == HttpStatusCode.NotFound, "Sigue activa la ruta anterior");
Check((await Get("/apiFeminicidios/auth/me?password=NO_GUARDAR_QUERY", admin)).IsSuccessStatusCode, "Falla ruta nueva con query");
factory.Repository.FallarListado = true;
Check((await Get("/apiFeminicidios/usuarios", admin)).StatusCode == HttpStatusCode.InternalServerError, "No maneja error del repositorio");
factory.Repository.FallarListado = false;
await factory.DisposeAsync(); // Cierra y vacía el archivo antes de inspeccionarlo.
var logFiles = Directory.GetFiles(logsDirectory, "api-feminicidios-*.log");
Check(logFiles.Length == 1, "No genera un archivo diario");
var logText = File.ReadAllText(logFiles[0]);
foreach (var status in new[] { "-> 200", "-> 401", "-> 403", "-> 429", "-> 500" })
    Check(logText.Contains(status), "Falta estado en logs: " + status);
Check(logText.Contains("HTTP GET apiFeminicidios/usuarios -> 500"), "Error pierde la ruta");
Check(logText.Contains("TraceId") && logText.Contains("Autenticación correcta")
    && logText.Contains("Autenticación rechazada") && logText.Contains("Usuario externo"), "Falta auditoría");
Check(!logText.Contains(FakeRepository.Password) && !logText.Contains(admin)
    && !logText.Contains(external) && !logText.Contains("NO_GUARDAR_QUERY"), "El log contiene secretos");
await using (var restart = new ApiFactory())
{
    using var restartedClient = restart.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
    await restartedClient.GetAsync("/apiFeminicidios/auth/me");
}
var appendedLog = File.ReadAllText(logFiles[0]);
Check(appendedLog.StartsWith(logText) && appendedLog.Length > logText.Length, "Reiniciar trunca el log");
Directory.Delete(logsDirectory, recursive: true);
Console.WriteLine($"OK: {checks} verificaciones de autenticación, rutas y logs.");

sealed class ApiFactory : WebApplicationFactory<TokenService>
{
    public FakeRepository Repository { get; } = new();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ICuentaRepository>();
            services.AddSingleton<ICuentaRepository>(Repository);
        });
    }
}

sealed class FakeRepository : ICuentaRepository
{
    public bool FallarListado { get; set; }
    public const string Password = "PasswordDePrueba2026!";
    private readonly string _hash = PasswordService.CrearHash(Password);
    private string _externalHash;
    private int _version = 1;
    private bool _enabled = true;
    public FakeRepository() { _externalHash = _hash; }
    public Cuenta Admin => new() { Id = 1, Usuario = "admin", Origen = "SIIID", Rol = "SUPER_USUARIO", PasswordHash = _hash };
    public Cuenta External => new() { Id = 2, Usuario = "dif", Origen = "EXTERNO", Rol = "CONSULTA_FEMINICIDIOS", PasswordHash = _externalHash, Version = _version.ToString() };
    public Task<Cuenta?> BuscarAsync(string origen, string usuario, CancellationToken ct) => Task.FromResult<Cuenta?>((origen, usuario) switch
    {
        ("SIIID", "admin") => Admin,
        ("EXTERNO", "dif") when _enabled => External,
        ("SIIID", "cambio") => new Cuenta { Id = 3, Origen = "SIIID", Rol = "SUPER_USUARIO", PasswordHash = _hash, RequiereCambioPassword = true },
        _ => null
    });
    public Task<Cuenta?> BuscarPorIdAsync(string origen, int id, CancellationToken ct) => BuscarAsync(origen, id == 1 ? "admin" : "dif", ct);
    public Task<int?> CrearAsync(CrearUsuarioRequest request, string hash, int administrador, CancellationToken ct) => Task.FromResult<int?>(4);
    public Task<IReadOnlyList<UsuarioExterno>> ListarAsync(CancellationToken ct)
    {
        if (FallarListado) throw new InvalidOperationException("Fallo simulado de repositorio.");
        return Task.FromResult<IReadOnlyList<UsuarioExterno>>([new(2, "dif", "DIF", "DIF", _enabled)]);
    }
    public Task<bool> HabilitarAsync(int id, bool habilitado, CancellationToken ct)
    {
        _enabled = habilitado; _version++; return Task.FromResult(true);
    }
    public Task<bool> CambiarPasswordAsync(int id, string hash, CancellationToken ct)
    {
        _externalHash = hash; _version++; return Task.FromResult(true);
    }
}
