# Phase 9 — Clustering de Markers — Design

**Status:** Approved
**Scope:** Primer sub-proyecto de Phase 9 (Escala & Calidad). El resto de Phase 9 (búsqueda avanzada, audit logs, security audit, testing exhaustivo) se diseña en specs separados, en ese orden.

## Contexto

El roadmap original (`phases.txt`) describe esta tarea como "Leaflet.markercluster para muchas propiedades". Sin embargo, el mapa real del proyecto (`PropertyMap.Web/wwwroot/js/map-interop.js`) usa **Mapbox GL JS**, no Leaflet — no hay ninguna dependencia de Leaflet en el código. Mapbox GL tiene clustering nativo vía GeoJSON source (`cluster: true`), que se usa en este diseño en lugar de Leaflet.markercluster.

## Problema

Hoy, `mapInterop.setMarkers(listings, dotNetRef)` crea un `mapboxgl.Marker` HTML individual por cada propiedad. Con muchas propiedades cercanas (ej. una ciudad densa), el mapa se vuelve ilegible — pines superpuestos, imposible distinguir cantidad real.

## Solución: approach híbrido

- **Vista alejada (zoom bajo, alta densidad):** las propiedades se agrupan en clusters — círculos con el número de propiedades agrupadas, usando el clustering nativo de Mapbox GL (GeoJSON source + capas).
- **Vista cercana (zoom alto, cluster disuelto):** se mantienen los `mapboxgl.Marker` HTML actuales (pin con ícono de casa, color por operación) sin cambios visuales ni de comportamiento.

Mapbox decide automáticamente, según `clusterRadius`/`clusterMaxZoom`, cuándo un punto se considera "agrupado" o "individual" — no hay lógica manual de distancia.

## Componentes

### `map-interop.js` — `mapInterop`

**`setMarkers(listings, dotNetRef)` (reescrito):**
1. Limpia markers HTML existentes (`clearMarkers()`, sin cambios).
2. Construye un GeoJSON `FeatureCollection`: un `Feature` por listing, `geometry: Point [lng, lat]`, `properties: { id, operacion, destacado }`.
3. Si la source `'listings-source'` ya existe, actualiza sus datos (`source.setData(geojson)`); si no, la crea con:
   ```js
   map.addSource('listings-source', {
     type: 'geojson',
     data: geojson,
     cluster: true,
     clusterRadius: 65,
     clusterMaxZoom: 15
   });
   ```
4. Agrega (una sola vez, si no existen) 2 capas:
   - `'clusters'` — `circle`, visible solo donde `point_count` existe (filtro `['has', 'point_count']`), `circle-radius` escalado en steps según `point_count` (ej. 18px hasta 10, 24px hasta 50, 30px más de 50), `circle-color` neutro (ej. `var(--color-primary)` resuelto a hex).
   - `'cluster-count'` — `symbol`, `text-field: ['get', 'point_count_abbreviated']`, centrado sobre el círculo.
5. Tras `setData`, usa `map.querySourceFeatures('listings-source')` para identificar qué listings NO están agrupados (`!feature.properties.cluster`) y crea/actualiza `mapboxgl.Marker` HTML solo para esos — mismo código de `_createHouseMarker` y mismos listeners (`click` → `OnMarkerClick`, `mouseenter`/`mouseleave` → `OnMarkerHover`) que existen hoy.
6. Se suscribe (una sola vez) a `map.on('sourcedata', ...)` filtrando por la source `'listings-source'` para recalcular el paso 5 cada vez que el usuario hace pan/zoom y Mapbox recalcula qué está agrupado.

**Eventos de cluster:**
- `map.on('click', 'clusters', (e) => { ... })`: obtiene `clusterId` del feature clickeado, llama a `source.getClusterExpansionZoom(clusterId)`, y hace `map.flyTo({ center, zoom: expansionZoom })` — zoom-in nativo hacia el cluster.
- `map.on('mouseenter', 'clusters', ...)` / `mouseleave`: cambia `cursor: pointer` sobre el canvas (UX estándar de mapas).

**Nueva función `highlightCluster(id)` (interna, llamada desde `highlightMarker`):**
- Busca en `querySourceFeatures('listings-source')` el feature cuyo `properties.id === id`.
- Si el feature tiene `cluster: true`, encuentra el cluster que lo contiene (vía `getClusterLeaves` del source de Mapbox, que expone qué leaves pertenecen a qué cluster) y aplica un resaltado visual al círculo correspondiente — usando `map.setFeatureState({source: 'listings-source', id: clusterFeature.id}, {highlighted: true})` y una expresión `case` en `circle-stroke-width` de la capa `'clusters'` que lea ese feature-state (ej. `['case', ['boolean', ['feature-state', 'highlighted'], false], 3, 0]`).
- Si el feature NO está agrupado (pin individual visible), delega al comportamiento actual de `highlightMarker` (clase CSS `hovered` sobre el marker HTML).

**`highlightMarker(id)` (modificado):** primero intenta resolver si `id` corresponde a un marker HTML activo (`this._markers.has(id)`); si sí, comportamiento sin cambios. Si no (porque está agrupado), llama a `highlightCluster(id)`.

**`clearMarkers()`:** además de remover markers HTML, limpia cualquier feature-state de highlight pendiente sobre la capa de clusters.

### Blazor

Sin cambios de contrato. El componente que orquesta el mapa sigue llamando `setMarkers`, `highlightMarker(id)`, `selectMarker(id)` exactamente igual — toda la lógica de "¿es cluster o pin individual?" vive en JS.

## Parámetros de clustering

- `clusterRadius: 65` (px) — intermedio entre el default de Mapbox (50) y un valor agresivo (80).
- `clusterMaxZoom: 15` — intermedio entre el default (14) y un valor agresivo (16). A partir de zoom 15, todos los puntos se muestran individuales sin importar densidad.

Estos valores son ajustables luego con datos reales de uso; no son una decisión final irreversible.

## Estilos

No se agrega CSS nuevo a `app.css`. Los estilos de los clusters (color, radio, texto) se definen mediante expresiones de Mapbox GL directamente en `map-interop.js` (capas `circle`/`symbol`), no como reglas CSS — son capas del mapa, no elementos DOM. El highlight de cluster usa `feature-state` + expresión de capa, no clases CSS.

## Testing

Sin tests automatizados nuevos — es un cambio de JS/UI puro sobre la interacción con el mapa, sin lógica de servidor ni endpoints involucrados (los listings ya se sirven igual que antes vía `/api/listings/map`). Verificación manual en navegador al cerrar la implementación:
- Cargar el mapa con suficientes propiedades cercanas entre sí para forzar clustering visible.
- Click en un cluster hace zoom-in correctamente.
- A partir de zoom suficiente, los clusters se disuelven y aparecen pines individuales con su color de operación de siempre.
- Click/hover en pines individuales sigue notificando a Blazor (`OnMarkerClick`/`OnMarkerHover`) y sincronizando con la lista lateral.
- Hover desde la lista lateral sobre una propiedad agrupada resalta visualmente el cluster que la contiene.

## Fuera de scope

- Coloreado de clusters por operación dominante (descartado, se eligió círculo neutro con número).
- Popup/lista de propiedades al click en cluster sin zoom (descartado, se eligió zoom nativo).
- Migración completa a capas Mapbox para pines individuales (eliminar `mapboxgl.Marker` HTML por completo) — quedó descartada en favor del approach híbrido, que preserva el código de eventos existente.
