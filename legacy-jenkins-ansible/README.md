# 📦 Archivos Legacy - Jenkins + Ansible

## ⚠️ ESTOS ARCHIVOS YA NO SE USAN

Esta carpeta contiene los archivos antiguos del sistema de despliegue con **Jenkins + Ansible** que fue reemplazado por **Dokploy**.

---

## 📁 Contenido

### `Jenkinsfile.ci.groovy`
- Pipeline de Jenkins para CI/CD
- **Usado antes:** Para builds automáticos en Jenkins
- **Ahora:** Dokploy hace builds automáticos desde GitHub

### `+devops/+develop/`
- `Jenkinsfile` - Pipeline para ambiente de desarrollo
- `vault.yml` - Credenciales encriptadas con Ansible Vault
- `docker-compose.backend.yml.j2` - Template Jinja2 para docker-compose

### `+devops/ansible/`
- `service_pull_and_setup.yml` - Playbook para pull de imágenes
- `setup_target.yml` - Configuración del servidor remoto
- `start_service.yml` - Iniciar servicios remotamente

### `+devops/docker/`
- `Dockerfile` (legacy/) - Dockerfile antiguo
- `docker-compose.base.yml.j2` - Template base para docker-compose
- `docker-entrypoint.sh` - Script de entrada (duplicado, ya existe en src/Deployment/)

---

## 🔄 Migración Completada

**Antes (Jenkins + Ansible):**
```
GitHub → Jenkins → Build → Ansible → SSH Server → Deploy
```

**Ahora (Dokploy):**
```
GitHub → Dokploy → Build + Deploy (todo automático)
```

---

## 📝 Por qué se movió a Legacy

1. **Complejidad innecesaria** - Ansible requería múltiples playbooks y configuración compleja
2. **Variables incompletas** - Solo 16 variables en `env.yaml`, faltaban muchas
3. **Difícil mantenimiento** - SSH keys, Vault, templates Jinja2
4. **Dokploy es más simple** - Todo desde UI web, sin SSH, sin Ansible

---

## ✅ Archivos ACTIVOS (no en legacy)

**Archivos que SÍ se usan con Dokploy:**

```
gestionhogar-backend/
├── docker-compose.yml              ✅ Docker Compose para Dokploy
├── env-template.txt                ✅ Template de variables
├── setup-local-env.sh              ✅ Script para desarrollo local
├── DOKPLOY_SETUP.md                ✅ Guía de configuración
└── src/
    └── Deployment/
        ├── Dockerfile.alpine       ✅ Build de .NET
        └── docker-entrypoint.sh    ✅ Script de inicio
```

---

## 🗑️ ¿Puedo eliminar esta carpeta?

**Recomendación:** Mantenerla por ahora (backup).

**Cuándo eliminar:** Después de 3-6 meses de producción estable con Dokploy.

---

**Fecha de migración:** Diciembre 2025  
**Sistema anterior:** Jenkins + Ansible  
**Sistema actual:** Dokploy

