# Phase 9.5 — Búsqueda Avanzada — Design

**Status:** Approved
**Scope:** Quinto y último sub-proyecto de Phase 9 (Escala & Calidad). Orden completo de Phase 9: 1) Clustering (✅), 2) Testing exhaustivo (✅), 3) Security audit (✅), 4) Audit logs (✅), 5) Búsqueda avanzada (este spec).

## Contexto

El roadmap original describe esta tarea como "Full-text search SQL Server o PostGIS". PostGIS no aplica — el proyecto usa SQL Server LocalDB, no PostgreSQL.

Hoy el filtrado de propiedades es 100% client-side: `Home.razor` trae **todos** los listings activos del servidor (`GET /api/listings` → `ListingRepository.GetActiveListingsAsync()`, sin paginación) y filtra en memoria con LINQ (`ApplyFilters()`). No existe ninguna búsqueda de texto sobre contenido de propiedades — el buscador visible en `FilterBar.razor` es geocoding de direcciones (vuela el mapa a una ubicación), no búsqueda de listings.

Esto no escala: cada carga de la home descarga el catálogo completo de propiedades activas, sin importar cuántas haya.

## Alcance

### 1. Motor de texto: `LIKE` vía EF Core (no Full-Text Search real)

Se usa `.Contains()` en LINQ, que EF Core traduce a `LIKE '%termino%'` en SQL Server. Sin configuración adicional (sin `CREATE FULLTEXT CATALOG`/`INDEX` vía SQL crudo), funciona igual en LocalDB y en tests con EF InMemory. Trade-off aceptado: sin ranking de relevancia ni stemming (buscar "casa" no encuentra "casas") — suficiente para el volumen de datos actual del proyecto.

### 2. Paginación solo en la lista lateral, el mapa no cambia

`GET /api/listings/map` (que alimenta el mapa + clustering de Phase 9.1) **no se modifica** — sigue trayendo todos los listings activos sin filtrar ni paginar. Decisión explícita: el mapa y la lista lateral pasan a ser dos vistas independientes del mismo catálogo — los filtros de `FilterBar` dejan de sincronizar automáticamente qué pines se ven en el mapa con la página actual de la lista (antes sí estaban sincronizados, vía filtrado client-side compartido). Este es un cambio de comportamiento consciente, no una regresión accidental.

Solo `GET /api/listings` (la lista lateral, que trae la entidad completa `PropertyListing`) se reemplaza por un nuevo endpoint paginado.

### 3. Nuevo endpoint: `GET /api/listings/search`

Query params (todos opcionales salvo `page`/`pageSize`, que tienen default):
- `q` (string) — texto libre, busca en `Titulo`, `Descripcion`, `Location.Ciudad`, `Location.DireccionTexto`
- `operacion` (string) — igual al filtro `Operacion` existente
- `tipoPropiedad` (string)
- `precioMax` (decimal)
- `dormitoriosMin` (int)
- `banosMin` (int)
- `page` (int, default 1)
- `pageSize` (int, default 20)

Devuelve `PagedResultDto<PropertyListing>`.

### 4. `IListingRepository.SearchAsync`

Nuevo método en el repositorio. Arma un `IQueryable<PropertyListing>` con `.Where()` condicionales (cada filtro se aplica solo si el parámetro correspondiente no es null/vacío), cuenta `TotalCount` antes de paginar (`CountAsync()`), y aplica `.Skip((page-1)*pageSize).Take(pageSize)` al final. El filtro de texto usa:
```csharp
.Where(l => l.Titulo.Contains(q) || l.Descripcion.Contains(q) ||
            l.Location.Ciudad.Contains(q) || l.Location.DireccionTexto.Contains(q))
```

### 5. `PagedResultDto<T>` (nuevo, genérico)

```csharp
public record PagedResultDto<T>(List<T> Items, int TotalCount, int Page, int PageSize);
```

### 6. `Home.razor`

Reemplaza la carga inicial de `allListings` + filtrado client-side por: cada cambio de filtro, de texto de búsqueda, o de página dispara una llamada a `SearchAsync` (vía un nuevo método en `IListingApiService`). Se guarda `_currentPage`/`_totalCount` para mostrar controles de paginación simples (anterior/siguiente + "mostrando X–Y de Z"). `mapListings` sigue cargándose una sola vez al inicio desde `/api/listings/map`, sin relación con la paginación.

### 7. `FilterBar.razor`

Se agrega un input nuevo, separado del buscador de geocoding existente: "Buscar palabra clave..." con debounce de 250ms (mismo patrón ya usado para las sugerencias de geocoding), que emite `OnKeywordChanged`.

## Testing

Tests de integración nuevos (xUnit + EF InMemory + `WebApplicationFactory`, mismo patrón que los 109 tests existentes):
- Búsqueda por palabra clave encuentra coincidencias en título, descripción y ciudad.
- Filtros combinados (operación + precio máximo + texto) se aplican todos a la vez.
- Paginación: `page`/`pageSize` se respetan, `TotalCount` refleja el total real (no solo la página actual).
- Búsqueda sin resultados devuelve `Items` vacío con `TotalCount = 0`, no error.
- `GET /api/listings/map` no se ve afectado por ningún cambio de esta tarea (test de regresión).

## Fuera de scope

- SQL Server Full-Text Search real (`CREATE FULLTEXT INDEX`, ranking, stemming) — descartado por complejidad de setup/testing frente al beneficio para el volumen actual.
- Sincronización entre los filtros de la lista paginada y los pines visibles del mapa — decisión consciente de mantenerlos independientes (ver punto 2).
- Paginación o filtrado en `AdminController.GetAll` (vista admin de todos los listings activos) — mismo problema de escala en teoría, pero fuera del alcance de esta tarea; el tráfico admin es mucho menor.
- Ordenamiento configurable de resultados (por precio, fecha, etc.) más allá del orden ya existente (`Destacado` desc, `FechaPublicacion` desc) — no pedido, no se agrega.
