# API de consulta de víctimas de feminicidio

API REST desarrollada en **ASP.NET Core / .NET 10** para compartir información del consolidado SIIID con el Sistema Nacional DIF mediante consultas mensuales y descargas en **JSON y Excel**.

El servicio se despliega de forma independiente de la aplicación SIIID. Combina autenticación JWT, autorización por roles, acceso a SQL Server y una bitácora de inicios de sesión y consultas.

## Funcionalidades

- Consulta por año y mes de corte de registros activos, confirmados y aceptados administrativamente, conforme a las reglas de SIIID.
- Exportación JSON y XLSX con el mismo contrato de 27 campos.
- Acceso para cuentas externas de consulta y superusuarios de SIIID.
- Administración de cuentas externas: creación, listado, habilitación y cambio de contraseña.
- Registro de autenticaciones y consultas en SQL Server, con fecha y hora de Ciudad de México.
- Logs estructurados, límites de solicitudes y manejo de errores.

## Tecnologías

| Componente | Tecnología |
| --- | --- |
| Servicio HTTP | ASP.NET Core, C#, .NET 10 |
| Persistencia | SQL Server, Dapper, Microsoft.Data.SqlClient |
| Autenticación | JWT Bearer con HS512 |
| Contraseñas externas | BCrypt |
| Exportación Excel | ClosedXML |
| Logs | Serilog, consola y archivos |
| Pruebas | Servidor en memoria y repositorios simulados |
| Despliegue | Linux, systemd y Apache como proxy inverso |

## Arquitectura y decisiones

Los controladores exponen autenticación, consulta y administración. Los servicios concentran la emisión y validación de tokens, las reglas de consulta y la generación de archivos. Los repositorios realizan el acceso a SQL Server.

La API utiliza la base existente de SIIID y tablas propias para cuentas externas y bitácora. Esto permite mantener un servicio independiente sin duplicar el consolidado ni crear otra base para administrar sus consumidores.

Los tokens duran **una hora**. Cada petición protegida verifica el estado actual de la cuenta y su credencial; cambiar la contraseña o deshabilitar una cuenta invalida su acceso. Las cuentas externas pueden consultar información, mientras que la administración exige el rol `SUPER_USUARIO` de SIIID.

La bitácora se guarda antes de entregar un token o una consulta. Si no se puede registrar el evento, el servicio responde `503` sin entregar el token ni los datos. Los logs y la bitácora no almacenan contraseñas, tokens ni el contenido de los registros consultados.

## Endpoints

Todas las rutas utilizan el prefijo `/apiFeminicidios`.

| Método | Ruta | Función | Acceso |
| --- | --- | --- | --- |
| POST | `/auth/token` | Obtener un token | Credenciales válidas |
| GET | `/auth/me` | Consultar la identidad autenticada | Usuario autenticado |
| GET | `/consulta` | Consultar JSON o descargar Excel | Cuenta externa o superusuario |
| GET | `/usuarios` | Listar cuentas externas | Superusuario |
| POST | `/usuarios` | Crear una cuenta externa | Superusuario |
| PATCH | `/usuarios/{id}/habilitado` | Habilitar o deshabilitar una cuenta | Superusuario |
| PUT | `/usuarios/{id}/password` | Restablecer una contraseña | Superusuario |

Ejemplo de consulta con un token obtenido previamente:

```http
GET https://localhost:7040/apiFeminicidios/consulta?anio=2026&mes=9&formato=json
Authorization: Bearer <TOKEN>
```

Para descargar Excel, usar `formato=xlsx`. El año y el mes son obligatorios y corresponden al corte de la carga, no a la fecha de los hechos. Ambos formatos incluyen el encabezado `X-Total-Registros`. Si no hay resultados, JSON devuelve `[]` y Excel conserva sus encabezados.

## Ejecutar en desarrollo

Se necesita el SDK **.NET 10** y acceso autorizado a una base SQL Server con el esquema SIIID y las tablas de cuentas externas y bitácora. El repositorio no incluye la base institucional ni sus datos y no ejecuta migraciones automáticas. Para revisar el proyecto sin esa base se pueden ejecutar las pruebas con repositorios simulados.

Desde la raíz del repositorio:

```powershell
dotnet restore VictimasFeminicidiosAPI.slnx
dotnet user-secrets set "ConnectionStrings:SiiidDb" "<CADENA_DE_CONEXION_LOCAL>" --project VictimasFeminicidiosAPI
```

Generar una clave JWT aleatoria de 64 bytes y guardarla en User Secrets:

```powershell
$jwtBytes = New-Object byte[] 64
$jwtRng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$jwtRng.GetBytes($jwtBytes)
$jwtRng.Dispose()
$jwtSecret = [Convert]::ToBase64String($jwtBytes)
dotnet user-secrets set "Jwt:SecretKey" $jwtSecret --project VictimasFeminicidiosAPI
Remove-Variable jwtSecret, jwtBytes

dotnet dev-certs https --trust
dotnet run --project VictimasFeminicidiosAPI --launch-profile https
```

Dirección local: `https://localhost:7040`. La descripción OpenAPI está disponible únicamente en desarrollo en `/apiFeminicidios/openapi/v1.json`. El servicio no incluye interfaz gráfica.

Las credenciales y claves se configuran fuera de Git mediante User Secrets o variables de entorno. Los ejemplos de esta documentación usan marcadores de posición.

## Verificación

```powershell
dotnet build VictimasFeminicidiosAPI.slnx
dotnet run --project VictimasFeminicidiosAPI.RegressionTests
```

Las pruebas cubren autenticación, roles, revocación de tokens, límites de intentos, validación de parámetros, contrato de 27 columnas, exportación JSON/Excel y fallos de bitácora. Usan datos simulados y no requieren una base real. La ejecución del SQL y la conciliación de resultados requieren validación adicional en una base autorizada.

## Documentación

- [Guía técnica](docs/GUIA_TECNICA.md): configuración, cuentas, contrato de columnas, reglas de consulta y bitácora.
- [Despliegue en Linux](deploy/README.md): publicación, servicio systemd, secretos, logs y proxy Apache.

## Autor

**Gerardo Melkart Noeller Álvarez** · [GitHub](https://github.com/gerardomelkart)
