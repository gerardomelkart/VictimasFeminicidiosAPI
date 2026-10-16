# Publicacion en el servidor SIIID (LAMPP)

Solo produccion, sin QA. Preparar primero el paquete y ejecutar los pasos en este orden. Ningun script ejecuta SQL, toca las carpetas de SIIID/DDCP ni reinicia sus servicios. La configuracion del proxy agrega una ruta exclusiva y recarga Apache de manera gradual.

## 1. Publicar desde Visual Studio

Proyecto a publicar: `VictimasFeminicidiosAPI` (no el proyecto RegressionTests).

- Configuracion: Release.
- Destino: carpeta.
- Runtime: linux-x64.
- Modo: autocontenido (self-contained).
- Archivo unico: desactivado.
- Recorte/trimming: desactivado.

Comprimir los archivos de la carpeta publish como `publish-feminicidios.zip`. Se acepta que esten en la raiz del ZIP o dentro de una carpeta publish. No incluir el codigo fuente ni pruebas. No introducir conexiones ni clave JWT en appsettings; se configuraran en el servidor.

## 2. Subir archivos

Desde la carpeta donde esta el ZIP:

```powershell
scp .\publish-feminicidios.zip siiid:~
```

Desde la raiz del repositorio, subir las plantillas y scripts (primera vez y cuando cambien):

```powershell
scp .\deploy\*.sh .\deploy\api-feminicidios.service .\deploy\apache-api-feminicidios.conf siiid:~
ssh siiid
```

## 3. Primera instalacion

En produccion ejecutar por cuenta del propietario el SQL de `api_feminicidios_usuario` que se entrego en el chat. No copiar la base de desarrollo ni sus credenciales.

```bash
sudo bash ./configurar-api-feminicidios.sh
```

Solicita la cadena de conexion SQL de produccion sin mostrarla y genera una clave JWT aleatoria propia. Destino SQL: `127.0.0.1,1433`, base `SIIID2`. Elegir credenciales autorizadas y permisos minimos. No enviar la cadena al chat. Para TLS usar `Encrypt=True` con certificado confiable; cualquier excepcion de confianza del certificado debe resolverse expresamente en el servidor. No se copian automaticamente credenciales de SIIID.

Crea `/etc/api-feminicidios.env`, propiedad root y modo 600. Si ya existe, lo conserva sin regenerar la clave. No compartir el contenido. Cualquier cambio posterior se realiza mediante `sudoedit /etc/api-feminicidios.env` y reinicio exclusivo de esta API.

```bash
sudo bash ./install-api-feminicidios.sh
sudo bash ./configurar-proxy-api-feminicidios.sh
```

El primer script instala el paquete/servicio y verifica que el endpoint protegido responda 401 sin token (arranque HTTP; no verifica la BD). El segundo verifica que el SSL de LAMPP sea un include activo, comprueba modulos, respalda la configuracion, agrega el include una sola vez, valida sintaxis y recarga Apache. Si falla sintaxis/recarga restaura su respaldo.

Publico: `https://cni03.sspc.gob.mx/apiFeminicidios/`. Puerto interno: `127.0.0.1:5003`, sin abrirlo en el firewall.

Si SELinux bloquea la conexion Apache al backend, revisar el AVC; el servidor ya tiene proxys a 5000/5001/5002. No deshabilitar SELinux. El instalador etiqueta el ejecutable como lo hace el instalador de SIIID.

## 4. Validar

```bash
sudo systemctl status api-feminicidios.service --no-pager -l
sudo journalctl -u api-feminicidios.service -n 40 --no-pager
sudo tail -n 30 /var/log/api-feminicidios/api-feminicidios-$(date +%Y%m%d).log
```

Desde Postman solicitar un token de superusuario SIIID en la URL publica, crear la cuenta DIF en produccion y probar JSON/Excel con sus credenciales. Las cuentas creadas en LocalDB no existen automaticamente en produccion. Confirmar que `/beta/`, `/qa-api/` y `/ddcp/api/` siguen respondiendo normalmente.

## 5. Publicaciones siguientes

```powershell
scp .\publish-feminicidios.zip siiid:~
ssh siiid
```

```bash
sudo bash ./install-api-feminicidios.sh
```

No repetir la configuracion de secretos/proxy salvo que haya cambios. El instalador conserva un respaldo en `/var/backups/api-feminicidios/` y restaura la aplicacion anterior si falla su arranque. Logs en `/var/log/api-feminicidios/` y configuracion en `/etc/api-feminicidios.env` no se reemplazan con el ZIP. Los respaldos no se borran automaticamente.

## Proxy de confianza

Apache exige TLS en esta ruta, preserva `/apiFeminicidios/` y el Host, reemplaza X-Forwarded-For/Proto de esta ruta y envia la IP de la conexion observada y HTTPS. La API acepta un salto unicamente desde 127.0.0.1/::1; no confia en cabeceras de proxys externos. Esto permite redireccion HTTPS y limite de login por IP sin confiar en valores aportados por el consumidor. Si hay un balanceador adicional, revisar su cadena de confianza antes de utilizar su IP declarada.

## Defines de LAMPP y saltos de linea

El servidor arranca Apache con `-DSSL -DPHP`. El script de proxy usa ambos defines en todas sus comprobaciones, recarga y recuperacion, para conservar el mismo conjunto de includes activos. `.gitattributes` fija LF en scripts, unidades y configuraciones incluso al clonar en Windows. Si ya se copiaron archivos CRLF, ejecutar `sed -i 's/\r$//' configurar-proxy-api-feminicidios.sh` en el servidor antes de volver a ejecutarlo. Esta correccion solo requiere subir nuevamente ese script; no recompilar ni reinstalar la API.
