#!/bin/bash
set -euo pipefail
[[ $EUID -eq 0 ]] || { echo 'Ejecutar con sudo.'; exit 1; }
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
APACHE=/opt/lampp/bin/apachectl
SSL=/opt/lampp/etc/extra/httpd-ssl.conf
CONF=/opt/lampp/etc/extra/apache-api-feminicidios.conf
STAMP="$(date +%Y%m%d_%H%M%S)_$$"
BACKUP="/var/backups/api-feminicidios/apache-$STAMP"
[[ -f "$SCRIPT_DIR/apache-api-feminicidios.conf" && -f "$SSL" ]] || { echo 'Falta configuracion Apache.'; exit 1; }
"$APACHE" -t
INCLUDES="$("$APACHE" -t -D DUMP_INCLUDES 2>&1)"
grep -Fq "$SSL" <<< "$INCLUDES" || { echo 'httpd-ssl.conf no esta incluido en Apache. Revisar antes de continuar.'; exit 1; }
MODULES="$("$APACHE" -M)"
for module in proxy_module proxy_http_module headers_module ssl_module; do
    grep -q "$module" <<< "$MODULES" || { echo "Falta modulo $module. No se modifico Apache."; exit 1; }
done
install -d -m 700 "$BACKUP"
cp -a "$SSL" "$BACKUP/httpd-ssl.conf"
HAD_CONF=0
[[ ! -f "$CONF" ]] || { cp -a "$CONF" "$BACKUP/apache-api-feminicidios.conf"; HAD_CONF=1; }
rollback() {
    local code=$?
    trap - ERR
    cp -a "$BACKUP/httpd-ssl.conf" "$SSL"
    if [[ $HAD_CONF -eq 1 ]]; then cp -a "$BACKUP/apache-api-feminicidios.conf" "$CONF"; else rm -f "$CONF"; fi
    "$APACHE" -t && "$APACHE" -k graceful || true
    echo "Se restauro Apache desde $BACKUP"
    exit "$code"
}
trap rollback ERR
install -m 644 "$SCRIPT_DIR/apache-api-feminicidios.conf" "$CONF"
INCLUDE="Include \"$CONF\""
if ! grep -Fxq "$INCLUDE" "$SSL"; then
    printf '\n# API independiente de feminicidios\n%s\n' "$INCLUDE" >> "$SSL"
fi
"$APACHE" -t
"$APACHE" -k graceful
trap - ERR
echo "Proxy configurado. Respaldo: $BACKUP"
echo 'URL: https://cni03.sspc.gob.mx/apiFeminicidios/auth/token'
