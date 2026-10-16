# API de consulta de victimas de feminicidio

API independiente de SIIID, basada en .NET 10. Primero se prueba en desarrollo; no tiene ambiente QA.

## Estado de esta entrega

Preparacion del repositorio y configuracion por ambiente. Todavia no incluye autenticacion, consultas ni exportaciones. No publicar esta plantilla como servicio operativo.

## Desarrollo

Abrir `VictimasFeminicidiosAPI.slnx` con el SDK .NET 10. Se usa la misma conexion local de SIIID: `(localdb)\MSSQLLocalDB`, base `SIIID2`, autenticacion integrada de Windows. No lleva contrasena SQL.

ASP.NET Core carga `appsettings.json`, despues `appsettings.Development.json` y, en desarrollo, User Secrets. Las variables de entorno permiten sobrescribir la configuracion.

Para una conexion local diferente, usar Administrar secretos de usuario en Visual Studio o:

```powershell
dotnet user-secrets set "ConnectionStrings:SiiidDb" "CADENA_LOCAL" --project VictimasFeminicidiosAPI
```

La clave JWT propia se configurara como `Jwt:SecretKey` en User Secrets. Generar 64 bytes aleatorios criptograficos y codificarlos en Base64; usar otra clave para produccion. No reutilizar las claves de SIIID, ni ponerlas en archivos versionados.

## Produccion (posterior a pruebas)

Servidor y base SIIID existentes. Configurar mediante archivo de entorno privado del servicio, fuera del repositorio y accesible solo para el administrador:

- `ASPNETCORE_ENVIRONMENT=Production`
- `ConnectionStrings__SiiidDb`: SQL Server `127.0.0.1,1433`, base `SIIID2`; credenciales propias con permisos minimos.
- `Jwt__SecretKey`: secreto exclusivo de produccion.

No copiar las credenciales de la API SIIID al repositorio. Usar `Encrypt=True` y validar el certificado SQL en produccion; revisar la confianza del certificado del servidor antes del despliegue. La configuracion actual de SIIID usa `TrustServerCertificate=True`, lo cual se debe revisar para este servicio.

Ruta prevista: `https://cni03.sspc.gob.mx/api-feminicidios/`. Servicio y puerto local propios; comprobar disponibilidad al publicar. Todavia no se configura Apache ni systemd.

## Siguiente implementacion

1. Revisar en SIIID la verificacion de contrasenas, usuarios SUPER_USUARIO y condiciones exactas de aprobacion administrativa del consolidado mensual.
2. Entregar el SQL de la tabla de usuarios externos por el chat. El propietario lo ejecuta y lo guarda en el repo de BD.
3. Implementar autenticacion del DIF y superusuarios, JWT de 15 minutos con validacion de firma, algoritmo, emisor, audiencia y vencimiento. Rechazar configuracion faltante o debil al iniciar; no usar claves de respaldo.
4. Proteger todos los endpoints de datos, comprobar cuentas habilitadas, limitar intentos de acceso y registrar operaciones sin contrasenas, tokens ni datos nominales.
5. Implementar una consulta parametrizada compartida por JSON y Excel: anio/mes de corte, consolidado aprobado y aceptado administrativamente, feminicidios consumados. Excluir tentativas, previos, mixtos y Federal.
6. Respetar las 48 columnas con encabezado del Excel de referencia, sin ReferenciaCarga ni columna vacia, con MesCorte en letras. Verificar la cardinalidad victima/delito y comparar resultados con el ejemplo.
7. Probar accesos permitidos/denegados, JWT alterados/vencidos, aprobaciones y equivalencia JSON/Excel antes de publicar.

Solo se modifica este repositorio, sin ramas adicionales y con un commit por entrega. Los repositorios SIIID son referencia de lectura.
