# Lógica de PreselectedId en Proyectos

## Descripción General

El `preselectedId` es un parámetro que permite que un proyecto específico aparezca en la primera posición de la primera página de resultados, independientemente de su posición original en la lista ordenada.

## Comportamiento Esperado

### ✅ Comportamiento Correcto

1. **Página 1**: El proyecto preseleccionado aparece en la posición 1
2. **Páginas 2+**: El proyecto preseleccionado NO aparece (evita duplicación)
3. **Otros proyectos**: Se desplazan hacia abajo en la primera página, mantienen su orden relativo

### ❌ Comportamiento Incorrecto (Duplicación)

1. **Página 1**: El proyecto preseleccionado aparece en la posición 1
2. **Páginas 2+**: El proyecto preseleccionado aparece también en su posición original
3. **Resultado**: Proyecto duplicado en la lista

## Implementación Técnica

### Backend (ProjectService.cs)

```csharp
// Lógica para preselectedId
Guid? preselectedGuid = null;
if (!string.IsNullOrWhiteSpace(preselectedId) && Guid.TryParse(preselectedId, out var parsedGuid))
{
    preselectedGuid = parsedGuid;
    
    if (page == 1)
    {
        // Página 1: Incluir proyecto preseleccionado al inicio
        query = query.OrderBy(p => p.Id == preselectedGuid ? 0 : 1);
    }
    else
    {
        // Páginas 2+: Excluir proyecto preseleccionado para evitar duplicados
        query = query.Where(p => p.Id != preselectedGuid);
    }
}
```

### Frontend (useProjects.ts)

```typescript
// Solo enviar preselectedId cuando hay un proyecto específico seleccionado
const preselectedId = isAllProjectsSelected ? undefined : selectedProject?.id;
```

## Casos de Uso

### Caso 1: Proyecto en Segunda Página
- **Situación**: 20 proyectos, "Hunter" está en posición 19 (página 2)
- **Acción**: Seleccionar "Hunter"
- **Resultado Esperado**:
  - Página 1: "Hunter" en posición 1, otros 9 proyectos desplazados
  - Página 2: "Hunter" NO aparece, solo 10 proyectos restantes

### Caso 2: Proyecto en Primera Página
- **Situación**: "Hunter" ya está en posición 5 (página 1)
- **Acción**: Seleccionar "Hunter"
- **Resultado Esperado**:
  - Página 1: "Hunter" se mueve a posición 1
  - Páginas 2+: Sin cambios

### Caso 3: "Todos los Proyectos"
- **Situación**: Usuario selecciona "Todos los proyectos"
- **Acción**: Navegar entre páginas
- **Resultado Esperado**: Sin preselectedId, orden normal

## Verificación y Testing

### 1. Verificar en Consola del Navegador

```javascript
// Buscar estos logs en la consola:
// 🔍 usePaginatedActiveProjectsWithSearch called with: { preselectedId: "guid-here" }
// 📊 Query data pages: [array of pages with data]
// 📋 All projects (flattened): [array of all projects]
// 🎯 ProjectSelector state: { selectedProject: {...}, preselectedId: "guid-here" }
// 📝 Project options created: [array of options]
```

### 2. Verificar en Network Tab

```http
GET /api/Projects/active/paginated?page=1&pageSize=10&preselectedId=guid-here
GET /api/Projects/active/paginated?page=2&pageSize=10&preselectedId=guid-here
```

**Verificar que**:
- Página 1: Incluye el proyecto preseleccionado
- Página 2: NO incluye el proyecto preseleccionado

### 3. Verificar Duplicación

**Síntomas de duplicación**:
```
Console Error: Encountered two children with the same key, `guid-here`
```

**Causa**: El mismo proyecto aparece en múltiples páginas

## Debugging

### Problema: Proyecto Duplicado

**Diagnóstico**:
1. Verificar que el backend excluye el proyecto en páginas 2+
2. Verificar que el frontend no envía preselectedId en páginas incorrectas
3. Verificar que las keys de React son únicas

**Solución**:
```csharp
// En páginas 2+, agregar:
query = query.Where(p => p.Id != preselectedGuid);
```

### Problema: Proyecto No Aparece en Primera Página

**Diagnóstico**:
1. Verificar que preselectedId se envía correctamente
2. Verificar que el proyecto existe y está activo
3. Verificar la lógica de ordenamiento

**Solución**:
```csharp
// En página 1, verificar:
if (preselectedProject != null)
{
    query = query.OrderBy(p => p.Id == preselectedGuid ? 0 : 1);
}
```

## Archivos Relacionados

- **Backend**: `gestionhogar-backend/src/Controllers/Project/ProjectService.cs`
- **Frontend Hook**: `gestionhogar-frontend/src/app/(admin)/admin/projects/_hooks/useProjects.ts`
- **Componente**: `gestionhogar-frontend/src/components/ui/project-selector.tsx`

## Notas Importantes

1. **Solo para proyectos activos**: La lógica solo aplica a `GetActiveProjectsPaginatedAsync`
2. **Paginación infinita**: El frontend usa `useInfiniteQuery` para cargar páginas dinámicamente
3. **Keys únicas**: Cada proyecto debe tener una key única en React para evitar warnings
4. **Performance**: La exclusión en páginas 2+ es eficiente y no afecta el rendimiento

## Ejemplo de Flujo Completo

1. Usuario selecciona "Hunter" (posición 19)
2. Frontend envía `preselectedId: "hunter-guid"`
3. Backend página 1: Mueve "Hunter" a posición 1
4. Backend página 2: Excluye "Hunter" de resultados
5. Frontend recibe datos sin duplicación
6. UI muestra "Hunter" solo en primera página
