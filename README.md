# API de consulta de victimas de feminicidio

API independiente de SIIID, basada en .NET 10. Primero se prueba en desarrollo; no tiene ambiente QA.

## Estado de esta entrega

Autenticacion JWT implementada, con acceso del DIF mediante cuentas externas y de superusuarios SIIID mediante sus cuentas actuales. Administracion de cuentas externas disponible solo para superusuarios. Todavia no incluye consultas de feminicidios ni exportaciones.

Antes de iniciar, ejecutar en desarrollo el SQL entregado por el chat para `dbo.api_feminicidios_usuario`. La API no crea ni migra tablas automaticamente. Los repositorios originales SIIID y BD no se modifican.

## Desarrollo

Abrir `VictimasFeminicidiosAPI.slnx` con el SDK .NET 10. Se usa la misma conexion local de SIIID: `(localdb)\MSSQLLocalDB`, base `SIIID2`, autenticacion integrada de Windows. No lleva contrasena SQL.

ASP.NET Core carga `appsettings.json`, despues `appsettings.Development.json` y, en desarrollo, User Secrets. Las variables de entorno permiten sobrescribir la configuracion.

Para una conexion local diferente, usar Administrar secretos de usuario en Visual Studio o:

```powershell
dotnet user-secrets set "ConnectionStrings:SiiidDb" "CADENA_LOCAL" --project VictimasFeminicidiosAPI
```

Configurar la clave JWT propia como `Jwt:SecretKey` en User Secrets. En PowerShell, desde la raiz del repositorio:

```powershell
$jwtBytes = New-Object byte[] 64
$jwtRng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$jwtRng.GetBytes($jwtBytes)
$jwtRng.Dispose()
$jwtSecret = [Convert]::ToBase64String($jwtBytes)
dotnet user-secrets set "Jwt:SecretKey" $jwtSecret --project VictimasFeminicidiosAPI
Remove-Variable jwtSecret, jwtBytes

dotnet run --project VictimasFeminicidiosAPI --launch-profile https
```

La API rechaza el arranque si falta la conexion o si la clave no es Base64 de al menos 64 bytes. Usar otra clave aleatoria para produccion. No reutilizar claves de SIIID ni versionarlas. Mantener esta clave estable entre ejecuciones; cambiarla invalida todos los tokens.

URL local: `https://localhost:7040`. OpenAPI JSON solamente en desarrollo: `/openapiFeminicidios.json`. Si hace falta, confiar en el certificado de desarrollo con `dotnet dev-certs https --trust`. No hay pantalla de login ni Swagger UI.

## Probar el acceso y crear la cuenta DIF

1. `POST /apiFeminicidios/auth/token`, con JSON `{"origen":"SIIID","usuario":"TU_SUPERUSUARIO","password":"TU_PASSWORD"}`. Debe ser una cuenta activa con rol activo SUPER_USUARIO. Si requiere cambiar contrasena, realizar el cambio en SIIID antes de ingresar aqui.
2. Copiar `accessToken` de la respuesta. Enviar `Authorization: Bearer TOKEN` en las peticiones protegidas.
3. `GET /apiFeminicidios/auth/me` comprueba el acceso. `GET /apiFeminicidios/usuarios` lista las cuentas externas sin hashes.
4. Crear DIF con `POST /apiFeminicidios/usuarios`, JSON `{"usuario":"DIF","nombre":"Consulta DIF","institucion":"DIF","password":"CONTRASENA_PRIVADA_DE_AL_MENOS_12_CARACTERES"}`. Elegir la contrasena fuera de Git; la API almacena BCrypt con costo 12, no texto claro. Maximo 72 bytes UTF-8 para evitar truncamiento BCrypt.
5. DIF solicita su token con `POST /apiFeminicidios/auth/token`, JSON `{"origen":"EXTERNO","usuario":"DIF","password":"SU_PASSWORD"}`. Tiene acceso de consulta; no puede administrar usuarios.
6. Un superusuario puede deshabilitar mediante `PATCH /apiFeminicidios/usuarios/ID/habilitado`, JSON `{"habilitado":false}`, o restablecer contrasena con `PUT /apiFeminicidios/usuarios/ID/password`, JSON `{"password":"NUEVA_PASSWORD_PRIVADA"}`.

No existe registro publico. El origen es explicito para evitar confusiones entre nombres coincidentes. No se aceptan tokens emitidos por la API SIIID.

JWT: HS512, emisor y audiencia propios, 15 minutos y tolerancia de reloj de 30 segundos. Cada peticion comprueba en BD el estado de la cuenta, rol, cambio obligatorio y huella de credencial. Cambiar password o deshabilitar invalida el acceso. La version ROWVERSION externa tambien impide revivir un token al reactivar la cuenta.

Limites iniciales por proceso: 60 solicitudes/minuto globales y 5 intentos/minuto por IP al login (HTTP 429 al exceder). No se confia en cabeceras de proxy aportadas por clientes. Al publicar, configurar el proxy Apache de confianza para HTTPS/IP real y revisar limites. Respuestas sin cache y errores genericos. Logs de acceso/administracion por ID, sin contrasenas, tokens ni datos nominales.

## Verificacion automatica

```powershell
dotnet build VictimasFeminicidiosAPI.slnx
dotnet run --project VictimasFeminicidiosAPI.RegressionTests
```

Las pruebas usan servidor en memoria y repositorio simulado; cubren acceso anonimo, roles, BCrypt, JWT invalidos, revocacion y limite de intentos. No sustituyen la prueba contra la base local de SIIID. No leen ni modifican bases reales.

## Produccion (posterior a pruebas)

Servidor y base SIIID existentes. Configurar mediante archivo de entorno privado del servicio, fuera del repositorio y accesible solo para el administrador:

- `ASPNETCORE_ENVIRONMENT=Production`
- `ConnectionStrings__SiiidDb`: SQL Server `127.0.0.1,1433`, base `SIIID2`; credenciales propias con permisos minimos.
- `Jwt__SecretKey`: secreto exclusivo de produccion.

No copiar las credenciales de la API SIIID al repositorio. Usar `Encrypt=True` y validar el certificado SQL en produccion; revisar la confianza del certificado del servidor antes del despliegue. La configuracion actual de SIIID usa `TrustServerCertificate=True`, lo cual se debe revisar para este servicio.

Ruta prevista: `https://cni03.sspc.gob.mx/apiFeminicidios/`. Servicio y puerto local propios; comprobar disponibilidad al publicar. Todavia no se configura Apache ni systemd.

## Pendiente: consulta y descargas

La revision de `InformeRepository.SabanasVictimasEstatales.cs` en SIIID confirma el uso de registros activos unidos con `carga` activa y estos estados:

- `tipo_carga = 'CARGA_INICIAL'` con `estado = 'CONFIRMADO'`.
- `tipo_carga = 'ACTUALIZACION'` con `estado = 'CONFIRMADO_ACTUALIZACION'`.

La consulta de la siguiente entrega debe usar anio/mes de corte y feminicidios consumados, sin previos, mixtos ni Federal. Respetara las 48 columnas con encabezado del Excel de referencia, sin ReferenciaCarga ni columna vacia, con MesCorte en letras. Validar cardinalidad victima/delito y equivalencia JSON/Excel.

Solo se modifica este repositorio, sin ramas adicionales y con un commit por entrega. Los repositorios SIIID son referencia de lectura. Los scripts SQL se entregan por el chat para que el propietario los ejecute y guarde en el repositorio de BD.

## Logs

Serilog escribe en consola y en `logs/api-feminicidios-YYYYMMDD.log`, relativo a la carpeta del proyecto/publicacion. El archivo del dia se abre para agregar registros; reiniciar la API no lo borra. No se borran historicos automaticamente ni se trunca el archivo por tamano. Los logs estan excluidos de Git.

Se registran inicio, acceso correcto/rechazado, administracion de usuarios, errores y una linea por peticion con metodo, plantilla de ruta, estado HTTP, duracion, ID/origen del usuario autenticado y TraceId. Se incluyen 401, 403, 429 y errores 500. No se registran cuerpos, Authorization, tokens, contrasenas ni query strings. Microsoft/System se limitan a Warning para evitar logs rutinarios de URLs completas.

Para cambiar la carpeta usar `Logs:Directory` en User Secrets (desarrollo) o `Logs__Directory` en el entorno del servicio (produccion). La cuenta del proceso necesita escritura. En produccion se usara una carpeta persistente fuera del directorio reemplazado por la publicacion. El despliegue Apache posterior debe conservar el prefijo `/apiFeminicidios/` al reenviar las peticiones.
