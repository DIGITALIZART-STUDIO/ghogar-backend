#!/bin/bash

# Script para configurar variables de entorno locales para pruebas
# Uso: ./setup-local-env.sh

echo "🔧 Configurando variables de entorno para desarrollo local..."

cat > .env << 'EOF'
# Archivo de prueba local - GENERADO AUTOMÁTICAMENTE
# NO SUBIR A GIT

# Base de datos
DATABASE_URL=Host=db;Database=gestionhogar;Username=gestionhogar_user;Password=test_password_local_123
POSTGRES_DB=gestionhogar
POSTGRES_USER=gestionhogar_user
POSTGRES_PASSWORD=test_password_local_123

# JWT
JWT_SECRET_KEY=TestSecretKeyForLocalDevelopmentAtLeast32CharactersLong
JWT_ISSUER=http://localhost:8080
JWT_AUDIENCE=http://localhost:8080
JWT_EXPIRATION_SECONDS=144000
JWT_REFRESH_EXPIRATION_SECONDS=4320000

# CORS
CORS_ALLOWED_ORIGIN_1=http://localhost:3000
CORS_ALLOWED_ORIGIN_2=http://localhost:4321
CORS_EXPIRATION_SECONDS=86400
CORS_REFRESH_EXPIRATION_SECONDS=604800
CORS_COOKIE_NAME=gestion_hogar_access_token
CORS_COOKIE_DOMAIN=localhost

# API Peru (usa token de prueba o deja vacío)
API_PERU_TOKEN=
API_PERU_BASE_URL=https://apiperu.dev/api

# Email (CREDENCIALES REALES DE DESARROLLO - NO SUBIR A GIT)
EMAIL_HOST=smtp.gmail.com
EMAIL_PORT=587
EMAIL_USERNAME=gestionhogarsacdesarrollo@gmail.com
EMAIL_PASSWORD=qpnj ywys tssz jzla
EMAIL_ENABLE_SSL=true
EMAIL_USE_DEFAULT_CREDENTIALS=false

# Cloudflare R2 (CREDENCIALES REALES DE DESARROLLO - NO SUBIR A GIT)
CLOUDFLARE_R2_ACCESS_KEY_ID=39a6411bc70fa6aa372f3f53507cf02f
CLOUDFLARE_R2_SECRET_ACCESS_KEY=e0983d5de60e322dc79d6b7275d0a0e034c5f894598a63083160182d117bf213
CLOUDFLARE_R2_ACCOUNT_ID=be74c8d3bc581f3f31171174033ce4f0
CLOUDFLARE_R2_API_S3=https://be74c8d3bc581f3f31171174033ce4f0.r2.cloudflarestorage.com
CLOUDFLARE_R2_BUCKET_NAME=gestion-hogar
CLOUDFLARE_R2_PUBLIC_URL_IMAGE=https://pub-2ec14747057247bba35d3b3cd6bd43a8.r2.dev

# Business Info
BUSINESS_NAME="Gestion Hogar"
BUSINESS_URL=http://localhost:3000
BUSINESS_PHONE="977 759 910"
BUSINESS_ADDRESS="Coop. La Alborada D-3, Cerro Colorado, Arequipa, Peru"
BUSINESS_CONTACT=informes@gestionhogarinmobiliaria.com
BUSINESS_LOGO_URL=https://i.imgur.com/H1Bz9bE.jpeg

# Logging
LOG_LEVEL_DEFAULT=Information
LOG_LEVEL_ASPNETCORE=Warning

# Lead Expiration
LEAD_EXPIRATION_ENABLED=true
LEAD_EXPIRATION_CRON_SCHEDULE=0 0 0,8,16 * * *
LEAD_EXPIRATION_MAX_ERRORS=3
LEAD_EXPIRATION_INITIAL_BACKOFF=30
LEAD_EXPIRATION_MAX_BACKOFF=1440
LEAD_EXPIRATION_MAX_FUTURE_DAYS=30
LEAD_EXPIRATION_MAX_PAST_YEARS=1
LEAD_EXPIRATION_MAX_RECYCLE_COUNT=10
LEAD_EXPIRATION_MIN_CREATION_AGE=1

# Hosts permitidos
ALLOWED_HOSTS=*
EOF

chmod 600 .env

echo "✅ Archivo .env creado con éxito"
echo ""
echo "⚠️  IMPORTANTE: Este archivo contiene credenciales de DESARROLLO."
echo "   Para producción, usa credenciales seguras en Dokploy."
echo ""
echo "🚀 Ahora puedes ejecutar: docker-compose up --build"

