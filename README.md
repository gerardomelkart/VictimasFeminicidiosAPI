# API de consulta de victimas de feminicidio

API independiente de SIIID, basada en .NET 10. Primero se prueba en desarrollo; no tiene ambiente QA.

## Estado de esta entrega

Autenticacion JWT implementada, con acceso del DIF mediante cuentas externas y de superusuarios SIIID mediante sus cuentas actuales. Administracion de cuentas externas disponible solo para superusuarios. Consulta mensual y descarga JSON/Excel implementadas.

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

URL local: `https://localhost:7040`. OpenAPI JSON solamente en desarrollo: `/apiFeminicidios/openapi/v1.json`. Si hace falta, confiar en el certificado de desarrollo con `dotnet dev-certs https --trust`. No hay pantalla de login ni Swagger UI.

## Probar el acceso y crear la cuenta DIF

1. `POST /apiFeminicidios/auth/token`, con JSON `{"origen":"SIIID","usuario":"TU_SUPERUSUARIO","password":"TU_PASSWORD"}`. Debe ser una cuenta activa con rol activo SUPER_USUARIO. Si requiere cambiar contrasena, realizar el cambio en SIIID antes de ingresar aqui.
2. Copiar `accessToken` de la respuesta. Enviar `Authorization: Bearer TOKEN` en las peticiones protegidas.
3. `GET /apiFeminicidios/auth/me` comprueba el acceso. `GET /apiFeminicidios/usuarios` lista las cuentas externas sin hashes.
4. Crear DIF con `POST /apiFeminicidios/usuarios`, JSON `{"usuario":"DIF","nombre":"Consulta DIF","institucion":"DIF","password":"CONTRASENA_PRIVADA_DE_AL_MENOS_12_CARACTERES"}`. Elegir la contrasena fuera de Git; la API almacena BCrypt con costo 12, no texto claro. Maximo 72 bytes UTF-8 para evitar truncamiento BCrypt.
5. DIF solicita su token con `POST /apiFeminicidios/auth/token`, JSON `{"origen":"EXTERNO","usuario":"DIF","password":"SU_PASSWORD"}`. Tiene acceso de consulta; no puede administrar usuarios.
6. Un superusuario puede deshabilitar mediante `PATCH /apiFeminicidios/usuarios/ID/habilitado`, JSON `{"habilitado":false}`, o restablecer contrasena con `PUT /apiFeminicidios/usuarios/ID/password`, JSON `{"password":"NUEVA_PASSWORD_PRIVADA"}`.

No existe registro publico. El origen es explicito para evitar confusiones entre nombres coincidentes. No se aceptan tokens emitidos por la API SIIID.

JWT: HS512, emisor y audiencia propios, 60 minutos y tolerancia de reloj de 30 segundos. Cada peticion comprueba en BD el estado de la cuenta, rol, cambio obligatorio y huella de credencial. Cambiar password o deshabilitar invalida el acceso. La version ROWVERSION externa tambien impide revivir un token al reactivar la cuenta.

Limites iniciales por proceso: 60 solicitudes/minuto globales y 5 intentos/minuto por IP al login (HTTP 429 al exceder). Solo se procesan cabeceras reenviadas desde el proxy local de confianza (127.0.0.1/::1, un salto). Apache las reemplaza en la ruta de esta API; ver deploy/README.md. Respuestas sin cache y errores genericos. Logs de acceso/administracion por ID, sin contrasenas, tokens ni datos nominales.

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

Ruta prevista: `https://cni03.sspc.gob.mx/apiFeminicidios/`. Servicio y puerto local propios; comprobar disponibilidad al publicar. Plantillas e instaladores en [deploy/README.md](deploy/README.md).

## Consulta mensual y descargas

Con el token del DIF o de un superusuario:

```http
GET https://localhost:7040/apiFeminicidios/consulta?anio=2026&mes=9&formato=json
Authorization: Bearer TOKEN
```

Cambiar `formato=xlsx` descarga `feminicidios_2026_09.xlsx`. Si se omite formato se usa JSON. Anio (2000 a 2100) y mes (1 a 12) son obligatorios. Parametros invalidos producen HTTP 400. Ambos formatos llevan `X-Total-Registros` con el conteo. Sin registros, JSON devuelve `[]` y Excel conserva sus 48 encabezados.

Los nombres, orden y significado de las columnas siguen el ejemplo de septiembre. No se incluye ReferenciaCarga ni la columna vacia. MesCorte se devuelve en mayusculas en espanol, las fechas de nacimiento/inicio/hechos como `yyyy-MM-dd` en JSON y fechas numericas en Excel. Horas `HH:mm:ss`, identificadores/CURP/CP como texto, nulos JSON como `null` y celdas Excel vacias. FechaRegistro conserva la fecha de registro de la carpeta, como la consulta nominal anterior. Excel guarda textos como valores literales, sin formulas.

El SQL esta en `Data/FeminicidiosSql.cs` y consulta directamente tablas, sin crear vistas, procedimientos ni otros objetos. Usa el mes/anio de corte de la carga de la carpeta (no la fecha de hechos) y verifica carpeta/delito/victima activos y las tres cargas activas con estos pares:

- `tipo_carga = 'CARGA_INICIAL'` con `estado = 'CONFIRMADO'`.
- `tipo_carga = 'ACTUALIZACION'` con `estado = 'CONFIRMADO_ACTUALIZACION'`.

Filtra `subtipo_delito = 'Feminicidio'` y `grado_consumacion.descripcion = 'Consumado'`, como el query previo. Excluye tentativas, previos, pendientes, rechazados y Federal; conserva la exclusion del usuario federal de las vistas nominales existentes. Las relaciones de sabana con textos identicos se unifican antes del join. Si quedan clasificaciones ambiguas que dupliquen un registro de victima, la API registra un error y no exporta un resultado duplicado.

La consulta y conteo quedan en el log, sin datos nominales. Las pruebas automaticas comprueban contrato de 48 columnas contra encabezados extraidos del ejemplo, JSON/Excel, parametros, celdas tipadas, textos que parecen formulas y duplicados. La ejecucion del SQL y la conciliacion de septiembre contra los datos reales se deben probar en la base local del propietario.

Los nuevos tokens duran una hora (`expiresIn=3600`). Los ya emitidos mantienen su vencimiento original: solicitar otro token despues de reiniciar la API. No hace falta cambiar la clave JWT ni la tabla.

Solo se modifica este repositorio, sin ramas adicionales y con un commit por entrega. Los repositorios SIIID son referencia de lectura. Los scripts SQL se entregan por el chat para que el propietario los ejecute y guarde en el repositorio de BD.

## Logs

Serilog escribe en consola y en `logs/api-feminicidios-YYYYMMDD.log`, relativo a la carpeta del proyecto/publicacion. El archivo del dia se abre para agregar registros; reiniciar la API no lo borra. No se borran historicos automaticamente ni se trunca el archivo por tamano. Los logs estan excluidos de Git.

Se registran inicio, acceso correcto/rechazado, administracion de usuarios, errores y una linea por peticion con metodo, plantilla de ruta, estado HTTP, duracion, ID/origen del usuario autenticado y TraceId. Se incluyen 401, 403, 429 y errores 500. No se registran cuerpos, Authorization, tokens, contrasenas ni query strings. Microsoft/System se limitan a Warning para evitar logs rutinarios de URLs completas.

Para cambiar la carpeta usar `Logs:Directory` en User Secrets (desarrollo) o `Logs__Directory` en el entorno del servicio (produccion). La cuenta del proceso necesita escritura. En produccion se usara una carpeta persistente fuera del directorio reemplazado por la publicacion. El despliegue Apache posterior debe conservar el prefijo `/apiFeminicidios/` al reenviar las peticiones.

## Publicacion

Seguir [deploy/README.md](deploy/README.md) para publicar linux-x64 desde Visual Studio, subir el ZIP por scp e instalar en el puerto 5003. Los scripts separan secretos, logs y binarios, y no ejecutan cambios en BD.


## Bitacora SQL de accesos y consultas

Antes de publicar esta version, crear `dbo.api_feminicidios_bitacora` en la base SIIID de desarrollo y despues en produccion, con el script entregado por chat. La API no ejecuta DDL ni migraciones automaticas. La cuenta SQL de la API necesita INSERT en esta tabla. No cambia el contrato ni el manual externo.

Se registra una fila por POST `/apiFeminicidios/auth/token` y GET `/apiFeminicidios/consulta`, incluidos rechazos 400/401/403/429, errores 500 y cancelaciones observadas (499). Fecha de inicio de Ciudad de Mexico (`fecha_hora_mexico`), evento LOGIN/CONSULTA, origen, ID y nombre del usuario, IP resuelta por el proxy confiable, TraceId, estado HTTP y duracion. En consultas agrega anio/mes, formato normalizado y conteo solo si hubo respuesta 200. Los intentos de login rechazados conservan el usuario intentado; su ID queda NULL. Cuando el limite o la validacion rechaza antes del controlador, la identidad puede quedar NULL. No se infiere identidad de JWT rechazados. Los filtros numericos invalidos se conservan si se pueden convertir a entero; los no numericos y formatos desconocidos quedan NULL.

La bitacora no guarda contrasenas, hashes, tokens, cuerpos, cabeceras, query libre ni registros de victimas. Sin llave foranea porque las cuentas proceden de dos tablas y el historial debe sobrevivir a sus cambios. Sin borrado automatico del historial.

Las respuestas de estas dos rutas se retienen temporalmente en memoria hasta insertar la bitacora, incluyendo serializacion JSON y generacion Excel. Esto agrega un buffer del tamano de la respuesta. Si falla INSERT o vence su limite de cinco segundos, se descarta la respuesta y se devuelve HTTP 503 sin token/datos; el fallo y TraceId quedan en los logs persistentes. La escritura usa un timeout propio aunque el cliente se desconecte. El estado SQL corresponde a la respuesta generada, no certifica que el cliente haya terminado de descargarla.

Validar en desarrollo: login correcto/incorrecto, JSON, XLSX, mes vacio y parametros invalidos; revisar las filas SQL por TraceId y conciliar total_registros. Las pruebas automaticas usan un repositorio simulado para verificar estos casos y la denegacion de token/datos si falla la auditoria; no requieren ni modifican una base real. Ejecutar el script antes de actualizar binarios en produccion.



### Hora de la bitacora

La columna `fecha_hora_mexico` (datetime2) guarda la hora de inicio de la peticion en Ciudad de Mexico. La API convierte UTC con la zona `America/Mexico_City`, sin depender de la zona del sistema operativo. Windows y Linux deben disponer de sus datos de zonas horarias. Las fechas de los JWT siguen en UTC y su vigencia permanece en una hora.

Antes de esta version se guardaba UTC en `fecha_utc`. Para actualizar, detener `api-feminicidios.service`, ejecutar el script entregado por chat que convierte una sola vez las filas existentes (UTC menos seis horas para los registros actuales de 2026) y renombra la columna a `fecha_hora_mexico`; despues instalar la nueva publicacion. Primero validar en desarrollo. Los indices existentes conservan su asociacion a la columna renombrada. No ejecutar API anterior con el esquema nuevo ni API nueva con el anterior. No se ejecutan migraciones automaticas ni se requiere cambiar la zona del servidor. El repositorio de BD lo actualiza el propietario.
