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
      style: 'mapbox://styles/mapbox/light-v11',
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

  // ── Poner/actualizar markers ───────────────────────────────────────────────
  setMarkers(listings, dotNetRef) {
    if (!this._map) return;
    this._dotNetRef = dotNetRef;
    this.clearMarkers();

    listings.forEach(l => {
      const el = document.createElement('div');
      el.className = 'pm-marker';
      el.textContent = this._fmtPrice(l.precio, l.moneda);

      el.addEventListener('click', () => {
        if (this._dotNetRef) {
          this._dotNetRef.invokeMethodAsync('OnMarkerClick', [l.id]);
        }
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
  _fmtPrice(precio, moneda) {
    if (!precio) return '?';
    const sym = moneda === 'USD' ? 'U$S' : '$';
    if (precio >= 1_000_000) return `${sym}${(precio / 1_000_000).toFixed(1)}M`;
    if (precio >= 1_000)     return `${sym}${Math.round(precio / 1_000)}k`;
    return `${sym}${precio}`;
  },
};
