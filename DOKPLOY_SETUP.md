# 🚀 Configuración de Variables en Dokploy

## 📋 Resumen Rápido

Este proyecto usa **Docker Compose** con **50 variables de entorno**. Todas las credenciales se configuran en **Dokploy UI** (no en el código).

---

## 🔐 Variables por Ambiente

### 🟢 **DESARROLLO (Dev Environment)**

Variables para ambiente de desarrollo/staging:

```bash
# Base de datos
DATABASE_URL=Host=db;Database=gestionhogar;Username=gestionhogar_dev;Password=dev_password_seguro
POSTGRES_DB=gestionhogar
POSTGRES_USER=gestionhogar_dev
POSTGRES_PASSWORD=dev_password_seguro

# JWT
JWT_SECRET_KEY=tu_clave_jwt_desarrollo_32_caracteres_minimo
JWT_ISSUER=https://api-dev.tu-dominio.com
JWT_AUDIENCE=https://api-dev.tu-dominio.com
JWT_EXPIRATION_SECONDS=144000
JWT_REFRESH_EXPIRATION_SECONDS=4320000

# CORS
CORS_ALLOWED_ORIGIN_1=https://dev.tu-frontend.com
CORS_ALLOWED_ORIGIN_2=https://staging.tu-landing.com
CORS_EXPIRATION_SECONDS=86400
CORS_REFRESH_EXPIRATION_SECONDS=604800
CORS_COOKIE_NAME=gestion_hogar_access_token
CORS_COOKIE_DOMAIN=.tu-dominio.com

# API Peru
API_PERU_TOKEN=tu_token_de_desarrollo
API_PERU_BASE_URL=https://apiperu.dev/api

# Email
EMAIL_HOST=smtp.gmail.com
EMAIL_PORT=587
EMAIL_USERNAME=desarrollo@tu-empresa.com
EMAIL_PASSWORD=tu_app_password_dev
EMAIL_ENABLE_SSL=true
EMAIL_USE_DEFAULT_CREDENTIALS=false

# Cloudflare R2
CLOUDFLARE_R2_ACCESS_KEY_ID=tu_access_key_dev
CLOUDFLARE_R2_SECRET_ACCESS_KEY=tu_secret_key_dev
CLOUDFLARE_R2_ACCOUNT_ID=tu_account_id
CLOUDFLARE_R2_API_S3=https://tu_account_id.r2.cloudflarestorage.com
CLOUDFLARE_R2_BUCKET_NAME=gestion-hogar-dev
CLOUDFLARE_R2_PUBLIC_URL_IMAGE=https://pub-dev.r2.dev

# Business Info
BUSINESS_NAME="Gestion Hogar"
BUSINESS_URL=https://dev.tu-frontend.com
BUSINESS_PHONE="977 759 910"
BUSINESS_ADDRESS="Tu dirección"
BUSINESS_CONTACT=info@tu-empresa.com
BUSINESS_LOGO_URL=https://tu-logo-url.com/logo.png

# Logging
LOG_LEVEL_DEFAULT=Debug
LOG_LEVEL_ASPNETCORE=Information

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

# Hosts
ALLOWED_HOSTS=*
```

---

### 🔴 **PRODUCCIÓN (Production Environment)**

Variables para ambiente de producción:

```bash
# Base de datos
DATABASE_URL=Host=db;Database=gestionhogar;Username=gestionhogar_prod;Password=PRODUCCION_PASSWORD_MUY_SEGURO
POSTGRES_DB=gestionhogar
POSTGRES_USER=gestionhogar_prod
POSTGRES_PASSWORD=PRODUCCION_PASSWORD_MUY_SEGURO

# JWT
JWT_SECRET_KEY=PRODUCCION_CLAVE_JWT_MUY_SEGURA_Y_DIFERENTE_DE_DEV
JWT_ISSUER=https://api.tu-dominio.com
JWT_AUDIENCE=https://api.tu-dominio.com
JWT_EXPIRATION_SECONDS=86400
JWT_REFRESH_EXPIRATION_SECONDS=604800

# CORS
CORS_ALLOWED_ORIGIN_1=https://tu-frontend.com
CORS_ALLOWED_ORIGIN_2=https://tu-landing-page.com
CORS_EXPIRATION_SECONDS=86400
CORS_REFRESH_EXPIRATION_SECONDS=604800
CORS_COOKIE_NAME=gestion_hogar_access_token
CORS_COOKIE_DOMAIN=.tu-dominio.com

# API Peru
API_PERU_TOKEN=tu_token_de_produccion
API_PERU_BASE_URL=https://apiperu.dev/api

# Email
EMAIL_HOST=smtp.gmail.com
EMAIL_PORT=587
EMAIL_USERNAME=produccion@tu-empresa.com
EMAIL_PASSWORD=PRODUCCION_APP_PASSWORD
EMAIL_ENABLE_SSL=true
EMAIL_USE_DEFAULT_CREDENTIALS=false

# Cloudflare R2
CLOUDFLARE_R2_ACCESS_KEY_ID=PRODUCCION_ACCESS_KEY
CLOUDFLARE_R2_SECRET_ACCESS_KEY=PRODUCCION_SECRET_KEY
CLOUDFLARE_R2_ACCOUNT_ID=tu_account_id
CLOUDFLARE_R2_API_S3=https://tu_account_id.r2.cloudflarestorage.com
CLOUDFLARE_R2_BUCKET_NAME=gestion-hogar-prod
CLOUDFLARE_R2_PUBLIC_URL_IMAGE=https://pub-prod.r2.dev

# Business Info
BUSINESS_NAME="Gestion Hogar"
BUSINESS_URL=https://tu-frontend.com
BUSINESS_PHONE="977 759 910"
BUSINESS_ADDRESS="Tu dirección"
BUSINESS_CONTACT=info@tu-empresa.com
BUSINESS_LOGO_URL=https://tu-logo-url.com/logo.png

# Logging
LOG_LEVEL_DEFAULT=Warning
LOG_LEVEL_ASPNETCORE=Error

# Lead Expiration (igual que dev)
LEAD_EXPIRATION_ENABLED=true
LEAD_EXPIRATION_CRON_SCHEDULE=0 0 0,8,16 * * *
LEAD_EXPIRATION_MAX_ERRORS=3
LEAD_EXPIRATION_INITIAL_BACKOFF=30
LEAD_EXPIRATION_MAX_BACKOFF=1440
LEAD_EXPIRATION_MAX_FUTURE_DAYS=30
LEAD_EXPIRATION_MAX_PAST_YEARS=1
LEAD_EXPIRATION_MAX_RECYCLE_COUNT=10
LEAD_EXPIRATION_MIN_CREATION_AGE=1

# Hosts
ALLOWED_HOSTS=*
```

---

## 🎯 Diferencias Clave por Ambiente

| Variable | Desarrollo | Producción |
|----------|------------|------------|
| `DATABASE_URL` | Usuario/Pass de dev | Usuario/Pass diferentes y seguros |
| `JWT_SECRET_KEY` | Clave dev | **Clave diferente y MÁS segura** |
| `CORS_ALLOWED_ORIGIN_1` | URL dev | URL producción |
| `EMAIL_PASSWORD` | App password dev | App password prod (diferente) |
| `CLOUDFLARE_R2_BUCKET_NAME` | `gestion-hogar-dev` | `gestion-hogar-prod` |
| `LOG_LEVEL_DEFAULT` | `Debug` | `Warning` |
| `BUSINESS_URL` | URL dev | URL producción |

---

## 🔐 Variables CRÍTICAS que DEBEN ser diferentes

**NUNCA uses las mismas credenciales en dev y producción:**

1. ✅ `JWT_SECRET_KEY` - Clave diferente en cada ambiente
2. ✅ `DATABASE_URL` - Passwords diferentes
3. ✅ `EMAIL_PASSWORD` - App passwords diferentes
4. ✅ `CLOUDFLARE_R2_SECRET_ACCESS_KEY` - Keys diferentes
5. ✅ `API_PERU_TOKEN` - Tokens diferentes

---

## 🔧 Niveles de Variables en Dokploy

### 📚 Sintaxis Especial de Dokploy

Dokploy tiene **3 niveles de variables** con sintaxis especial:

- **🔵 Project-level:** `${{project.VARIABLE_NAME}}`
- **🟡 Environment-level:** `${{environment.VARIABLE_NAME}}`
- **🟢 Service-level:** `${VARIABLE_NAME}` o `${{service.VARIABLE_NAME}}`

### 🎯 Estrategia para Nuestro Proyecto

#### **🔵 Project-Level (Compartidas - TODOS los servicios)**

Variables que podrían usar backend Y frontend:

```bash
# Información del negocio (compartida)
BUSINESS_NAME=Gestion Hogar
BUSINESS_PHONE=977 759 910
BUSINESS_ADDRESS="Coop. La Alborada D-3, Cerro Colorado, Arequipa, Peru"
BUSINESS_CONTACT=informes@gestionhogarinmobiliaria.com
BUSINESS_LOGO_URL=https://i.imgur.com/H1Bz9bE.jpeg

# Configuración general
ALLOWED_HOSTS=*
```

#### **🟡 Environment-Level (Por ambiente dev/prod)**

Variables que CAMBIAN por ambiente:

```bash
# Base de datos (diferente por ambiente)
DATABASE_URL=Host=db;Database=gestionhogar;Username=user_dev;Password=pass_dev
POSTGRES_DB=gestionhogar
POSTGRES_USER=user_dev
POSTGRES_PASSWORD=pass_dev

# Seguridad (DIFERENTE por ambiente)
JWT_SECRET_KEY=clave_segura_dev_min_32_caracteres
JWT_ISSUER=https://api-dev.tu-dominio.com
JWT_AUDIENCE=https://api-dev.tu-dominio.com

# CORS (URLs diferentes por ambiente)
CORS_ALLOWED_ORIGIN_1=https://dev.tu-frontend.com
CORS_ALLOWED_ORIGIN_2=https://staging.tu-landing.com

# APIs externas (tokens diferentes por ambiente)
API_PERU_TOKEN=token_desarrollo

# Email (credenciales diferentes por ambiente)
EMAIL_USERNAME=desarrollo@tu-empresa.com
EMAIL_PASSWORD=app_password_dev

# Cloudflare (buckets diferentes por ambiente)
CLOUDFLARE_R2_ACCESS_KEY_ID=key_dev
CLOUDFLARE_R2_SECRET_ACCESS_KEY=secret_dev
CLOUDFLARE_R2_BUCKET_NAME=gestion-hogar-dev
CLOUDFLARE_R2_PUBLIC_URL_IMAGE=https://pub-dev.r2.dev

# URLs específicas del ambiente
BUSINESS_URL=https://dev.tu-frontend.com
```

#### **🟢 Service-Level (Específicas del backend)**

Variables solo para el servicio backend:

```bash
# Logging (solo backend)
LOG_LEVEL_DEFAULT=Debug
LOG_LEVEL_ASPNETCORE=Information

# Lead Expiration (solo backend)
LEAD_EXPIRATION_ENABLED=true
LEAD_EXPIRATION_CRON_SCHEDULE=0 0 0,8,16 * * *
LEAD_EXPIRATION_MAX_ERRORS=3
LEAD_EXPIRATION_INITIAL_BACKOFF=30
LEAD_EXPIRATION_MAX_BACKOFF=1440
LEAD_EXPIRATION_MAX_FUTURE_DAYS=30
LEAD_EXPIRATION_MAX_PAST_YEARS=1
LEAD_EXPIRATION_MAX_RECYCLE_COUNT=10
LEAD_EXPIRATION_MIN_CREATION_AGE=1

# JWT timing (solo backend)
JWT_EXPIRATION_SECONDS=144000
JWT_REFRESH_EXPIRATION_SECONDS=4320000

# CORS timing (solo backend)
CORS_EXPIRATION_SECONDS=86400
CORS_REFRESH_EXPIRATION_SECONDS=604800
CORS_COOKIE_NAME=gestion_hogar_access_token
CORS_COOKIE_DOMAIN=.tu-dominio.com

# Email config (solo backend)
EMAIL_HOST=smtp.gmail.com
EMAIL_PORT=587
EMAIL_ENABLE_SSL=true
EMAIL_USE_DEFAULT_CREDENTIALS=false

# Cloudflare config (solo backend)
CLOUDFLARE_R2_ACCOUNT_ID=tu_account_id
CLOUDFLARE_R2_API_S3=https://tu_account_id.r2.cloudflarestorage.com

# API Peru (solo backend)
API_PERU_BASE_URL=https://apiperu.dev/api
```

---

## 📝 Cómo Configurar en Dokploy

### **Opción 1: SIN Sintaxis Especial (Recomendado)**

Mantén tu `docker-compose.yml` actual con `${VARIABLE_NAME}`. Dokploy resuelve automáticamente:

#### **Environment Variables (Resuelve: Environment > Service > Project)**
```
Environment (development) → Environment Variables
- DATABASE_URL=Host=db;Database=gestionhogar;Username=user_dev;Password=pass_dev
- JWT_SECRET_KEY=clave_segura_dev
- CORS_ALLOWED_ORIGIN_1=https://dev.tu-frontend.com
- EMAIL_PASSWORD=app_password_dev
# ... TODAS las variables de environment
```

### **Opción 2: CON Sintaxis Especial**

Si quieres ser explícito, modifica `docker-compose.yml`:

```yaml
environment:
  # Project variables
  - BUSINESS_NAME=${{project.BUSINESS_NAME}}
  - ALLOWED_HOSTS=${{project.ALLOWED_HOSTS}}

  # Environment variables
  - DATABASE_URL=${{environment.DATABASE_URL}}
  - JWT_SECRET_KEY=${{environment.JWT_SECRET_KEY}}
  - CORS_ALLOWED_ORIGIN_1=${{environment.CORS_ALLOWED_ORIGIN_1}}

  # Service variables
  - LOG_LEVEL_DEFAULT=${LOG_LEVEL_DEFAULT}
  - LEAD_EXPIRATION_ENABLED=${LEAD_EXPIRATION_ENABLED}
```

### Paso 1: Crear Proyecto
1. Dokploy UI → **Create Project**
2. Nombre: `GestionHogar`

### Paso 2: Configurar Variables Compartidas (Project)
```
Project Settings → Environment Variables → Add Variable
- BUSINESS_NAME = Gestion Hogar
- BUSINESS_PHONE = 977 759 910
- BUSINESS_ADDRESS = "Coop. La Alborada D-3, Cerro Colorado, Arequipa, Peru"
- BUSINESS_CONTACT = informes@gestionhogarinmobiliaria.com
- BUSINESS_LOGO_URL = https://i.imgur.com/H1Bz9bE.jpeg
- ALLOWED_HOSTS = *
```

### Paso 3: Crear Servicio Backend
1. **Add Service** → Docker Compose
2. Repository: `github.com/DIGITALIZART-STUDIO/ghogar-backend`
3. Branch: `main`
4. Docker Compose Path: `./docker-compose.yml`

### Paso 4: Configurar Variables de Servicio (opcional)
```
Service (backend) → Environment Variables
- LOG_LEVEL_DEFAULT = Debug
- LOG_LEVEL_ASPNETCORE = Information
```

### Paso 5: Crear Environments

#### **Environment: Development**
```
Environment (development) → Environment Variables → Add Variable
- DATABASE_URL = Host=db;Database=gestionhogar;Username=user_dev;Password=pass_dev
- POSTGRES_DB = gestionhogar
- POSTGRES_USER = user_dev
- POSTGRES_PASSWORD = pass_dev
- JWT_SECRET_KEY = clave_segura_dev_min_32_caracteres
- JWT_ISSUER = https://api-dev.tu-dominio.com
- JWT_AUDIENCE = https://api-dev.tu-dominio.com
- CORS_ALLOWED_ORIGIN_1 = https://dev.tu-frontend.com
- CORS_ALLOWED_ORIGIN_2 = https://staging.tu-landing.com
- API_PERU_TOKEN = token_desarrollo
- EMAIL_USERNAME = desarrollo@tu-empresa.com
- EMAIL_PASSWORD = app_password_dev
- CLOUDFLARE_R2_ACCESS_KEY_ID = key_dev
- CLOUDFLARE_R2_SECRET_ACCESS_KEY = secret_dev
- CLOUDFLARE_R2_BUCKET_NAME = gestion-hogar-dev
- CLOUDFLARE_R2_PUBLIC_URL_IMAGE = https://pub-dev.r2.dev
- BUSINESS_URL = https://dev.tu-frontend.com
- JWT_EXPIRATION_SECONDS = 144000
- JWT_REFRESH_EXPIRATION_SECONDS = 4320000
- CORS_EXPIRATION_SECONDS = 86400
- CORS_REFRESH_EXPIRATION_SECONDS = 604800
- CORS_COOKIE_NAME = gestion_hogar_access_token
- CORS_COOKIE_DOMAIN = .tu-dominio.com
- EMAIL_HOST = smtp.gmail.com
- EMAIL_PORT = 587
- EMAIL_ENABLE_SSL = true
- EMAIL_USE_DEFAULT_CREDENTIALS = false
- CLOUDFLARE_R2_ACCOUNT_ID = tu_account_id
- CLOUDFLARE_R2_API_S3 = https://tu_account_id.r2.cloudflarestorage.com
- API_PERU_BASE_URL = https://apiperu.dev/api
- LEAD_EXPIRATION_ENABLED = true
- LEAD_EXPIRATION_CRON_SCHEDULE = 0 0 0,8,16 * * *
- LEAD_EXPIRATION_MAX_ERRORS = 3
- LEAD_EXPIRATION_INITIAL_BACKOFF = 30
- LEAD_EXPIRATION_MAX_BACKOFF = 1440
- LEAD_EXPIRATION_MAX_FUTURE_DAYS = 30
- LEAD_EXPIRATION_MAX_PAST_YEARS = 1
- LEAD_EXPIRATION_MAX_RECYCLE_COUNT = 10
- LEAD_EXPIRATION_MIN_CREATION_AGE = 1
```

#### **Environment: Production**
```
Environment (production) → Environment Variables → Add Variable
- DATABASE_URL = Host=db;Database=gestionhogar;Username=user_prod;Password=PASSWORD_SEGURO_PROD
- POSTGRES_DB = gestionhogar
- POSTGRES_USER = user_prod
- POSTGRES_PASSWORD = PASSWORD_SEGURO_PROD
- JWT_SECRET_KEY = CLAVE_JWT_PRODUCCION_MUY_SEGURA_DIFERENTE
- JWT_ISSUER = https://api.tu-dominio.com
- JWT_AUDIENCE = https://api.tu-dominio.com
- CORS_ALLOWED_ORIGIN_1 = https://tu-frontend.com
- CORS_ALLOWED_ORIGIN_2 = https://tu-landing-page.com
- API_PERU_TOKEN = token_produccion
- EMAIL_USERNAME = produccion@tu-empresa.com
- EMAIL_PASSWORD = APP_PASSWORD_PRODUCCION
- CLOUDFLARE_R2_ACCESS_KEY_ID = KEY_PRODUCCION
- CLOUDFLARE_R2_SECRET_ACCESS_KEY = SECRET_PRODUCCION
- CLOUDFLARE_R2_BUCKET_NAME = gestion-hogar-prod
- CLOUDFLARE_R2_PUBLIC_URL_IMAGE = https://pub-prod.r2.dev
- BUSINESS_URL = https://tu-frontend.com
- LOG_LEVEL_DEFAULT = Warning
- LOG_LEVEL_ASPNETCORE = Error
# ... resto igual que development pero con valores de producción
```

### Paso 6: Deploy
1. Selecciona el environment (dev o prod)
2. Click **Deploy**
3. Dokploy automáticamente:
   - Clona el repo
   - Resuelve las variables según la jerarquía (environment > service > project)
   - Inyecta las variables al contenedor
   - Ejecuta `docker-compose build`
   - Ejecuta `docker-compose up -d`

---

## 🧪 Desarrollo Local

Para desarrollo en tu máquina:

```bash
# 1. Genera el archivo .env
./setup-local-env.sh

# 2. Edita .env con tus credenciales de desarrollo
nano .env

# 3. Levanta los servicios
docker-compose up --build

# 4. Verifica
curl http://localhost:8080/api/healthz
```

---

## ⚠️ IMPORTANTE: Rotar Credenciales

**Las siguientes credenciales estaban expuestas en Git y DEBEN rotarse antes de producción:**

### 1. Gmail App Password
- Email: `gestionhogarsacdesarrollo@gmail.com`
- **Acción:** Generar nuevo App Password en Google Account

### 2. Cloudflare R2 Keys
- **Acción:** Revocar keys actuales y generar nuevas en Cloudflare Dashboard

**Usa las nuevas credenciales en Dokploy (NO las del código antiguo).**

---

## 🎯 Checklist de Despliegue

### Development:
- [ ] Crear environment "development" en Dokploy
- [ ] Configurar las 50 variables de desarrollo
- [ ] Hacer deploy
- [ ] Verificar health check: `https://api-dev.tu-dominio.com/api/healthz`

### Production:
- [ ] Rotar TODAS las credenciales sensibles
- [ ] Crear environment "production" en Dokploy
- [ ] Configurar las 50 variables de producción (con nuevas credenciales)
- [ ] Hacer deploy
- [ ] Verificar health check: `https://api.tu-dominio.com/api/healthz`

---

## 📚 Archivos Usados por Dokploy

### ✅ **Archivos ACTIVOS (usados por Dokploy):**

- **`docker-compose.yml`** - Orquesta backend + PostgreSQL
- **`src/Deployment/Dockerfile.alpine`** - Construye la imagen .NET
- **`src/Deployment/docker-entrypoint.sh`** - Ejecuta migraciones + app
- **`env-template.txt`** - Template de variables de entorno
- **`setup-local-env.sh`** - Script para desarrollo local

### ⚠️ **Archivos LEGACY (movidos, no usados):**

- **`legacy-jenkins-ansible/`** - TODO el sistema antiguo
  - `Jenkinsfile.ci.groovy` - Pipeline CI de Jenkins
  - `+devops/+develop/` - Jenkinsfile, vault.yml, templates Jinja2
  - `+devops/ansible/` - Playbooks de Ansible
  - `+devops/docker/` - Dockerfiles y templates antiguos

- **`src/Deployment/legacy/`**
  - `Jenkins.dev.groovy` - Pipeline de desarrollo
  - `env.yaml` - Variables incompletas
  - `docker-compose.dev.yml` - Docker compose antiguo

### 📁 **Estructura Final:**

```
gestionhogar-backend/
├── docker-compose.yml              ✅ USADO - Docker Compose
├── env-template.txt                ✅ USADO - Variables template
├── setup-local-env.sh              ✅ USADO - Dev local
├── DOKPLOY_SETUP.md                ✅ USADO - Esta guía
├── README.md                       ✅ USADO - Descripción del proyecto
├── backend.sln                     ✅ USADO - Solution .NET
├── docs/                           ✅ USADO - Documentación
├── src/
│   ├── appsettings.json            ✅ USADO - Config base (placeholders)
│   ├── GestionHogar.csproj         ✅ USADO - Proyecto .NET
│   ├── Program.cs                  ✅ USADO - Punto de entrada
│   ├── Deployment/
│   │   ├── Dockerfile.alpine       ✅ USADO - Build .NET
│   │   ├── docker-entrypoint.sh    ✅ USADO - Startup script
│   │   └── legacy/                 📁 LEGACY (Jenkins/Ansible antiguo)
│   ├── Configuration/              ✅ USADO - Clases de config
│   ├── Controllers/                ✅ USADO - API Controllers
│   ├── Model/                      ✅ USADO - Entities
│   ├── Services/                   ✅ USADO - Servicios
│   ├── Migrations/                 ✅ USADO - Migraciones EF Core
│   ├── Templates/                  ✅ USADO - Templates de documentos
│   └── Utils/                      ✅ USADO - Utilidades
└── legacy-jenkins-ansible/         📁 LEGACY (TODO el sistema antiguo)
    ├── README.md                   ℹ️  Explicación del legacy
    ├── Jenkinsfile.ci.groovy
    └── +devops/
        ├── +develop/               (Jenkinsfile, vault.yml, templates)
        ├── ansible/                (Playbooks de Ansible)
        └── docker/                 (Dockerfiles y templates antiguos)
```

---

## 🚨 Solución de Problemas

### Backend no arranca
1. Verifica que TODAS las 50 variables estén configuradas
2. Revisa logs en Dokploy UI

### Error de conexión a base de datos
- Verifica que `DATABASE_URL` use `Host=db` (nombre del servicio en docker-compose)

### Error CORS
- Verifica que `CORS_ALLOWED_ORIGIN_1` coincida exactamente con la URL de tu frontend

### Emails no se envían
- Verifica `EMAIL_PASSWORD` (debe ser App Password de Gmail, no la contraseña normal)

---

**¡Listo para desplegar! 🚀**
