#!/usr/bin/env bash
# ==============================================================================
# Tenis Ahora (IEEE SRS-830) — Script de Despliegue Automatizado en VPS Ubuntu
# Stack: Frontend React (SPA) + Nginx Reverse Proxy + Backend C# .NET 8 (Docker)
#        + Supabase PostgreSQL / Auth + Mercado Pago SDK + Certbot SSL
# ==============================================================================
# Uso en la VPS (Ubuntu 22.04 / 24.04 LTS):
#   chmod +x deploy.sh
#   sudo DOMAIN="tenisahora.club" ADMIN_EMAIL="admin@tenisahora.club" ./deploy.sh
# ==============================================================================

set -euo pipefail

# ------------------------------------------------------------------------------
# PASO 1: Variables de Configuración y Pre-chequeos del Sistema
# ------------------------------------------------------------------------------
DOMAIN="${DOMAIN:-tenisahora.club}"
ADMIN_EMAIL="${ADMIN_EMAIL:-admin@tenisahora.club}"
API_PORT="${API_PORT:-5090}"
CONTAINER_NAME="${CONTAINER_NAME:-tenis-ahora-api}"
IMAGE_NAME="${IMAGE_NAME:-tenis-ahora-api:latest}"
APP_DIR="${APP_DIR:-/opt/tenis-ahora}"
BACKEND_DIR="${BACKEND_DIR:-$(pwd)}"
FRONTEND_DIST_DIR="${FRONTEND_DIST_DIR:-/var/www/tenis-ahora/dist}"
ENV_FILE="${ENV_FILE:-/etc/tenis-ahora/api.env}"
ENABLE_SSL="${ENABLE_SSL:-true}"

echo "=============================================================================="
echo " [Tenis Ahora] Iniciando despliegue de producción para dominio: ${DOMAIN}"
echo "=============================================================================="

if [[ "${EUID}" -ne 0 ]]; then
  echo "[ERROR] Este script debe ejecutarse con privilegios root (sudo)." >&2
  exit 1
fi

mkdir -p "$(dirname "${ENV_FILE}")"
mkdir -p "${FRONTEND_DIST_DIR}"

if [[ ! -f "${ENV_FILE}" ]]; then
  cat <<'EOF' > "${ENV_FILE}"
# ==============================================================================
# Variables de Entorno de Producción — TenisAhora.Api (.NET 8)
# Complete los secretos reales de Supabase y Mercado Pago antes de exponer al público
# ==============================================================================
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:5090

# Conexión a Supabase PostgreSQL (Esquema de 34 tablas con herencia TPT)
ConnectionStrings__SupabasePostgres=Host=db.xxxx.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=CHANGE_ME;SSL Mode=Require;Trust Server Certificate=true

# Configuración de Validación JWT de Supabase Auth
Supabase__Url=https://xxxx.supabase.co
Supabase__Issuer=https://xxxx.supabase.co/auth/v1
Supabase__Audience=authenticated
Supabase__JwtSecret=CHANGE_ME_SUPABASE_JWT_SECRET_MIN_32_CHARS

# Credenciales de Pasarela de Pagos Mercado Pago (Seña Obligatoria 50% — RD01)
MercadoPago__AccessToken=APP_USR-CHANGE_ME
MercadoPago__PublicKey=APP_USR-CHANGE_ME
MercadoPago__WebhookSecret=CHANGE_ME
MercadoPago__NotificationUrl=https://tenisahora.club/api/webhooks/mercadopago
MercadoPago__SuccessBackUrl=https://tenisahora.club/reservar
MercadoPago__PendingBackUrl=https://tenisahora.club/reservar
MercadoPago__FailureBackUrl=https://tenisahora.club/reservar

# Orígenes CORS permitidos
Cors__AllowedOrigins__0=https://tenisahora.club
Cors__AllowedOrigins__1=https://www.tenisahora.club
EOF
  chmod 600 "${ENV_FILE}"
  echo "[INFO] Se creó plantilla de entorno en ${ENV_FILE}. Verifique las credenciales de producción."
fi

# ------------------------------------------------------------------------------
# PASO 2: Instalación de Docker Engine, Docker Compose Plugin y Firewall UFW
# ------------------------------------------------------------------------------
echo "------------------------------------------------------------------------------"
echo " [Paso 2/5] Verificando e instalando Docker Engine y UFW..."
echo "------------------------------------------------------------------------------"

apt-get update -y
apt-get install -y ca-certificates curl gnupg lsb-release ufw

if ! command -v docker >/dev/null 2>&1; then
  echo "[INFO] Instalando Docker Engine oficial desde repositorio APT de Docker..."
  install -m 0755 -d /etc/apt/keyrings
  curl -fsSL https://download.docker.com/linux/ubuntu/gpg | gpg --dearmor --yes -o /etc/apt/keyrings/docker.gpg
  chmod a+r /etc/apt/keyrings/docker.gpg

  echo \
    "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu \
    $(. /etc/os-release && echo "${VERSION_CODENAME}") stable" \
    > /etc/apt/sources.list.d/docker.list

  apt-get update -y
  apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
fi

systemctl enable --now docker

# Configurar reglas básicas de firewall (SSH + HTTP/HTTPS para Nginx)
ufw allow OpenSSH >/dev/null 2>&1 || true
ufw allow 80/tcp >/dev/null 2>&1 || true
ufw allow 443/tcp >/dev/null 2>&1 || true
ufw --force enable >/dev/null 2>&1 || true

# ------------------------------------------------------------------------------
# PASO 3: Build y Ejecución del Contenedor Docker de la API en C# (.NET 8)
# ------------------------------------------------------------------------------
echo "------------------------------------------------------------------------------"
echo " [Paso 3/5] Compilando imagen Docker (${IMAGE_NAME}) y levantando contenedor..."
echo "------------------------------------------------------------------------------"

docker build -t "${IMAGE_NAME}" -f "${BACKEND_DIR}/Dockerfile" "${BACKEND_DIR}"

if docker ps -a --format '{{.Names}}' | grep -Eq "^${CONTAINER_NAME}\$"; then
  echo "[INFO] Deteniendo y reemplazando contenedor previo: ${CONTAINER_NAME}..."
  docker stop "${CONTAINER_NAME}" >/dev/null 2>&1 || true
  docker rm "${CONTAINER_NAME}" >/dev/null 2>&1 || true
fi

# Ejecutar contenedor escuchando únicamente en loopback (127.0.0.1:5090)
# para que todo el tráfico externo pase obligatoriamente por Nginx (TLS/SSL)
docker run -d \
  --name "${CONTAINER_NAME}" \
  --restart unless-stopped \
  --env-file "${ENV_FILE}" \
  -p "127.0.0.1:${API_PORT}:5090" \
  --memory="512m" \
  --cpus="1.0" \
  "${IMAGE_NAME}"

echo "[INFO] Esperando a que TenisAhora.Api (.NET 8) responda en http://127.0.0.1:${API_PORT}/api/health ..."
for attempt in $(seq 1 20); do
  if curl -fsS "http://127.0.0.1:${API_PORT}/api/health" >/dev/null 2>&1; then
    echo "[OK] TenisAhora.Api operativa en el intento ${attempt}."
    break
  fi
  if [[ "${attempt}" -eq 20 ]]; then
    echo "[WARN] El endpoint /api/health aún no respondió tras 20 intentos. Revise: docker logs ${CONTAINER_NAME}"
  fi
  sleep 2
done

# Limpiar capas huérfanas para cuidar el espacio en la VPS
docker image prune -f >/dev/null 2>&1 || true

# ------------------------------------------------------------------------------
# PASO 4: Configuración de Nginx como Proxy Inverso (/api -> :5090) y Servidor SPA
# ------------------------------------------------------------------------------
echo "------------------------------------------------------------------------------"
echo " [Paso 4/5] Configurando Nginx como Reverse Proxy y servidor SPA..."
echo "------------------------------------------------------------------------------"

apt-get install -y nginx
systemctl enable --now nginx

NGINX_CONF="/etc/nginx/sites-available/tenis-ahora.conf"

cat <<EOF > "${NGINX_CONF}"
# ==============================================================================
# Nginx Reverse Proxy & SPA Server — Club Tenis Ahora (IEEE SRS-830)
# ==============================================================================
server {
    listen 80;
    listen [::]:80;
    server_name ${DOMAIN} www.${DOMAIN};

    # Directorio del build de producción de React (vite build -> dist/)
    root ${FRONTEND_DIST_DIR};
    index index.html;

    # Límite de 10 MB para soportar carga de reglamentos y certificados PDF/JPG/PNG (máx. 5 MB en Base64)
    client_max_body_size 10M;

    # Compresión Gzip para activos estáticos y respuestas JSON
    gzip on;
    gzip_vary on;
    gzip_min_length 1024;
    gzip_types text/plain text/css application/json application/javascript text/xml application/xml text/javascript image/svg+xml;

    # Cabeceras de endurecimiento de seguridad
    add_header X-Frame-Options "SAMEORIGIN" always;
    add_header X-Content-Type-Options "nosniff" always;
    add_header Referrer-Policy "strict-origin-when-cross-origin" always;

    # Proxy inverso hacia el contenedor Docker de la API en C# (.NET 8)
    location /api/ {
        proxy_pass http://127.0.0.1:${API_PORT}/api/;
        proxy_http_version 1.1;
        proxy_set_header Upgrade \$http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host \$host;
        proxy_set_header X-Real-IP \$remote_addr;
        proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto \$scheme;
        proxy_set_header Authorization \$http_authorization;
        proxy_pass_header Authorization;
        proxy_cache_bypass \$http_upgrade;
        proxy_read_timeout 60s;
        proxy_connect_timeout 15s;
    }

    # Soporte para rutas de React Router (SPA)
    location / {
        try_files \$uri \$uri/ /index.html;
    }

    # Caché de activos estáticos versionados por Vite
    location ~* \.(?:js|css|woff2?|ttf|eot|ico|png|jpg|jpeg|gif|svg|webp)\$ {
        expires 30d;
        add_header Cache-Control "public, immutable";
        access_log off;
    }
}
EOF

ln -sf "${NGINX_CONF}" /etc/nginx/sites-enabled/tenis-ahora.conf
rm -f /etc/nginx/sites-enabled/default

nginx -t
systemctl reload nginx
echo "[OK] Configuración de Nginx validada y recargada."

# ------------------------------------------------------------------------------
# PASO 5: Certificados SSL Gratuitos con Certbot (Let's Encrypt) y Auto-Renovación
# ------------------------------------------------------------------------------
echo "------------------------------------------------------------------------------"
echo " [Paso 5/5] Configurando SSL/TLS gratuito con Certbot (Let's Encrypt)..."
echo "------------------------------------------------------------------------------"

apt-get install -y certbot python3-certbot-nginx

if [[ "${ENABLE_SSL}" == "true" && "${DOMAIN}" != "localhost" ]]; then
  if certbot --nginx \
      -d "${DOMAIN}" \
      --non-interactive \
      --agree-tos \
      -m "${ADMIN_EMAIL}" \
      --redirect; then
    echo "[OK] Certificado SSL emitido y redirección HTTP -> HTTPS activada para ${DOMAIN}."
  else
    echo "[WARN] Certbot no pudo emitir el certificado (verifique que el registro DNS A de ${DOMAIN} apunte a la IP de esta VPS)."
  fi
fi

systemctl enable --now certbot.timer >/dev/null 2>&1 || true

echo "=============================================================================="
echo " [Tenis Ahora] Despliegue finalizado exitosamente."
echo " - API .NET 8 (Docker): http://127.0.0.1:${API_PORT}/api/health"
echo " - Portal Público HTTPS: https://${DOMAIN}"
echo "=============================================================================="
