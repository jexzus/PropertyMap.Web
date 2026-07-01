// PropertyMap — Mapbox GL JS Interop

window.mapInterop = {

  _map: null,
  _markers: new Map(),   // id → { marker: mapboxgl.Marker, data }
  _searchMarker: null,   // pin sobre la ubicación buscada
  _dotNetRef: null,
  _selectedId: null,
  _geojsonData: null,              // último FeatureCollection cargado (para reconstruir tras cambio de estilo)
  _clusterHandlersBound: false,
  _highlightedClusterFeatureId: null,
  _highlightSeq: 0,
  _userLat: null,
  _userLng: null,

  // ── Inicializar el mapa ────────────────────────────────────────────────────
  initMap(elementId, lat, lng, zoom) {
    if (typeof mapboxgl === 'undefined') { console.warn('Mapbox GL no cargó'); return; }
    const el = document.getElementById(elementId);
    if (!el) return;

    if (this._map) {
      // Si el contenedor sigue siendo el mismo y está en el DOM, solo redimensionar.
      const current = this._map.getContainer ? this._map.getContainer() : null;
      if (current === el && el.isConnected) {
        this._map.resize();
        return;
      }
      // Navegación SPA: el contenedor anterior se destruyó. Recreamos el mapa.
      try { this._map.remove(); } catch (e) { /* ya removido */ }
      this._map = null;
      this._markers.clear();
      this._searchMarker = null;
      this._dotNetRef = null;
      this._selectedId = null;
      this._clusterHandlersBound = false;
      this._geojsonData = null;
    }

    mapboxgl.accessToken = window.MAPBOX_TOKEN;

    this._map = new mapboxgl.Map({
      container: elementId,
      style: this._resolveStyle('mapbox://styles/mapbox/streets-v12'),
      center: [lng, lat],
      zoom: zoom ?? 13,
      attributionControl: true,
    });

    this._map.addControl(new mapboxgl.NavigationControl(), 'bottom-right');
  },

  // ── Centrar en ubicación del usuario ──────────────────────────────────────
  centerOnUser() {
    // Solo una vez por sesión de página, para no re-preguntar en cada navegación.
    if (this._centered || !this._map || !navigator.geolocation) return;
    this._centered = true;
    navigator.geolocation.getCurrentPosition(
      pos => this._map.flyTo({ center: [pos.coords.longitude, pos.coords.latitude], zoom: 14 }),
      () => {}
    );
  },

  // Vuela a una coordenada y coloca el marcador de búsqueda (como Google Maps).
  flyTo(lat, lng) {
    if (!this._map) return;
    this._map.flyTo({ center: [lng, lat], zoom: 15, duration: 1000 });
    this.setSearchMarker(lat, lng);
  },

  // Marcador distintivo (azul) sobre la ubicación buscada por el usuario.
  setSearchMarker(lat, lng) {
    if (!this._map) return;
    if (this._searchMarker) {
      this._searchMarker.setLngLat([lng, lat]);
      return;
    }
    const el = document.createElement('div');
    el.className = 'pm-search-marker';
    el.innerHTML = `
      <svg width="34" height="44" viewBox="0 0 24 32" fill="none" xmlns="http://www.w3.org/2000/svg" aria-hidden="true">
        <path d="M12 0C5.4 0 0 5.4 0 12c0 9 12 20 12 20s12-11 12-20C24 5.4 18.6 0 12 0z" fill="#1a73e8" stroke="white" stroke-width="1.5"/>
        <circle cx="12" cy="12" r="4.5" fill="white"/>
      </svg>`;
    this._searchMarker = new mapboxgl.Marker({ element: el, anchor: 'bottom' })
      .setLngLat([lng, lat])
      .addTo(this._map);
  },

  clearSearchMarker() {
    if (this._searchMarker) { this._searchMarker.remove(); this._searchMarker = null; }
  },

  // Sugerencias de lugares (ciudades, barrios) mientras se escribe.
  // Usa Photon (OpenStreetMap) en modo fallback: soporta proximidad por lat/lon y bbox.
  async geocodeSuggest(query) {
    if (!query || query.trim().length < 3) return [];

    // Capturar ubicación del usuario la primera vez (para ordenar por cercanía).
    if (this._userLat === null && navigator.geolocation) {
      navigator.geolocation.getCurrentPosition(
        pos => { this._userLat = pos.coords.latitude; this._userLng = pos.coords.longitude; },
        ()  => { this._userLat = 0; this._userLng = 0; }
      );
    }

    try {
      if (window.MAP_FALLBACK || !window.MAPBOX_TOKEN) {
        // Photon geocoder: sin token, soporta proximidad, más estable que Nominatim.
        let url = `https://photon.komoot.io/api/?q=${encodeURIComponent(query)}&limit=6&lang=es&bbox=-73.58,-55.05,-53.58,-21.78`;
        if (this._userLat && this._userLng)
          url += `&lat=${this._userLat}&lon=${this._userLng}`;
        const json = await (await fetch(url)).json();
        return (json.features || []).slice(0, 5).map(f => {
          const p = f.properties;
          // Construir label: nombre + provincia/estado
          const parts = [p.name, p.state || p.county].filter(Boolean);
          return {
            label: parts.join(', '),
            lat: f.geometry.coordinates[1],
            lng: f.geometry.coordinates[0]
          };
        });
      } else {
        let url = `https://api.mapbox.com/search/geocode/v6/forward?q=${encodeURIComponent(query)}&access_token=${window.MAPBOX_TOKEN}&language=es&country=ar&limit=5`;
        if (this._userLat && this._userLng)
          url += `&proximity=${this._userLng},${this._userLat}`;
        const json = await (await fetch(url)).json();
        return (json.features || []).map(f => ({
          label: (f.properties && (f.properties.full_address || f.properties.name)) || '',
          lat: f.geometry.coordinates[1],
          lng: f.geometry.coordinates[0]
        }));
      }
    } catch (e) {
      console.warn('geocodeSuggest error', e);
      return [];
    }
  },

  // Geocodificación precisa de dirección con número.
  // bbox restringe DUROS los resultados al área de la ciudad — proximity solo sugiere, bbox impone.
  async geocodeAddress(query, proximityLng, proximityLat, bboxMinLng, bboxMinLat, bboxMaxLng, bboxMaxLat) {
    if (!query || query.trim().length < 3) return [];
    try {
      const nominatimSearch = async () => {
        let url = `https://nominatim.openstreetmap.org/search?q=${encodeURIComponent(query)}&format=json&limit=3&countrycodes=ar&accept-language=es`;
        if (bboxMinLng != null)
          url += `&viewbox=${bboxMinLng},${bboxMaxLat},${bboxMaxLng},${bboxMinLat}&bounded=1`;
        const json = await (await fetch(url)).json();
        return (json || []).map(r => ({ label: r.display_name, lat: parseFloat(r.lat), lng: parseFloat(r.lon) }));
      };

      if (window.MAP_FALLBACK || !window.MAPBOX_TOKEN) {
        return await nominatimSearch();
      } else {
        let url = `https://api.mapbox.com/search/geocode/v6/forward?q=${encodeURIComponent(query)}&access_token=${window.MAPBOX_TOKEN}&language=es&country=ar&limit=3`;
        if (proximityLng != null && proximityLat != null)
          url += `&proximity=${proximityLng},${proximityLat}`;
        if (bboxMinLng != null)
          url += `&bbox=${bboxMinLng},${bboxMinLat},${bboxMaxLng},${bboxMaxLat}`;
        const json = await (await fetch(url)).json();
        const results = (json.features || []).map(f => ({
          label: (f.properties && (f.properties.full_address || f.properties.name)) || '',
          lat: f.geometry.coordinates[1],
          lng: f.geometry.coordinates[0]
        }));
        // Si Mapbox no encuentra nada, intentar con Nominatim
        if (results.length === 0) return await nominatimSearch();
        return results;
      }
    } catch (e) {
      console.warn('geocodeAddress error', e);
      return [];
    }
  },

  // ── Geocodificar y volar al lugar ─────────────────────────────────────────
  async geocodeAndFly(query) {
    if (!this._map || !query) return;
    try {
      const coords = window.MAP_FALLBACK
        ? await this._geocodeNominatim(query)   // modo temporal sin token
        : await this._geocodeMapbox(query);
      if (coords) {
        const [lng, lat] = coords;
        this._map.flyTo({ center: [lng, lat], zoom: 15, duration: 1200 });
        this.setSearchMarker(lat, lng);
      }
    } catch (e) {
      console.warn('Geocoding error', e);
    }
  },

  // Geocoding de Mapbox (API v6; la v5 está deprecada). Requiere token.
  async _geocodeMapbox(query) {
    const token = window.MAPBOX_TOKEN;
    const encoded = encodeURIComponent(query);
    const url = `https://api.mapbox.com/search/geocode/v6/forward?q=${encoded}&access_token=${token}&language=es&country=ar&limit=1`;
    const json = await (await fetch(url)).json();
    const feature = json.features?.[0];
    return feature?.geometry?.coordinates ?? feature?.center ?? null;
  },

  // Geocoding gratuito de OpenStreetMap (Nominatim), sin token. Limitado a Argentina.
  async _geocodeNominatim(query) {
    const encoded = encodeURIComponent(query);
    const url = `https://nominatim.openstreetmap.org/search?q=${encoded}&format=json&limit=1&countrycodes=ar&accept-language=es`;
    const json = await (await fetch(url)).json();
    const r = json?.[0];
    return r ? [parseFloat(r.lon), parseFloat(r.lat)] : null;
  },

  // ── Cambiar style del mapa ────────────────────────────────────────────────
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

  // ── Poner/actualizar markers ───────────────────────────────────────────────
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

  // ── Resaltar marker seleccionado (click) ──────────────────────────────────
  selectMarker(id) {
    this._selectedId = id;
    this._markers.forEach(({ marker }, markerId) => {
      const el = marker.getElement();
      if (el) el.classList.toggle('selected', markerId === id);
    });
  },

  // Resalta visualmente el círculo del cluster que contiene un listing agrupado.
  _highlightCluster(id) {
    this._clearClusterHighlight();
    if (!this._map) return;
    if (!this._map.getSource('listings-source')) return;

    const seq = ++this._highlightSeq;
    const source = this._map.getSource('listings-source');
    const clusters = this._map.querySourceFeatures('listings-source', { filter: ['has', 'point_count'] });

    clusters.forEach(cluster => {
      source.getClusterLeaves(cluster.properties.cluster_id, cluster.properties.point_count, 0, (err, leaves) => {
        if (seq !== this._highlightSeq) return; // callback obsoleto (hubo un highlight/clear más nuevo), descartar
        if (err || !leaves) return;
        if (leaves.some(leaf => leaf.properties.id === id)) {
          this._highlightedClusterFeatureId = cluster.id;
          this._map.setFeatureState({ source: 'listings-source', id: cluster.id }, { highlighted: true });
        }
      });
    });
  },

  _clearClusterHighlight() {
    this._highlightSeq++;
    if (this._highlightedClusterFeatureId != null && this._map && this._map.getSource('listings-source')) {
      this._map.setFeatureState({ source: 'listings-source', id: this._highlightedClusterFeatureId }, { highlighted: false });
    }
    this._highlightedClusterFeatureId = null;
  },

  // ── Highlight marker desde hover de card ──────────────────────────────────
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

  // ── Limpiar todos los markers ──────────────────────────────────────────────
  clearMarkers() {
    this._markers.forEach(({ marker }) => marker.remove());
    this._markers.clear();
    this._geojsonData = { type: 'FeatureCollection', features: [] };
    if (this._map && this._map.getSource('listings-source')) {
      this._map.getSource('listings-source').setData(this._geojsonData);
    }
    this._clearClusterHighlight();
  },

  resizeMap() {
    if (this._map) this._map.resize();
  },

  // ── Resize handle drag ────────────────────────────────────────────────────
  initResize(panelSelector, handleSelector) {
    const panel = document.querySelector(panelSelector);
    const handle = document.querySelector(handleSelector);
    if (!panel || !handle) return;

    let dragging = false, startX = 0, startW = 0;

    handle.addEventListener('mousedown', e => {
      dragging = true;
      startX = e.clientX;
      startW = panel.offsetWidth;
      document.body.style.cssText += ';cursor:col-resize;user-select:none';
      e.preventDefault();
    });

    document.addEventListener('mousemove', e => {
      if (!dragging) return;
      const w = Math.min(580, Math.max(300, startW + e.clientX - startX));
      panel.style.width = w + 'px';
      this._map?.resize();
    });

    document.addEventListener('mouseup', () => {
      if (dragging) {
        dragging = false;
        document.body.style.cursor = '';
        document.body.style.userSelect = '';
      }
    });
  },

  // ── Helpers ────────────────────────────────────────────────────────────────

  // Resuelve el estilo del mapa según el modo activo:
  //  • Con token de Mapbox → se usa el estilo mapbox:// tal cual.
  //  • Sin token (MapLibre) → estilos gratuitos equivalentes que no requieren token.
  _resolveStyle(style) {
    if (!window.MAP_FALLBACK) return style;
    if (typeof style === 'string' && style.includes('satellite')) {
      return this._satelliteStyle();
    }
    // OSM raster tiles: siempre disponibles, sin autenticación.
    return this._osmRasterStyle();
  },

  _osmRasterStyle() {
    return {
      version: 8,
      sources: {
        osm: {
          type: 'raster',
          tiles: ['https://tile.openstreetmap.org/{z}/{x}/{y}.png'],
          tileSize: 256,
          attribution: '© <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
        }
      },
      layers: [{ id: 'osm-tiles', type: 'raster', source: 'osm' }]
    };
  },

  // Estilo satelital gratuito (imágenes de Esri World Imagery, sin token).
  _satelliteStyle() {
    return {
      version: 8,
      sources: {
        'esri-satellite': {
          type: 'raster',
          tiles: ['https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}'],
          tileSize: 256,
          attribution: 'Tiles © Esri, Maxar, Earthstar Geographics'
        }
      },
      layers: [{ id: 'esri-satellite', type: 'raster', source: 'esri-satellite' }]
    };
  },

  _createHouseMarker(operacion) {
    const colorMap = {
      'Venta':              '#be123c',   // crimson
      'Alquiler':           '#16a34a',   // green
      'AlquilerTemporario': '#7c3aed',   // purple
    };
    const color = colorMap[operacion] ?? '#be123c';
    const opClass = (operacion ?? '').toLowerCase().replace('alquilertemporario', 'temporario');

    const el = document.createElement('div');
    el.className = `pm-marker pm-marker--${opClass}`;
    el.dataset.operacion = operacion;
    el.innerHTML = `
      <svg width="30" height="38" viewBox="0 0 30 38" fill="none" xmlns="http://www.w3.org/2000/svg" aria-hidden="true">
        <path d="M15 1C7.268 1 1 7.268 1 15c0 9.941 14 22 14 22S29 24.941 29 15C29 7.268 22.732 1 15 1z"
              fill="${color}" stroke="white" stroke-width="1.5"/>
        <path d="M15 8L22.5 14.5H20.5V22H9.5V14.5H7.5L15 8Z" fill="white" opacity="0.95"/>
        <rect x="12.5" y="17" width="5" height="5" rx="0.5" fill="${color}"/>
      </svg>`;
    return el;
  },

  _fmtPrice(precio, moneda) {
    if (!precio) return '?';
    const sym = moneda === 'USD' ? 'U$S' : '$';
    if (precio >= 1_000_000) return `${sym}${(precio / 1_000_000).toFixed(1)}M`;
    if (precio >= 1_000)     return `${sym}${Math.round(precio / 1_000)}k`;
    return `${sym}${precio}`;
  },
};

// ── Mini-mapa de un solo punto (detalle + wizard) ───────────────────────────
// Instancias independientes por elemento, para coexistir con el mapa principal.
// Usa la misma librería activa (Mapbox con token o MapLibre gratis) vía el alias
// global `mapboxgl` y reutiliza mapInterop._resolveStyle para elegir el estilo.
window.detailMap = {
  _maps: {},   // elementId → { map, marker }

  init(elementId, lat, lng, zoom, draggable, dotNetRef) {
    if (typeof mapboxgl === 'undefined') { console.warn('Mapa no cargó'); return; }
    const el = document.getElementById(elementId);
    if (!el) return;
    if (this._maps[elementId]) this.dispose(elementId);

    if (window.MAPBOX_TOKEN) mapboxgl.accessToken = window.MAPBOX_TOKEN;

    const style = window.mapInterop
      ? window.mapInterop._resolveStyle('mapbox://styles/mapbox/streets-v12')
      : 'https://tiles.openfreemap.org/styles/liberty';

    const map = new mapboxgl.Map({
      container: elementId,
      style: style,
      center: [lng, lat],
      zoom: zoom ?? 15,
      attributionControl: true,
    });
    map.addControl(new mapboxgl.NavigationControl(), 'bottom-right');

    const marker = new mapboxgl.Marker({ draggable: !!draggable })
      .setLngLat([lng, lat])
      .addTo(map);

    if (draggable && dotNetRef) {
      marker.on('dragend', () => {
        const p = marker.getLngLat();
        dotNetRef.invokeMethodAsync('OnPinDragged', p.lat, p.lng);
      });
      // Click en el mapa también mueve el pin.
      map.on('click', (e) => {
        marker.setLngLat(e.lngLat);
        dotNetRef.invokeMethodAsync('OnPinDragged', e.lngLat.lat, e.lngLat.lng);
      });
    }

    map.on('load', () => map.resize());

    this._maps[elementId] = { map, marker };
  },

  setPosition(elementId, lat, lng, zoom) {
    const inst = this._maps[elementId];
    if (!inst) return;
    inst.marker.setLngLat([lng, lat]);
    if (zoom != null) inst.map.flyTo({ center: [lng, lat], zoom });
    else inst.map.setCenter([lng, lat]);
  },

  dispose(elementId) {
    const inst = this._maps[elementId];
    if (!inst) return;
    try { inst.map.remove(); } catch (e) { /* ya removido */ }
    delete this._maps[elementId];
  },
};

// ── Persistencia simple (borrador del wizard) ───────────────────────────────
window.pmStorage = {
  get(key)      { try { return localStorage.getItem(key); } catch { return null; } },
  set(key, val) { try { localStorage.setItem(key, val); } catch { /* cuota llena */ } },
  remove(key)   { try { localStorage.removeItem(key); } catch { /* noop */ } },
};
