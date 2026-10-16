#!/bin/bash
set -euo pipefail
[[ $EUID -eq 0 ]] || { echo 'Ejecutar con sudo.'; exit 1; }
ZIP_FILE="${1:-/home/opc/publish-feminicidios.zip}"
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
API_DIR=/var/www/api-feminicidios
SERVICE=api-feminicidios.service
ENV_FILE=/etc/api-feminicidios.env
STAMP="$(date +%Y%m%d_%H%M%S)_$$"
BACKUP="/var/backups/api-feminicidios/$STAMP"
STAGE="$(mktemp -d /tmp/api-feminicidios.XXXXXX)"
trap 'rm -rf -- "$STAGE"' EXIT
[[ -f "$ZIP_FILE" && -f "$ENV_FILE" && -f "$SCRIPT_DIR/$SERVICE" ]] || {
    echo 'Falta ZIP, archivo /etc/api-feminicidios.env o plantilla del servicio.'; exit 1;
}
for cmd in python3 systemctl curl ss file; do command -v "$cmd" >/dev/null; done
# No detener otro proceso que pudiera ocupar el puerto elegido.
if ss -ltnH | awk '{print $4}' | grep -Eq ':5003$'; then
    if ! systemctl is-active --quiet "$SERVICE"; then
        echo 'El puerto 5003 esta ocupado por otro servicio. No se realizaron cambios.'; exit 1;
    fi
fi
# Extraer en staging sin permitir rutas fuera del ZIP ni enlaces simbolicos.
python3 - "$ZIP_FILE" "$STAGE" <<'PY'
import sys, zipfile, pathlib, stat
root=pathlib.Path(sys.argv[2]).resolve()
with zipfile.ZipFile(sys.argv[1]) as z:
    for info in z.infolist():
        name=info.filename.replace('\\','/')
        dest=(root/name).resolve()
        if not dest.is_relative_to(root) or stat.S_ISLNK(info.external_attr >> 16):
            raise SystemExit('ZIP con ruta o enlace no permitido.')
        if info.is_dir(): dest.mkdir(parents=True,exist_ok=True)
        else:
            dest.parent.mkdir(parents=True,exist_ok=True)
            with z.open(info) as src, dest.open('wb') as out:
                import shutil
                shutil.copyfileobj(src,out)
PY
mapfile -t BINARIES < <(find "$STAGE" -type f -name VictimasFeminicidiosAPI)
[[ ${#BINARIES[@]} -eq 1 ]] || { echo 'El ZIP debe contener una sola publicacion Linux de la API.'; exit 1; }
SOURCE="$(dirname -- "${BINARIES[0]}")"
file "${BINARIES[0]}" | grep -q 'ELF 64-bit.*x86-64' || { echo 'Publicar para linux-x64, no Windows.'; exit 1; }
[[ -f "$SOURCE/VictimasFeminicidiosAPI.dll" && -f "$SOURCE/libhostpolicy.so" ]] || {
    echo 'Se requiere publicacion AUTOCONTENIDA, sin archivo unico.'; exit 1;
}
[[ -f "$SOURCE/appsettings.Production.json" ]] || { echo 'Falta appsettings.Production.json.'; exit 1; }
# Nunca publicar secretos, logs ni configuracion local.
python3 - "$SOURCE" <<'PY'
import sys,json,pathlib
root=pathlib.Path(sys.argv[1])
for p in root.glob('appsettings*.json'):
    cfg=json.loads(p.read_text(encoding='utf-8-sig'))
    if cfg.get('ConnectionStrings',{}).get('SiiidDb','').strip() and 'Development' not in p.name:
        raise SystemExit('Hay una conexion versionada en '+p.name)
    if cfg.get('Jwt',{}).get('SecretKey','').strip():
        raise SystemExit('Hay una clave JWT en '+p.name)
    if 'Local' in p.name: raise SystemExit('No incluir configuraciones Local en el paquete.')
PY
rm -rf -- "$SOURCE/logs"
rm -f -- "$SOURCE/appsettings.Development.json"
install -d -m 750 -o opc -g opc /var/log/api-feminicidios
install -d -m 700 "$BACKUP"
HAD_APP=0
HAD_SERVICE=0
WAS_ACTIVE=0
WAS_ENABLED=0
[[ ! -d "$API_DIR" ]] || { cp -a "$API_DIR" "$BACKUP/app"; HAD_APP=1; }
[[ ! -f "/etc/systemd/system/$SERVICE" ]] || { cp -a "/etc/systemd/system/$SERVICE" "$BACKUP/$SERVICE"; HAD_SERVICE=1; }
systemctl is-active --quiet "$SERVICE" && WAS_ACTIVE=1
systemctl is-enabled --quiet "$SERVICE" 2>/dev/null && WAS_ENABLED=1
rollback() {
    local code=$?
    trap - ERR
    echo "Fallo la instalacion; restaurando respaldo $BACKUP"
    systemctl stop "$SERVICE" || true
    [[ $WAS_ENABLED -eq 1 ]] || systemctl disable "$SERVICE" 2>/dev/null || true
    rm -rf -- "$API_DIR"
    if [[ $HAD_APP -eq 1 ]]; then cp -a "$BACKUP/app" "$API_DIR"; fi
    if [[ $HAD_SERVICE -eq 1 ]]; then cp -a "$BACKUP/$SERVICE" "/etc/systemd/system/$SERVICE";
    else rm -f "/etc/systemd/system/$SERVICE"; fi
    systemctl daemon-reload
    [[ $WAS_ENABLED -eq 1 ]] || systemctl disable "$SERVICE" 2>/dev/null || true
    [[ $WAS_ACTIVE -eq 0 ]] || systemctl start "$SERVICE" || true
    exit "$code"
}
trap rollback ERR
if [[ $HAD_SERVICE -eq 1 || $WAS_ACTIVE -eq 1 ]]; then systemctl stop "$SERVICE"; fi
rm -rf -- "$API_DIR"
install -d -m 755 "$API_DIR"
cp -a "$SOURCE/." "$API_DIR/"
chown -R root:opc "$API_DIR"
find "$API_DIR" -type d -exec chmod 755 {} +
find "$API_DIR" -type f -exec chmod 644 {} +
chmod 755 "$API_DIR/VictimasFeminicidiosAPI"
# Igual que SIIID, conservar SELinux habilitado y etiquetar el ejecutable.
if command -v getenforce >/dev/null && [[ $(getenforce) != Disabled ]]; then
    command -v semanage >/dev/null
    semanage fcontext -a -t bin_t "$API_DIR(/.*)?" 2>/dev/null || semanage fcontext -m -t bin_t "$API_DIR(/.*)?"
    restorecon -R "$API_DIR"
fi
chmod 600 "$ENV_FILE"
chown root:root "$ENV_FILE"
install -m 644 "$SCRIPT_DIR/$SERVICE" "/etc/systemd/system/$SERVICE"
systemctl daemon-reload
systemctl enable "$SERVICE"
systemctl restart "$SERVICE"
READY=0
for attempt in {1..20}; do
    STATUS="$(curl -s -o /dev/null -w '%{http_code}' --max-time 2 \
        -H 'Host: cni03.sspc.gob.mx' -H 'X-Forwarded-Proto: https' -H 'X-Forwarded-For: 127.0.0.1' \
        http://127.0.0.1:5003/apiFeminicidios/auth/me || true)"
    if [[ $STATUS == 401 ]]; then READY=1; break; fi
    sleep 1
done
[[ $READY -eq 1 ]] || { echo 'La API no respondio 401 sin token. Consultar journalctl -u api-feminicidios.service.'; false; }
trap - ERR
echo "API instalada en 5003; respaldo: $BACKUP"
echo 'Logs: /var/log/api-feminicidios/'
echo 'El instalador NO modifica Apache ni ejecuta SQL.'
echo 'Primera publicacion: configurar Apache segun deploy/README.md.'
