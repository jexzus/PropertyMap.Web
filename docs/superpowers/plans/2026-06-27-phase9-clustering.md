# Phase 9 — Clustering de Markers Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Agrupar propiedades cercanas en clusters numerados cuando el mapa está alejado, usando el clustering nativo de Mapbox GL JS, disolviendo en pines individuales (comportamiento actual sin cambios) cuando el zoom es suficiente.

**Architecture:** Approach híbrido en `map-interop.js`: una GeoJSON source con `cluster: true` alimenta 2 capas Mapbox (`clusters` círculo + `cluster-count` texto) para la vista agrupada; los puntos que Mapbox reporta como NO agrupados siguen renderizándose como `mapboxgl.Marker` HTML (mismo código/eventos que hoy). Un listener de `sourcedata` recalcula qué puntos son individuales en cada pan/zoom.

**Tech Stack:** Mapbox GL JS (ya en uso, sin nuevas dependencias), Blazor Server (.NET 9) sin cambios de contrato.

**Spec de referencia:** `docs/superpowers/specs/2026-06-27-phase9-clustering-design.md`

**Nota de corrección de spec:** el spec menciona `properties: { id, operacion, destacado }` para el GeoJSON feature. El campo `destacado` NO existe en `ListingMapDto` (verificado en `PropertyMap.Core/DTOs/ListingMapDto.cs` — solo tiene `Id, Lat, Lng, Titulo, Precio, Moneda, TipoPropiedad, Operacion, FotoUrl`) y el diseño aprobado no lo usa en ninguna lógica de cluster. Este plan usa `properties: { id, operacion }` únicamente — no es una desviación funcional, solo un campo no usado que no existía para empezar.

---

### Task 1: GeoJSON cluster source + capas + reescritura de `setMarkers`

**Files:**
- Modify: `PropertyMap.Web/PropertyMap.Web/wwwroot/js/map-interop.js`

- [ ] **Step 1: Agregar el estado interno de clustering**

En el objeto `window.mapInterop`, justo después de la línea `_selectedId: null,` (línea 9), agregar:

```js
  _selectedId: null,
  _geojsonData: null,              // último FeatureCollection cargado (para reconstruir tras cambio de estilo)
  _clusterHandlersBound: false,
  _highlightedClusterFeatureId: null,
```

(Reemplaza solo la línea `_selectedId: null,` por el bloque de 4 líneas de arriba.)

- [ ] **Step 2: Agregar helpers de construcción de GeoJSON y capas**

Inmediatamente antes del comentario `// ── Poner/actualizar markers ──...` (línea 194 del archivo original), insertar:

```js
  // ── Clustering: helpers ─────────────────────────────────────────────────────

  // Construye el FeatureCollection GeoJSON a partir de los listings.
  _buildGeoJson(listings) {
    return {
      type: 'FeatureCollection',
      features: listings.map(l => ({
        type: 'Feature',
        id: l.id,
        properties: { id: l.id, operacion: l.operacion },
        geometry: { type: 'Point', coordinates: [l.lng, l.lat] }
      }))
    };
  },

  // Crea la source + capas de cluster si todavía no existen en el estilo actual
  // (necesario también después de un cambio de estilo, que las borra).
  _ensureClusterLayers() {
    if (!this._map.getSource('listings-source')) {
      this._map.addSource('listings-source', {
        type: 'geojson',
        data: this._geojsonData ?? { type: 'FeatureCollection', features: [] },
        cluster: true,
        clusterRadius: 65,
        clusterMaxZoom: 15
      });
    }

    if (!this._map.getLayer('clusters')) {
      this._map.addLayer({
        id: 'clusters',
        type: 'circle',
        source: 'listings-source',
        filter: ['has', 'point_count'],
        paint: {
          'circle-color': '#1a73e8',
          'circle-radius': ['step', ['get', 'point_count'], 18, 10, 24, 50, 30],
          'circle-stroke-color': '#ffffff',
          'circle-stroke-width': ['case', ['boolean', ['feature-state', 'highlighted'], false], 4, 2]
        }
      });
    }

    if (!this._map.getLayer('cluster-count')) {
      this._map.addLayer({
        id: 'cluster-count',
        type: 'symbol',
        source: 'listings-source',
        filter: ['has', 'point_count'],
        layout: {
          'text-field': ['get', 'point_count_abbreviated'],
          'text-font': ['DIN Pro Medium', 'Arial Unicode MS Bold'],
          'text-size': 13
        },
        paint: { 'text-color': '#ffffff' }
      });
    }

    this._bindClusterHandlers();
  },

  // Listeners de click/hover sobre la capa de clusters y recálculo de pines
  // individuales en cada pan/zoom. Se bindean una sola vez por instancia de mapa
  // (los listeners de map.on persisten across setStyle, a diferencia de las capas).
  _bindClusterHandlers() {
    if (this._clusterHandlersBound) return;
    this._clusterHandlersBound = true;

    this._map.on('click', 'clusters', (e) => {
      const features = this._map.queryRenderedFeatures(e.point, { layers: ['clusters'] });
      if (!features.length) return;
      const clusterId = features[0].properties.cluster_id;
      this._map.getSource('listings-source').getClusterExpansionZoom(clusterId, (err, zoom) => {
        if (err) return;
        this._map.flyTo({ center: features[0].geometry.coordinates, zoom });
      });
    });

    this._map.on('mouseenter', 'clusters', () => { this._map.getCanvas().style.cursor = 'pointer'; });
    this._map.on('mouseleave', 'clusters', () => { this._map.getCanvas().style.cursor = ''; });

    this._map.on('sourcedata', (e) => {
      if (e.sourceId === 'listings-source' && e.isSourceLoaded) {
        this._refreshIndividualMarkers();
      }
    });
  },

  // Crea/actualiza los mapboxgl.Marker HTML solo para los puntos que NO están
  // agrupados según Mapbox (mismo aspecto/eventos que el código preexistente).
  _refreshIndividualMarkers() {
    if (!this._map.getSource('listings-source')) return;
    const features = this._map.querySourceFeatures('listings-source', {
      filter: ['!', ['has', 'point_count']]
    });

    const visibleIds = new Set(features.map(f => f.properties.id));

    this._markers.forEach(({ marker }, id) => {
      if (!visibleIds.has(id)) { marker.remove(); this._markers.delete(id); }
    });

    features.forEach(f => {
      const id = f.properties.id;
      if (this._markers.has(id)) return;

      const el = this._createHouseMarker(f.properties.operacion);

      el.addEventListener('click', () => {
        if (this._dotNetRef) this._dotNetRef.invokeMethodAsync('OnMarkerClick', [id]);
      });
      el.addEventListener('mouseenter', () => {
        if (this._dotNetRef) this._dotNetRef.invokeMethodAsync('OnMarkerHover', id);
      });
      el.addEventListener('mouseleave', () => {
        if (this._dotNetRef) this._dotNetRef.invokeMethodAsync('OnMarkerHover', -1);
      });

      const [lng, lat] = f.geometry.coordinates;
      const marker = new mapboxgl.Marker({ element: el, anchor: 'bottom' })
        .setLngLat([lng, lat])
        .addTo(this._map);

      this._markers.set(id, { marker, data: f.properties });
    });
  },

```

- [ ] **Step 3: Reescribir `setMarkers` para usar la source GeoJSON**

Reemplazar el método `setMarkers` completo (líneas 195-224 del archivo original):

```js
  setMarkers(listings, dotNetRef) {
    if (!this._map) return;
    this._dotNetRef = dotNetRef;
    this._geojsonData = this._buildGeoJson(listings);

    const apply = () => {
      if (this._map.getSource('listings-source')) {
        this._map.getSource('listings-source').setData(this._geojsonData);
      } else {
        this._ensureClusterLayers();
      }
      this._refreshIndividualMarkers();
    };

    if (this._map.isStyleLoaded()) apply();
    else this._map.once('load', apply);
  },
```

- [ ] **Step 4: Reconstruir clusters tras un cambio de estilo**

`setStyle` (cambiar entre vista calle/satélite) reemplaza el estilo completo, lo que borra la source y las capas custom. Reemplazar el método `setStyle` existente:

```js
  setStyle(styleUrl) {
    if (!this._map) return;
    // Guardar markers actuales para re-agregarlos después del style load
    const currentMarkers = new Map(this._markers);
    this._map.setStyle(this._resolveStyle(styleUrl));
    this._map.once('styledata', () => {
      currentMarkers.forEach(({ marker }) => marker.addTo(this._map));
      this._ensureClusterLayers();
      if (this._geojsonData && this._map.getSource('listings-source')) {
        this._map.getSource('listings-source').setData(this._geojsonData);
      }
    });
  },
```

- [ ] **Step 5: Verificar que no hay errores de sintaxis**

Run: `cd C:\Agentes\PropertyMap && node --check src/PropertyMap.Web/PropertyMap.Web/wwwroot/js/map-interop.js`
Expected: sin salida (sin errores de sintaxis). Si `node` no está disponible, abrir el archivo en el editor y confirmar que no hay llaves/paréntesis sin cerrar antes de continuar.

- [ ] **Step 6: Commit**

```bash
cd C:\Agentes\PropertyMap\src
git add PropertyMap.Web/PropertyMap.Web/wwwroot/js/map-interop.js
git commit -m "feat(map): cluster nearby listings using Mapbox GL native clustering"
```

---

### Task 2: Highlight de cluster desde hover de la lista + limpieza en `clearMarkers`

**Files:**
- Modify: `PropertyMap.Web/PropertyMap.Web/wwwroot/js/map-interop.js`

- [ ] **Step 1: Agregar `_highlightCluster` y `_clearClusterHighlight`**

Inmediatamente antes del método `highlightMarker` (después del método `selectMarker`, línea 233 del archivo original), insertar:

```js
  // Resalta visualmente el círculo del cluster que contiene un listing agrupado.
  _highlightCluster(id) {
    this._clearClusterHighlight();
    if (!this._map.getSource('listings-source')) return;

    const source = this._map.getSource('listings-source');
    const clusters = this._map.querySourceFeatures('listings-source', { filter: ['has', 'point_count'] });

    clusters.forEach(cluster => {
      source.getClusterLeaves(cluster.properties.cluster_id, cluster.properties.point_count, 0, (err, leaves) => {
        if (err || !leaves) return;
        if (leaves.some(leaf => leaf.properties.id === id)) {
          this._highlightedClusterFeatureId = cluster.id;
          this._map.setFeatureState({ source: 'listings-source', id: cluster.id }, { highlighted: true });
        }
      });
    });
  },

  _clearClusterHighlight() {
    if (this._highlightedClusterFeatureId != null && this._map.getSource('listings-source')) {
      this._map.setFeatureState({ source: 'listings-source', id: this._highlightedClusterFeatureId }, { highlighted: false });
    }
    this._highlightedClusterFeatureId = null;
  },

```

- [ ] **Step 2: Modificar `highlightMarker` para delegar a cluster cuando corresponda**

Reemplazar el método `highlightMarker` completo (líneas 236-246 del archivo original):

```js
  highlightMarker(id) {
    if (id === null || id === undefined) {
      this._markers.forEach(({ marker }) => {
        const el = marker.getElement();
        if (el) el.classList.remove('hovered');
      });
      this._clearClusterHighlight();
      return;
    }

    if (this._markers.has(id)) {
      this._markers.forEach(({ marker }, markerId) => {
        const el = marker.getElement();
        if (el) el.classList.toggle('hovered', markerId === id);
      });
      this._clearClusterHighlight();
      return;
    }

    this._highlightCluster(id);
  },
```

- [ ] **Step 3: Limpiar la source y el highlight en `clearMarkers`**

Reemplazar el método `clearMarkers` completo (líneas 249-252 del archivo original):

```js
  clearMarkers() {
    this._markers.forEach(({ marker }) => marker.remove());
    this._markers.clear();
    this._geojsonData = { type: 'FeatureCollection', features: [] };
    if (this._map && this._map.getSource('listings-source')) {
      this._map.getSource('listings-source').setData(this._geojsonData);
    }
    this._clearClusterHighlight();
  },
```

- [ ] **Step 4: Verificar que no hay errores de sintaxis**

Run: `cd C:\Agentes\PropertyMap && node --check src/PropertyMap.Web/PropertyMap.Web/wwwroot/js/map-interop.js`
Expected: sin salida.

- [ ] **Step 5: Commit**

```bash
cd C:\Agentes\PropertyMap\src
git add PropertyMap.Web/PropertyMap.Web/wwwroot/js/map-interop.js
git commit -m "feat(map): highlight containing cluster when hovering a grouped listing"
```

---

### Task 3: Verificación manual end-to-end en navegador

**Files:** ninguno (solo verificación, sin cambios de código)

- [ ] **Step 1: Levantar la API y el host Blazor**

```bash
cd C:\Agentes\PropertyMap\src
dotnet run --project PropertyMap.Api/PropertyMap.Api.csproj &
dotnet run --project PropertyMap.Web/PropertyMap.Web/PropertyMap.Web.csproj
```

(O usar el `run` skill del proyecto si ya está configurado para levantar ambos servicios.)

- [ ] **Step 2: Confirmar que hay suficientes propiedades cercanas para forzar clustering**

Si la base de datos seedeada no tiene suficientes propiedades cercanas entre sí, usar el panel admin/publisher para crear o aprobar varias propiedades con coordenadas cercanas (mismo barrio/ciudad) antes de continuar. Sin esto, el clustering nunca se activará visualmente y no se puede verificar.

- [ ] **Step 3: Verificar aparición de clusters**

Abrir la página del mapa (`/` o donde esté `MapView.razor`) con zoom alejado. Confirmar visualmente:
- Aparecen círculos azules con un número en zonas con propiedades cercanas, en vez de pines individuales superpuestos.
- Al hacer zoom in gradualmente, los clusters se dividen en clusters más chicos y eventualmente en pines individuales (a partir de zoom ≈15).

- [ ] **Step 4: Verificar click en cluster**

Click sobre un círculo de cluster. Confirmar que el mapa hace zoom-in (vuelo animado) centrado en esa zona, sin recargar la página ni errores en la consola del navegador (F12 → Console).

- [ ] **Step 5: Verificar pines individuales sin regresión**

Con zoom suficiente para ver pines individuales, confirmar:
- Los pines mantienen el color/ícono de casa según operación (rojo Venta, verde Alquiler, violeta AlquilerTemporario) — sin cambios visuales respecto a antes de este feature.
- Click en un pin individual abre el popup de detalle (`OnMarkerClick` → `popupListing`) igual que antes.
- Hover sobre un pin individual resalta la card correspondiente en la lista lateral (`OnMarkerHover`) igual que antes.

- [ ] **Step 6: Verificar highlight de cluster desde la lista lateral**

Con zoom alejado (clusters visibles), hacer hover sobre una card de la lista lateral cuya propiedad esté actualmente agrupada. Confirmar que el círculo del cluster correspondiente se resalta visualmente (borde más grueso, vía `circle-stroke-width`).

- [ ] **Step 7: Verificar cambio de estilo (calle ↔ satélite) no rompe el clustering**

Con clusters visibles, click en el botón "Satélite" y luego "Calle" (toggle de `MapView.razor`). Confirmar que los clusters y pines siguen apareciendo correctamente después del cambio de estilo, sin errores de consola.

- [ ] **Step 8: Confirmar ausencia de errores de consola**

Revisar la consola del navegador (F12) durante todo el flujo anterior. No debe haber errores relacionados a `listings-source`, `clusters`, `getClusterExpansionZoom`, `getClusterLeaves`, o `setFeatureState`.

- [ ] **Step 9: Detener los servidores de desarrollo**

Cerrar los procesos `dotnet run` levantados en el Step 1.
