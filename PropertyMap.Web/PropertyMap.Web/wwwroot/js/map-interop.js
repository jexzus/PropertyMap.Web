// PropertyMap — Mapbox GL JS Interop

window.mapInterop = {

  _map: null,
  _markers: new Map(),   // id → { marker: mapboxgl.Marker, data }
  _dotNetRef: null,
  _selectedId: null,

  // ── Inicializar el mapa ────────────────────────────────────────────────────
  initMap(elementId, lat, lng, zoom) {
    if (typeof mapboxgl === 'undefined') { console.warn('Mapbox GL no cargó'); return; }
    if (this._map) {
      this._map.resize();
      return;
    }

    mapboxgl.accessToken = window.MAPBOX_TOKEN;

    this._map = new mapboxgl.Map({
      container: elementId,
      style: 'mapbox://styles/mapbox/streets-v12',
      center: [lng, lat],
      zoom: zoom ?? 13,
      attributionControl: true,
    });

    this._map.addControl(new mapboxgl.NavigationControl(), 'bottom-right');
  },

  // ── Centrar en ubicación del usuario ──────────────────────────────────────
  centerOnUser() {
    if (!this._map || !navigator.geolocation) return;
    navigator.geolocation.getCurrentPosition(
      pos => this._map.flyTo({ center: [pos.coords.longitude, pos.coords.latitude], zoom: 14 }),
      () => {}
    );
  },

  // ── Geocodificar y volar al lugar ─────────────────────────────────────────
  async geocodeAndFly(query) {
    if (!this._map || !query) return;
    const token = window.MAPBOX_TOKEN;
    const encoded = encodeURIComponent(query);
    const url = `https://api.mapbox.com/geocoding/v5/mapbox.places/${encoded}.json?access_token=${token}&language=es&country=AR&limit=1`;
    try {
      const res = await fetch(url);
      const json = await res.json();
      const feature = json.features?.[0];
      if (feature) {
        const [lng, lat] = feature.center;
        this._map.flyTo({ center: [lng, lat], zoom: 13, duration: 1200 });
      }
    } catch (e) {
      console.warn('Geocoding error', e);
    }
  },

  // ── Cambiar style del mapa ────────────────────────────────────────────────
  setStyle(styleUrl) {
    if (!this._map) return;
    // Guardar markers actuales para re-agregarlos después del style load
    const currentMarkers = new Map(this._markers);
    this._map.setStyle(styleUrl);
    this._map.once('styledata', () => {
      currentMarkers.forEach(({ marker }) => marker.addTo(this._map));
    });
  },

  // ── Poner/actualizar markers ───────────────────────────────────────────────
  setMarkers(listings, dotNetRef) {
    if (!this._map) return;
    this._dotNetRef = dotNetRef;
    this.clearMarkers();

    listings.forEach(l => {
      const el = this._createHouseMarker(l.operacion);

      el.addEventListener('click', () => {
        if (this._dotNetRef)
          this._dotNetRef.invokeMethodAsync('OnMarkerClick', [l.id]);
      });

      el.addEventListener('mouseenter', () => {
        if (this._dotNetRef)
          this._dotNetRef.invokeMethodAsync('OnMarkerHover', l.id);
      });

      el.addEventListener('mouseleave', () => {
        if (this._dotNetRef)
          this._dotNetRef.invokeMethodAsync('OnMarkerHover', -1);
      });

      const marker = new mapboxgl.Marker({ element: el, anchor: 'bottom' })
        .setLngLat([l.lng, l.lat])
        .addTo(this._map);

      this._markers.set(l.id, { marker, data: l });
    });
  },

  // ── Resaltar marker seleccionado (click) ──────────────────────────────────
  selectMarker(id) {
    this._selectedId = id;
    this._markers.forEach(({ marker }, markerId) => {
      const el = marker.getElement();
      if (el) el.classList.toggle('selected', markerId === id);
    });
  },

  // ── Highlight marker desde hover de card ──────────────────────────────────
  highlightMarker(id) {
    this._markers.forEach(({ marker }, markerId) => {
      const el = marker.getElement();
      if (!el) return;
      if (id === null || id === undefined) {
        el.classList.remove('hovered');
      } else {
        el.classList.toggle('hovered', markerId === id);
      }
    });
  },

  // ── Limpiar todos los markers ──────────────────────────────────────────────
  clearMarkers() {
    this._markers.forEach(({ marker }) => marker.remove());
    this._markers.clear();
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
