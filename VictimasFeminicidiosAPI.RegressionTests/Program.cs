using ClosedXML.Excel;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
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
var settings = new JwtSettings { Issuer = "VictimasFeminicidiosAPI", Audience = "VictimasFeminicidiosAPI.Clients", SecretKey = secret, MinutesToExpire = 60 };
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
BadConfig(new JwtSettings { Issuer = "i", Audience = "a", MinutesToExpire = 60, SecretKey = Convert.ToBase64String(new byte[32]) });
Check(!PasswordService.EsNuevaPasswordValida(new string('ñ', 37)), "BCrypt no debe truncar UTF-8");
Check(!PasswordService.EsNuevaPasswordValida("corta"), "Contraseña demasiado corta");
BadConfig(new JwtSettings { Issuer = "i", Audience = "a", MinutesToExpire = 61, SecretKey = secret });

// El proxy local puede reenviar esquema/IP; un emisor externo no.
async Task<DefaultHttpContext> Forward(string peer, string? clientIp)
{
    var options = new ForwardedHeadersOptions();
    ProxyConfiguration.Configurar(options);
    var context = new DefaultHttpContext();
    context.Connection.RemoteIpAddress = IPAddress.Parse(peer);
    context.Request.Scheme = "http";
    context.Request.Headers["X-Forwarded-Proto"] = "https";
    if (clientIp is not null) context.Request.Headers["X-Forwarded-For"] = clientIp;
    var middleware = new ForwardedHeadersMiddleware(_ => Task.CompletedTask, NullLoggerFactory.Instance, Options.Create(options));
    await middleware.Invoke(context);
    return context;
}
var trusted = await Forward("127.0.0.1", "203.0.113.10");
Check(trusted.Request.Scheme == "https" && trusted.Connection.RemoteIpAddress!.ToString() == "203.0.113.10", "No procesa proxy local");
var untrusted = await Forward("203.0.113.20", "203.0.113.10");
Check(untrusted.Request.Scheme == "http" && untrusted.Connection.RemoteIpAddress!.ToString() == "203.0.113.20", "Confía en proxy externo");
var asymmetric = await Forward("127.0.0.1", null);
Check(asymmetric.Request.Scheme == "http", "Procesa cabeceras incompletas");

await using var factory = new ApiFactory();
using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
async Task<HttpResponseMessage> Login(string origen, string usuario, string password) =>
    await client.PostAsJsonAsync("/apiFeminicidios/auth/token", new { origen, usuario, password });
async Task<string> Token(HttpResponseMessage response)
{
    Check(response.IsSuccessStatusCode, "Login válido rechazado");
    using var data = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    Check(data.RootElement.GetProperty("expiresIn").GetInt32() == 3600, "Vencimiento inesperado");
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
var issued = new JwtSecurityTokenHandler().ReadJwtToken(external);
Check(Math.Abs((issued.ValidTo - issued.ValidFrom).TotalSeconds - 3600) < 1, "JWT no dura una hora");
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

// Contrato de consulta: repositorio simulado; nunca conecta a una base real.
Check((await Get("/apiFeminicidios/consulta?anio=2026&mes=9")).StatusCode == HttpStatusCode.Unauthorized, "Consulta pública");
var jsonResponse = await Get("/apiFeminicidios/consulta?anio=2026&mes=9", external);
Check(jsonResponse.IsSuccessStatusCode, "DIF no puede consultar");
Check(factory.Feminicidios.UltimoPeriodo == (2026, 9), "No envía el periodo al repositorio");
using var jsonDoc = JsonDocument.Parse(await jsonResponse.Content.ReadAsStringAsync());
var expectedHeaders = JsonSerializer.Deserialize<string[]>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ColumnasReferencia.json")))!;
var jsonRow = jsonDoc.RootElement[0];
Check(jsonRow.EnumerateObject().Select(x => x.Name).SequenceEqual(expectedHeaders), "JSON no coincide con las 48 columnas del ejemplo");
Check(!jsonRow.TryGetProperty("IdRegistro", out _), "Expone ID interno de la consulta");
Check(jsonRow.GetProperty("MesCorte").GetString() == "SEPTIEMBRE", "Mes no está en letras");
Check(jsonRow.GetProperty("CURP").ValueKind == JsonValueKind.Null, "NULL se convirtió en texto");
Check(jsonResponse.Headers.GetValues("X-Total-Registros").Single() == "1", "Conteo JSON incorrecto");
var xlsxResponse = await Get("/apiFeminicidios/consulta?anio=2026&mes=9&formato=xlsx", admin);
Check(xlsxResponse.IsSuccessStatusCode, "Superusuario no puede descargar Excel");
Check(xlsxResponse.Content.Headers.ContentType?.MediaType == "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "MIME Excel incorrecto");
Check((xlsxResponse.Content.Headers.ContentDisposition?.FileNameStar ?? "").Contains("2026_09"), "Nombre Excel incorrecto");
using (var stream = new MemoryStream(await xlsxResponse.Content.ReadAsByteArrayAsync()))
using (var book = new XLWorkbook(stream))
{
    var sheet = book.Worksheet(1);
    Check(sheet.LastColumnUsed()!.ColumnNumber() == 48, "Excel incluye una columna extra");
    Check(expectedHeaders.SequenceEqual(Enumerable.Range(1, 48).Select(c => sheet.Cell(1, c).GetString())), "Encabezados Excel distintos");
    Check(sheet.LastRowUsed()!.RowNumber() == 2, "Excel duplica/omite datos");
    Check(sheet.Cell(2, 2).GetString() == "=1+1" && !sheet.Cell(2, 2).HasFormula, "Excel ejecuta texto como fórmula");
    Check(sheet.Cell(2, 44).DataType == XLDataType.Text && sheet.Cell(2, 44).GetString() == "01000", "CP pierde cero inicial");
    Check(sheet.Cell(2, 13).GetValue<int>() == jsonRow.GetProperty("Edad").GetInt32(), "Edad Excel/JSON distinta");
    Check(sheet.Cell(2, 21).GetDateTime().ToString("yyyy-MM-dd") == jsonRow.GetProperty("FechaInicioCarpeta").GetString(), "Fecha Excel/JSON distinta");
    Check(sheet.Cell(2, 22).GetTimeSpan().ToString() == jsonRow.GetProperty("HoraInicioCarpeta").GetString(), "Hora Excel/JSON distinta");
    Check(sheet.Cell(2, 5).IsEmpty(), "NULL no quedó vacío en Excel");
}
foreach (var query in new[] { "anio=2026&mes=0", "anio=2026&mes=13", "mes=9", "anio=2026", "anio=2026&mes=9&formato=csv", "anio=texto&mes=9" })
    Check((await Get("/apiFeminicidios/consulta?" + query, external)).StatusCode == HttpStatusCode.BadRequest, "Acepta parámetros inválidos: " + query);
var empty = await Get("/apiFeminicidios/consulta?anio=2026&mes=8", external);
Check((await empty.Content.ReadAsStringAsync()) == "[]", "Periodo vacío no devuelve []");
var emptyExcel = await Get("/apiFeminicidios/consulta?anio=2026&mes=8&formato=xlsx", external);
using (var book = new XLWorkbook(new MemoryStream(await emptyExcel.Content.ReadAsByteArrayAsync())))
    Check(book.Worksheet(1).LastRowUsed()!.RowNumber() == 1 && book.Worksheet(1).LastColumnUsed()!.ColumnNumber() == 48, "Excel vacío pierde encabezados");
factory.Feminicidios.Duplicar = true;
Check((await Get("/apiFeminicidios/consulta?anio=2026&mes=9", external)).StatusCode == HttpStatusCode.InternalServerError, "Publica filas ambiguas duplicadas");
factory.Feminicidios.Duplicar = false;

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
// Bitácora SQL simulada: cubre éxito, rechazos, errores y ausencia de datos sensibles.
var eventos = factory.Bitacora.Eventos;
Check(eventos.Count(e => e.Evento == "LOGIN" && e.EstadoHttp == 200) == 2, "Faltan accesos correctos");
Check(eventos.Any(e => e.Evento == "LOGIN" && e.EstadoHttp == 401 && e.Usuario == "dif"), "Falta acceso rechazado");
Check(eventos.Any(e => e.Evento == "LOGIN" && e.EstadoHttp == 429 && e.Usuario is null), "Falta login limitado");
Check(eventos.Any(e => e.Evento == "CONSULTA" && e.EstadoHttp == 401 && e.IdUsuario is null), "Falta consulta sin token");
Check(eventos.Any(e => e.Evento == "CONSULTA" && e.EstadoHttp == 400), "Falta consulta inválida");
Check(eventos.Any(e => e.Evento == "CONSULTA" && e.EstadoHttp == 500), "Falta consulta fallida");
foreach (var formato in new[] { "json", "xlsx" })
{
    Check(eventos.Any(e => e.Evento == "CONSULTA" && e.EstadoHttp == 200 && e.Anio == 2026
        && e.Mes == 9 && e.Formato == formato && e.TotalRegistros == 1 && e.IdUsuario is not null), "Consulta sin detalle: " + formato);
    Check(eventos.Any(e => e.Evento == "CONSULTA" && e.EstadoHttp == 200 && e.Mes == 8
        && e.Formato == formato && e.TotalRegistros == 0), "Consulta vacía sin bitácora: " + formato);
}
Check(eventos.All(e => e.FechaUtc.Kind == DateTimeKind.Utc && e.DuracionMs >= 0 && !string.IsNullOrEmpty(e.TraceId)), "Metadatos incompletos");
var auditoriaJson = JsonSerializer.Serialize(eventos);
Check(!auditoriaJson.Contains(FakeRepository.Password) && !auditoriaJson.Contains(external)
    && !auditoriaJson.Contains("=1+1") && !auditoriaJson.Contains("01000"), "Bitácora contiene secretos/datos");
factory.Bitacora.Fallar = true;
var sinBitacora = await Get("/apiFeminicidios/consulta?anio=2026&mes=9", admin);
Check(sinBitacora.StatusCode == HttpStatusCode.ServiceUnavailable
    && !(await sinBitacora.Content.ReadAsStringAsync()).Contains("IdentificadorVictima")
    && !sinBitacora.Headers.Contains("X-Total-Registros"), "Entrega datos sin bitácora");
factory.Bitacora.Fallar = false;
await using (var auditFailure = new ApiFactory())
{
    auditFailure.Bitacora.Fallar = true;
    using var auditClient = auditFailure.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
    var loginNoAudit = await auditClient.PostAsJsonAsync("/apiFeminicidios/auth/token",
        new { origen = "EXTERNO", usuario = "dif", password = FakeRepository.Password });
    Check(loginNoAudit.StatusCode == HttpStatusCode.ServiceUnavailable
        && !(await loginNoAudit.Content.ReadAsStringAsync()).Contains("accessToken"), "Emite token sin bitácora");
}
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
    && !logText.Contains(external) && !logText.Contains("NO_GUARDAR_QUERY")
    && !logText.Contains("=1+1") && !logText.Contains("01000"), "El log contiene secretos");
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
    public FakeBitacoraRepository Bitacora { get; } = new();
    public FakeFeminicidiosRepository Feminicidios { get; } = new();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IBitacoraRepository>();
            services.AddSingleton<IBitacoraRepository>(Bitacora);
            services.RemoveAll<ICuentaRepository>();
            services.AddSingleton<ICuentaRepository>(Repository);
            services.RemoveAll<IFeminicidiosRepository>();
            services.AddSingleton<IFeminicidiosRepository>(Feminicidios);
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

sealed class FakeFeminicidiosRepository : IFeminicidiosRepository
{
    public bool Duplicar { get; set; }
    public (int Anio, int Mes) UltimoPeriodo { get; private set; }
    public Task<IReadOnlyList<FeminicidioRegistro>> ConsultarAsync(int anio, int mes, CancellationToken ct)
    {
        UltimoPeriodo = (anio, mes);
        if (mes == 8) return Task.FromResult<IReadOnlyList<FeminicidioRegistro>>([]);
        var row = new FeminicidioRegistro
        {
            IdRegistro = 1, IdentificadorVictima = "0001", Nombre = "=1+1", CURP = null,
            Edad = 30, AnioCorte = 2026, MesCorte = "SEPTIEMBRE", IdentificadorCarpeta = "00001",
            FechaInicioCarpeta = "2026-09-01", HoraInicioCarpeta = "16:40:50",
            FechaHechos = "2026-09-01", HoraHechos = "00:00:00", CodigoPostal = "01000",
            GradoConsumacion = "Consumado", SubtipoDelito = "Feminicidio",
            FechaRegistro = new DateTime(2026, 9, 2, 12, 0, 0)
        };
        return Task.FromResult<IReadOnlyList<FeminicidioRegistro>>(Duplicar ? [row, row] : [row]);
    }
}

sealed class FakeBitacoraRepository : IBitacoraRepository
{
    public List<BitacoraEvento> Eventos { get; } = [];
    public bool Fallar { get; set; }
    public Task RegistrarAsync(BitacoraEvento evento, CancellationToken ct)
    {
        if (Fallar) throw new InvalidOperationException("Fallo simulado de bitácora.");
        Eventos.Add(evento);
        return Task.CompletedTask;
    }
}
