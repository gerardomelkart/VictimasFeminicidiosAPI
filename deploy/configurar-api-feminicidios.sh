#!/bin/bash
set -euo pipefail
[[ $EUID -eq 0 ]] || { echo 'Ejecutar con sudo.'; exit 1; }
python3 - <<'PY'
import os, getpass, secrets, base64
path = '/etc/api-feminicidios.env'
if os.path.exists(path):
    raise SystemExit('Ya existe '+path+'. Se conserva la configuracion y la clave JWT.')
connection = getpass.getpass('Cadena SQL de PRODUCCION (captura oculta): ').strip()
if not connection or '\n' in connection or '\r' in connection:
    raise SystemExit('Cadena vacia o invalida.')
def quote(value):
    return '"'+value.replace('\\','\\\\').replace('"','\\"')+'"'
values = {
    'ASPNETCORE_ENVIRONMENT': 'Production',
    'ASPNETCORE_URLS': 'http://127.0.0.1:5003',
    'ConnectionStrings__SiiidDb': connection,
    'Jwt__SecretKey': base64.b64encode(secrets.token_bytes(64)).decode(),
    'Jwt__MinutesToExpire': '60',
    'Logs__Directory': '/var/log/api-feminicidios'
}
fd=os.open(path, os.O_WRONLY|os.O_CREAT|os.O_EXCL, 0o600)
with os.fdopen(fd,'w') as f:
    f.write(''.join(k+'='+quote(v)+'\n' for k,v in values.items()))
print('Configuracion creada con permisos 600. No compartir su contenido.')
PY
