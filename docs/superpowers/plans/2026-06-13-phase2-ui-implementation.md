# PropertyMap Phase 2 UI — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Transformar la UI de PropertyMap en una experiencia estilo Zillow: markers de casa coloreados por operación, toggle satélite/calle en mapa, style Mapbox más colorido, navbar limpio con logo+auth, barra de filtros completa debajo del navbar, y modal de búsqueda inicial.

**Architecture:** Home.razor orquesta el estado (filtros, modal, listings); los componentes Navbar, FilterBar y SearchModal son "dumb" (reciben parámetros, emiten callbacks). La lógica de mapa vive en map-interop.js (puro JS), llamada desde MapView.razor via IJSRuntime. Los markers son divs con SVG inline generado en JS, coloreados vía CSS classes.

**Tech Stack:** Blazor Web App .NET 9 · Mapbox GL JS v3.4.0 · CSS custom (OKLCH tokens) · ASP.NET Core Identity

---

## Mapa de archivos

| Acción | Archivo |
|--------|---------|
| Modify | `PropertyMap.Core/Enums/TipoPropiedad.cs` |
| Modify | `PropertyMap.Web/wwwroot/js/map-interop.js` |
| Modify | `PropertyMap.Web/wwwroot/app.css` |
| Modify | `PropertyMap.Web/Components/Map/MapView.razor` |
| Modify | `PropertyMap.Web/Components/Pages/Home.razor` |
| Create | `PropertyMap.Web/Components/Layout/Navbar.razor` |
| Create | `PropertyMap.Web/Components/Listings/FilterBar.razor` |
| Create | `PropertyMap.Web/Components/Listings/SearchModal.razor` |

Paths completos parten de: `C:\Agentes\PropertyMap\src\PropertyMap.Web\PropertyMap.Web\`

---

## Task 1: Ampliar enum TipoPropiedad

**Files:**
- Modify: `C:\Agentes\PropertyMap\src\PropertyMap.Core\Enums\TipoPropiedad.cs`

- [ ] **Step 1: Reemplazar el enum con los valores completos**

```csharp
namespace PropertyMap.Core.Enums;

public enum TipoPropiedad
{
    Departamento,
    Casa,
    Duplex,
    PH,
    Complejo,
    Terreno,
    Campo,
    Local,
    Oficina,
    Cochera,
    Otro
}
```

- [ ] **Step 2: Build para verificar que el seed y el resto compilan**

Desde terminal (parado en `C:\Agentes\PropertyMap\src`):
```
dotnet build PropertyMap.sln
```
Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 3: Commit**

```bash
git add PropertyMap.Core/Enums/TipoPropiedad.cs
git commit -m "feat: extend TipoPropiedad enum with Duplex, PH, Complejo, Campo, Cochera"
```

---

## Task 2: Markers SVG de casa coloreados por operación

**Files:**
- Modify: `C:\Agentes\PropertyMap\src\PropertyMap.Web\PropertyMap.Web\wwwroot\js\map-interop.js`
- Modify: `C:\Agentes\PropertyMap\src\PropertyMap.Web\PropertyMap.Web\wwwroot\app.css`

- [ ] **Step 1: Reemplazar la función `setMarkers` en map-interop.js**

Reemplazar el bloque `setMarkers` completo con:

```js
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
```

- [ ] **Step 2: Agregar helper `_createHouseMarker` antes de `_fmtPrice`**

```js
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
```

- [ ] **Step 3: Actualizar estilos de markers en app.css**

Buscar el bloque `.pm-marker` existente y reemplazarlo completo:

```css
/* ── Map Markers ──────────────────────────────────────────────── */
.pm-marker {
  cursor: pointer;
  transition: transform 200ms cubic-bezier(0.16, 1, 0.3, 1),
              filter 200ms ease;
  transform-origin: bottom center;
  filter: drop-shadow(0 2px 4px rgba(0,0,0,0.25));
}

.pm-marker:hover,
.pm-marker.hovered {
  transform: scale(1.3);
  filter: drop-shadow(0 4px 10px rgba(0,0,0,0.35));
  z-index: 10;
}

.pm-marker.selected {
  transform: scale(1.4);
  filter: drop-shadow(0 4px 14px rgba(0,0,0,0.45));
  z-index: 20;
}

.pm-marker--venta svg path:first-child     { stroke-width: 2; }
.pm-marker--alquiler svg path:first-child  { stroke-width: 2; }
.pm-marker--temporario svg path:first-child { stroke-width: 2; }
```

- [ ] **Step 4: Arrancar la app y verificar visualmente**

```
dotnet run --project PropertyMap.Web/PropertyMap.Web
```

Abrir http://localhost:5xxx — los markers deben mostrar íconos de casa: rojos (crimson) para venta, verdes para alquiler, morados para temporario.

- [ ] **Step 5: Commit**

```bash
git add PropertyMap.Web/PropertyMap.Web/wwwroot/js/map-interop.js
git add PropertyMap.Web/PropertyMap.Web/wwwroot/app.css
git commit -m "feat: replace oval markers with house SVG icons, color-coded by operation type"
```

---

## Task 3: Mapbox style más colorido + toggle satélite/calle

**Files:**
- Modify: `C:\Agentes\PropertyMap\src\PropertyMap.Web\PropertyMap.Web\wwwroot\js\map-interop.js`
- Modify: `C:\Agentes\PropertyMap\src\PropertyMap.Web\PropertyMap.Web\Components\Map\MapView.razor`
- Modify: `C:\Agentes\PropertyMap\src\PropertyMap.Web\PropertyMap.Web\wwwroot\app.css`

- [ ] **Step 1: Cambiar style inicial en `initMap` y agregar `setStyle`**

En `initMap`, cambiar la línea del style:
```js
style: 'mapbox://styles/mapbox/streets-v12',
```

Luego agregar después de `initResize`:
```js
  setStyle(styleUrl) {
    if (!this._map) return;
    this._map.setStyle(styleUrl);
    // Re-render markers after style load (setStyle clears them)
    this._map.once('styledata', () => {
      this._markers.forEach(({ marker }) => marker.addTo(this._map));
    });
  },
```

- [ ] **Step 2: Agregar JSInvokable `SetMapStyle` y botón toggle en MapView.razor**

Agregar dentro del `<div class="map-wrapper">`, justo antes de `@if (popupListing is not null)`:

```razor
<div class="map-style-toggle">
    <button class="style-btn @(isSatellite ? "" : "active")"
            @onclick='() => ToggleStyle(false)'
            title="Vista de calles">
        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <rect x="3" y="3" width="18" height="18" rx="2"/><path d="M3 9h18M9 21V9"/>
        </svg>
        Calle
    </button>
    <button class="style-btn @(isSatellite ? "active" : "")"
            @onclick='() => ToggleStyle(true)'
            title="Vista satelital">
        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <circle cx="12" cy="12" r="10"/><path d="M2 12h20M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10z"/>
        </svg>
        Satélite
    </button>
</div>
```

Agregar en el bloque `@code` de MapView.razor:
```csharp
private bool isSatellite;

private async Task ToggleStyle(bool satellite)
{
    isSatellite = satellite;
    var style = satellite
        ? "mapbox://styles/mapbox/satellite-streets-v12"
        : "mapbox://styles/mapbox/streets-v12";
    await JS.InvokeVoidAsync("mapInterop.setStyle", style);
}
```

- [ ] **Step 3: Agregar CSS para el toggle en app.css**

```css
/* ── Map Style Toggle ─────────────────────────────────────────── */
.map-style-toggle {
  position: absolute;
  bottom: 120px;
  right: 12px;
  display: flex;
  flex-direction: column;
  gap: 2px;
  background: white;
  border-radius: 8px;
  padding: 4px;
  box-shadow: 0 2px 8px rgba(0,0,0,0.18);
  z-index: 10;
}

.style-btn {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 6px 10px;
  font-size: var(--text-xs);
  font-weight: 500;
  color: var(--text-secondary);
  background: transparent;
  border: none;
  border-radius: 6px;
  cursor: pointer;
  white-space: nowrap;
  transition: background 150ms, color 150ms;
}

.style-btn:hover    { background: var(--surface-raised); color: var(--text-primary); }
.style-btn.active   { background: var(--accent); color: white; }
```

- [ ] **Step 4: Verificar visualmente**

Con la app corriendo, verificar:
- El mapa ahora muestra calles en color (azul para agua, verde para parques, etc.)
- Los botones "Calle" / "Satélite" aparecen sobre el mapa (abajo a la derecha)
- Click "Satélite" → cambia a vista satelital con nombres de calles
- Click "Calle" → vuelve a streets-v12

- [ ] **Step 5: Commit**

```bash
git add PropertyMap.Web/PropertyMap.Web/wwwroot/js/map-interop.js
git add PropertyMap.Web/PropertyMap.Web/Components/Map/MapView.razor
git add PropertyMap.Web/PropertyMap.Web/wwwroot/app.css
git commit -m "feat: colorful map style (streets-v12) and satellite/street toggle"
```

---

## Task 4: Hover marker → highlight card (dirección JS→Blazor)

**Files:**
- Modify: `C:\Agentes\PropertyMap\src\PropertyMap.Web\PropertyMap.Web\Components\Map\MapView.razor`
- Modify: `C:\Agentes\PropertyMap\src\PropertyMap.Web\PropertyMap.Web\Components\Pages\Home.razor`

- [ ] **Step 1: Agregar `[JSInvokable] OnMarkerHover` en MapView.razor**

Agregar en el bloque `@code`, después de `OnMarkerClick`:

```csharp
[JSInvokable]
public async Task OnMarkerHover(int id)
{
    var hoveredId = id == -1 ? (int?)null : id;
    await OnMarkerHovered.InvokeAsync(hoveredId);
}
```

Agregar el parámetro en el bloque `[Parameter]`:
```csharp
[Parameter] public EventCallback<int?> OnMarkerHovered { get; set; }
```

- [ ] **Step 2: Wiring en Home.razor**

En el tag `<MapView>` dentro de Home.razor, agregar:
```razor
OnMarkerHovered="HoverListing"
```

(El método `HoverListing(int? id)` ya existe en Home.razor y actualiza `hoveredListingId`.)

- [ ] **Step 3: Verificar sincronización bidireccional**

Con la app corriendo:
- Hover sobre una card → el marker correspondiente se agranda y cambia color ✓
- Hover sobre un marker → la card correspondiente se resalta con borde azul ✓

- [ ] **Step 4: Commit**

```bash
git add PropertyMap.Web/PropertyMap.Web/Components/Map/MapView.razor
git add PropertyMap.Web/PropertyMap.Web/Components/Pages/Home.razor
git commit -m "feat: bidirectional hover sync between map markers and listing cards"
```

---

## Task 5: Navbar.razor — logo + autenticación

**Files:**
- Create: `C:\Agentes\PropertyMap\src\PropertyMap.Web\PropertyMap.Web\Components\Layout\Navbar.razor`
- Modify: `C:\Agentes\PropertyMap\src\PropertyMap.Web\PropertyMap.Web\wwwroot\app.css`

- [ ] **Step 1: Crear Navbar.razor**

```razor
@using Microsoft.AspNetCore.Components.Authorization

<nav class="pm-navbar" role="navigation" aria-label="Navegación principal">
    <a href="/" class="pm-navbar__logo" aria-label="PropertyMap — Inicio">
        PropertyMap
    </a>

    <div class="pm-navbar__actions">
        <AuthorizeView>
            <Authorized>
                <AuthorizeView Roles="Publisher">
                    <Authorized>
                        <a href="/publisher/dashboard" class="btn-ghost">Mi panel</a>
                        <a href="/publisher/dashboard" class="btn-primary">Publicar</a>
                    </Authorized>
                    <NotAuthorized>
                        <a href="/publisher/upgrade" class="btn-ghost">Publicar</a>
                    </NotAuthorized>
                </AuthorizeView>
            </Authorized>
            <NotAuthorized>
                <a href="/Account/Login" class="btn-ghost">Iniciar sesión</a>
                <a href="/Account/Register" class="btn-primary">Publicar</a>
            </NotAuthorized>
        </AuthorizeView>
    </div>
</nav>
```

- [ ] **Step 2: Actualizar CSS del navbar en app.css**

Buscar el bloque `.pm-navbar` existente y reemplazarlo:

```css
/* ── Navbar ──────────────────────────────────────────────────── */
.pm-navbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 20px;
  height: 56px;
  background: white;
  border-bottom: 1px solid var(--border);
  position: sticky;
  top: 0;
  z-index: 100;
  flex-shrink: 0;
}

.pm-navbar__logo {
  font-size: var(--text-lg);
  font-weight: 700;
  color: var(--text-primary);
  text-decoration: none;
  letter-spacing: -0.02em;
}

.pm-navbar__logo:hover { color: var(--accent); }

.pm-navbar__actions {
  display: flex;
  align-items: center;
  gap: 8px;
}
```

Asegurarse que `.pm-navbar__filters` ya no está en el navbar (se eliminan esas reglas).

- [ ] **Step 3: Commit**

```bash
git add PropertyMap.Web/PropertyMap.Web/Components/Layout/Navbar.razor
git add PropertyMap.Web/PropertyMap.Web/wwwroot/app.css
git commit -m "feat: add Navbar component with logo and auth buttons"
```

---

## Task 6: Geocodificación en map-interop.js

**Files:**
- Modify: `C:\Agentes\PropertyMap\src\PropertyMap.Web\PropertyMap.Web\wwwroot\js\map-interop.js`

- [ ] **Step 1: Agregar función `geocodeAndFly` en map-interop.js**

Agregar después de `centerOnUser`:

```js
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
```

- [ ] **Step 2: Commit**

```bash
git add PropertyMap.Web/PropertyMap.Web/wwwroot/js/map-interop.js
git commit -m "feat: add geocodeAndFly for address/city search via Mapbox Geocoding API"
```

---

## Task 7: FilterBar.razor — barra de filtros bajo el navbar

**Files:**
- Create: `C:\Agentes\PropertyMap\src\PropertyMap.Web\PropertyMap.Web\Components\Listings\FilterBar.razor`
- Modify: `C:\Agentes\PropertyMap\src\PropertyMap.Web\PropertyMap.Web\wwwroot\app.css`

- [ ] **Step 1: Crear FilterBar.razor**

```razor
@using PropertyMap.Core.Enums
@inject IJSRuntime JS

<div class="filter-bar" role="search" aria-label="Filtros de búsqueda">

    @* ── Búsqueda texto ── *@
    <div class="filter-search-wrap">
        <svg class="filter-search-icon" width="16" height="16" viewBox="0 0 24 24" fill="none"
             stroke="currentColor" stroke-width="2" aria-hidden="true">
            <circle cx="11" cy="11" r="8"/><line x1="21" y1="21" x2="16.65" y2="16.65"/>
        </svg>
        <input class="filter-search-input"
               type="text"
               placeholder="Ciudad, barrio o dirección..."
               @bind="searchText"
               @onkeydown="HandleSearchKey"
               aria-label="Buscar ciudad o dirección" />
        @if (!string.IsNullOrWhiteSpace(searchText))
        {
            <button class="filter-search-clear" @onclick="ClearSearch" aria-label="Limpiar búsqueda">×</button>
        }
    </div>

    @* ── Operación ── *@
    <select class="filter-select @(!string.IsNullOrEmpty(Operacion) ? "has-value" : "")"
            value="@Operacion"
            @onchange="e => EmitOperacion(e.Value?.ToString() ?? "")"
            aria-label="Tipo de operación">
        <option value="">Operación</option>
        <option value="Venta">En venta</option>
        <option value="Alquiler">En alquiler</option>
        <option value="AlquilerTemporario">Temporario</option>
    </select>

    @* ── Precio ── *@
    <select class="filter-select @(!string.IsNullOrEmpty(MaxPrecio) ? "has-value" : "")"
            value="@MaxPrecio"
            @onchange="e => EmitMaxPrecio(e.Value?.ToString() ?? "")"
            aria-label="Precio máximo">
        <option value="">Precio máx.</option>
        <option value="50000">USD 50.000</option>
        <option value="100000">USD 100.000</option>
        <option value="200000">USD 200.000</option>
        <option value="500000">USD 500.000</option>
        <option value="1000000">USD 1.000.000</option>
    </select>

    @* ── Dormitorios ── *@
    <select class="filter-select @(Dormitorios.HasValue ? "has-value" : "")"
            value="@(Dormitorios?.ToString() ?? "")"
            @onchange="e => EmitDormitorios(e.Value?.ToString() ?? "")"
            aria-label="Dormitorios">
        <option value="">Dormitorios</option>
        <option value="1">1 dorm.</option>
        <option value="2">2 dorm.</option>
        <option value="3">3 dorm.</option>
        <option value="4">4+ dorm.</option>
    </select>

    @* ── Baños ── *@
    <select class="filter-select @(Banos.HasValue ? "has-value" : "")"
            value="@(Banos?.ToString() ?? "")"
            @onchange="e => EmitBanos(e.Value?.ToString() ?? "")"
            aria-label="Baños">
        <option value="">Baños</option>
        <option value="1">1 baño</option>
        <option value="2">2 baños</option>
        <option value="3">3+ baños</option>
    </select>

    @* ── Tipo de propiedad ── *@
    <select class="filter-select @(!string.IsNullOrEmpty(TipoProp) ? "has-value" : "")"
            value="@TipoProp"
            @onchange="e => EmitTipo(e.Value?.ToString() ?? "")"
            aria-label="Tipo de propiedad">
        <option value="">Tipo</option>
        @foreach (var tipo in Enum.GetValues<TipoPropiedad>())
        {
            <option value="@tipo">@tipo</option>
        }
    </select>

    @* ── Limpiar filtros ── *@
    @if (HasActiveFilters)
    {
        <button class="filter-clear-btn" @onclick="ClearAllFilters" aria-label="Limpiar todos los filtros">
            Limpiar
        </button>
    }
</div>

@code {
    [Parameter] public string Operacion { get; set; } = "";
    [Parameter] public string MaxPrecio { get; set; } = "";
    [Parameter] public int? Dormitorios { get; set; }
    [Parameter] public int? Banos { get; set; }
    [Parameter] public string TipoProp { get; set; } = "";

    [Parameter] public EventCallback<string> OnOperacionChanged { get; set; }
    [Parameter] public EventCallback<string> OnMaxPrecioChanged { get; set; }
    [Parameter] public EventCallback<int?> OnDormitoriosChanged { get; set; }
    [Parameter] public EventCallback<int?> OnBanosChanged { get; set; }
    [Parameter] public EventCallback<string> OnTipoChanged { get; set; }
    [Parameter] public EventCallback OnClearAll { get; set; }

    private string searchText = "";

    private bool HasActiveFilters =>
        !string.IsNullOrEmpty(Operacion) || !string.IsNullOrEmpty(MaxPrecio) ||
        Dormitorios.HasValue || Banos.HasValue || !string.IsNullOrEmpty(TipoProp);

    private async Task HandleSearchKey(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && !string.IsNullOrWhiteSpace(searchText))
            await JS.InvokeVoidAsync("mapInterop.geocodeAndFly", searchText);
    }

    private async Task ClearSearch()
    {
        searchText = "";
        await Task.CompletedTask;
    }

    private async Task EmitOperacion(string v)  => await OnOperacionChanged.InvokeAsync(v);
    private async Task EmitMaxPrecio(string v)  => await OnMaxPrecioChanged.InvokeAsync(v);
    private async Task EmitTipo(string v)        => await OnTipoChanged.InvokeAsync(v);

    private async Task EmitDormitorios(string v) =>
        await OnDormitoriosChanged.InvokeAsync(int.TryParse(v, out var n) ? n : null);

    private async Task EmitBanos(string v) =>
        await OnBanosChanged.InvokeAsync(int.TryParse(v, out var n) ? n : null);

    private async Task ClearAllFilters() => await OnClearAll.InvokeAsync();
}
```

- [ ] **Step 2: Agregar CSS de FilterBar en app.css**

```css
/* ── Filter Bar ───────────────────────────────────────────────── */
.filter-bar {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 10px 20px;
  background: white;
  border-bottom: 1px solid var(--border);
  flex-shrink: 0;
  flex-wrap: wrap;
  position: sticky;
  top: 56px;   /* altura del navbar */
  z-index: 99;
}

.filter-search-wrap {
  position: relative;
  display: flex;
  align-items: center;
  flex: 1;
  min-width: 200px;
  max-width: 340px;
}

.filter-search-icon {
  position: absolute;
  left: 10px;
  color: var(--text-tertiary);
  pointer-events: none;
}

.filter-search-input {
  width: 100%;
  padding: 8px 32px 8px 34px;
  font-size: var(--text-sm);
  font-family: inherit;
  border: 1.5px solid var(--border);
  border-radius: 8px;
  background: var(--surface);
  color: var(--text-primary);
  outline: none;
  transition: border-color 150ms;
}

.filter-search-input:focus { border-color: var(--accent); }

.filter-search-clear {
  position: absolute;
  right: 8px;
  background: none;
  border: none;
  font-size: 16px;
  color: var(--text-tertiary);
  cursor: pointer;
  padding: 0 4px;
  line-height: 1;
}

.filter-select {
  padding: 8px 28px 8px 12px;
  font-size: var(--text-sm);
  font-family: inherit;
  border: 1.5px solid var(--border);
  border-radius: 8px;
  background: var(--surface) url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='10' height='6' viewBox='0 0 10 6'%3E%3Cpath d='M1 1l4 4 4-4' stroke='%239ca3af' stroke-width='1.5' fill='none' stroke-linecap='round'/%3E%3C/svg%3E") no-repeat right 10px center;
  -webkit-appearance: none;
  appearance: none;
  color: var(--text-secondary);
  cursor: pointer;
  outline: none;
  transition: border-color 150ms;
  white-space: nowrap;
}

.filter-select:focus   { border-color: var(--accent); }
.filter-select.has-value {
  border-color: var(--accent);
  color: var(--text-primary);
  background-color: oklch(0.97 0.01 220);
}

.filter-clear-btn {
  padding: 8px 14px;
  font-size: var(--text-sm);
  font-weight: 500;
  color: var(--text-secondary);
  background: none;
  border: 1.5px solid var(--border);
  border-radius: 8px;
  cursor: pointer;
  transition: all 150ms;
  white-space: nowrap;
}

.filter-clear-btn:hover {
  border-color: var(--text-secondary);
  color: var(--text-primary);
}
```

- [ ] **Step 3: Commit**

```bash
git add PropertyMap.Web/PropertyMap.Web/Components/Listings/FilterBar.razor
git add PropertyMap.Web/PropertyMap.Web/wwwroot/app.css
git commit -m "feat: FilterBar component with search, operation, price, bedrooms, bathrooms, property type"
```

---

## Task 8: SearchModal.razor — modal de búsqueda inicial

**Files:**
- Create: `C:\Agentes\PropertyMap\src\PropertyMap.Web\PropertyMap.Web\Components\Listings\SearchModal.razor`
- Modify: `C:\Agentes\PropertyMap\src\PropertyMap.Web\PropertyMap.Web\wwwroot\app.css`

- [ ] **Step 1: Crear SearchModal.razor**

```razor
@using PropertyMap.Core.Enums
@inject IJSRuntime JS

@if (IsVisible)
{
    <div class="search-modal-overlay" @onclick="HandleOverlayClick" role="dialog" aria-modal="true" aria-label="Búsqueda de propiedades">
        <div class="search-modal" @onclick:stopPropagation="true">

            <button class="search-modal-close" @onclick="Close" aria-label="Cerrar">
                <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5">
                    <line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/>
                </svg>
            </button>

            <div class="search-modal-header">
                <h2 class="search-modal-title">¿Qué estás buscando?</h2>
                <p class="search-modal-subtitle">Encontrá tu próxima propiedad</p>
            </div>

            <div class="search-modal-fields">

                @* Lugar *@
                <div class="smodal-field">
                    <label class="smodal-label">¿Dónde querés buscar?</label>
                    <div class="filter-search-wrap" style="max-width:100%">
                        <svg class="filter-search-icon" width="16" height="16" viewBox="0 0 24 24" fill="none"
                             stroke="currentColor" stroke-width="2" aria-hidden="true">
                            <circle cx="11" cy="11" r="8"/><line x1="21" y1="21" x2="16.65" y2="16.65"/>
                        </svg>
                        <input class="filter-search-input" style="max-width:100%"
                               type="text"
                               placeholder="Ciudad, barrio, dirección..."
                               @bind="lugar" />
                    </div>
                </div>

                @* Operación *@
                <div class="smodal-field">
                    <label class="smodal-label">Tipo de operación</label>
                    <div class="smodal-toggle-group">
                        @foreach (var op in new[] { ("Venta", "Comprar"), ("Alquiler", "Alquilar"), ("AlquilerTemporario", "Temporario") })
                        {
                            var opVal = op.Item1;
                            <button class="smodal-toggle @(operacion == opVal ? "active" : "")"
                                    @onclick="() => operacion = opVal">
                                @op.Item2
                            </button>
                        }
                    </div>
                </div>

                @* Tipo de propiedad *@
                <div class="smodal-field">
                    <label class="smodal-label">Tipo de propiedad</label>
                    <select class="filter-select" style="width:100%"
                            @bind="tipoProp"
                            aria-label="Tipo de propiedad">
                        <option value="">Cualquier tipo</option>
                        @foreach (var tipo in Enum.GetValues<TipoPropiedad>())
                        {
                            <option value="@tipo">@tipo</option>
                        }
                    </select>
                </div>
            </div>

            <button class="smodal-submit btn-primary" @onclick="Submit">
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" aria-hidden="true">
                    <circle cx="11" cy="11" r="8"/><line x1="21" y1="21" x2="16.65" y2="16.65"/>
                </svg>
                Buscar propiedades
            </button>
        </div>
    </div>
}

@code {
    [Parameter] public bool IsVisible { get; set; }
    [Parameter] public EventCallback<SearchCriteria> OnSearch { get; set; }

    private string lugar = "";
    private string operacion = "";
    private string tipoProp = "";

    private async Task Submit()
    {
        await OnSearch.InvokeAsync(new SearchCriteria(lugar, operacion, tipoProp));
        await JS.InvokeVoidAsync("sessionStorage.setItem", "pm_searched", "1");
    }

    private async Task Close()
    {
        await OnSearch.InvokeAsync(new SearchCriteria("", "", ""));
        await JS.InvokeVoidAsync("sessionStorage.setItem", "pm_searched", "1");
    }

    private async Task HandleOverlayClick() => await Close();

    public record SearchCriteria(string Lugar, string Operacion, string TipoProp);
}
```

- [ ] **Step 2: Agregar CSS del modal en app.css**

```css
/* ── Search Modal ─────────────────────────────────────────────── */
.search-modal-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0,0,0,0.45);
  backdrop-filter: blur(4px);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 200;
  animation: overlay-in 200ms ease;
}

@keyframes overlay-in {
  from { opacity: 0; }
  to   { opacity: 1; }
}

.search-modal {
  position: relative;
  background: white;
  border-radius: 20px;
  padding: 36px 32px 28px;
  width: 100%;
  max-width: 480px;
  margin: 16px;
  box-shadow: 0 24px 60px rgba(0,0,0,0.22);
  animation: modal-in 250ms cubic-bezier(0.16, 1, 0.3, 1);
}

@keyframes modal-in {
  from { transform: translateY(20px) scale(0.97); opacity: 0; }
  to   { transform: translateY(0) scale(1); opacity: 1; }
}

.search-modal-close {
  position: absolute;
  top: 16px;
  right: 16px;
  width: 32px;
  height: 32px;
  display: flex;
  align-items: center;
  justify-content: center;
  background: var(--surface);
  border: none;
  border-radius: 50%;
  cursor: pointer;
  color: var(--text-secondary);
  transition: background 150ms;
}
.search-modal-close:hover { background: var(--surface-raised); color: var(--text-primary); }

.search-modal-header { margin-bottom: 24px; }
.search-modal-title  { font-size: var(--text-xl); font-weight: 700; margin: 0 0 4px; }
.search-modal-subtitle { font-size: var(--text-sm); color: var(--text-secondary); margin: 0; }

.search-modal-fields { display: flex; flex-direction: column; gap: 18px; }

.smodal-field  { display: flex; flex-direction: column; gap: 6px; }
.smodal-label  { font-size: var(--text-sm); font-weight: 600; color: var(--text-primary); }

.smodal-toggle-group { display: flex; gap: 8px; }
.smodal-toggle {
  flex: 1;
  padding: 10px 8px;
  font-size: var(--text-sm);
  font-weight: 500;
  border: 1.5px solid var(--border);
  border-radius: 10px;
  background: var(--surface);
  color: var(--text-secondary);
  cursor: pointer;
  transition: all 150ms;
  text-align: center;
}
.smodal-toggle:hover  { border-color: var(--accent); color: var(--accent); }
.smodal-toggle.active { border-color: var(--accent); background: var(--accent); color: white; }

.smodal-submit {
  width: 100%;
  margin-top: 24px;
  padding: 14px;
  font-size: var(--text-base);
  font-weight: 600;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  border-radius: 12px;
}
```

- [ ] **Step 3: Commit**

```bash
git add PropertyMap.Web/PropertyMap.Web/Components/Listings/SearchModal.razor
git add PropertyMap.Web/PropertyMap.Web/wwwroot/app.css
git commit -m "feat: SearchModal onboarding component with location, operation type, property type"
```

---

## Task 9: Refactorizar Home.razor — integrar todos los componentes

**Files:**
- Modify: `C:\Agentes\PropertyMap\src\PropertyMap.Web\PropertyMap.Web\Components\Pages\Home.razor`

- [ ] **Step 1: Reemplazar Home.razor completo**

```razor
@page "/"
@using Microsoft.AspNetCore.Components.Authorization
@using PropertyMap.Core.Entities
@using PropertyMap.Core.Enums
@using PropertyMap.Core.Interfaces
@using PropertyMap.Core.DTOs
@inject IListingRepository ListingRepo
@inject IJSRuntime JS
@rendermode InteractiveServer

<PageTitle>PropertyMap — Propiedades en el mapa</PageTitle>

<div class="app-shell">

    <Navbar />

    <FilterBar
        Operacion="@filtroOperacion"
        MaxPrecio="@filtroMaxPrecioStr"
        Dormitorios="@filtroDormitorios"
        Banos="@filtroBanos"
        TipoProp="@filtroTipo"
        OnOperacionChanged="v => { filtroOperacion = v; ApplyFilters(); }"
        OnMaxPrecioChanged="v => { filtroMaxPrecioStr = v; ApplyFilters(); }"
        OnDormitoriosChanged="v => { filtroDormitorios = v; ApplyFilters(); }"
        OnBanosChanged="v => { filtroBanos = v; ApplyFilters(); }"
        OnTipoChanged="v => { filtroTipo = v; ApplyFilters(); }"
        OnClearAll="ClearAllFilters" />

    @if (loadError is not null)
    {
        <div style="padding:2rem;color:red;font-family:monospace">Error: @loadError</div>
    }

    <div class="split-layout">

        <ListingsPanel
            Listings="filteredListings"
            SelectedListingId="selectedListingId"
            HoveredListingId="hoveredListingId"
            IsCollapsed="isPanelCollapsed"
            IsLoading="isLoading"
            OnListingSelected="SelectListing"
            OnListingHover="HoverListing"
            OnToggleCollapse="TogglePanel" />

        @if (!isPanelCollapsed)
        {
            <div id="resize-handle" class="resize-handle" role="separator"
                 aria-label="Redimensionar panel" aria-orientation="vertical"></div>
        }

        <div style="position:relative;flex:1;overflow:hidden">
            @if (isPanelCollapsed)
            {
                <button class="btn-panel-toggle" @onclick="TogglePanel" aria-label="Mostrar listado">
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <polyline points="9 18 15 12 9 6"/>
                    </svg>
                    @filteredListings.Count prop.
                </button>
            }
            <MapView @ref="mapView"
                Listings="mapListings"
                SelectedListingId="selectedListingId"
                OnListingSelected="SelectListing"
                OnMarkerHovered="HoverListing" />
        </div>
    </div>

    <SearchModal
        IsVisible="showModal"
        OnSearch="HandleSearchCriteria" />
</div>

@code {
    private List<PropertyListing> allListings = [];
    private List<PropertyListing> filteredListings = [];
    private List<ListingMapDto> mapListings = [];
    private string? loadError;

    private string filtroOperacion = "";
    private string filtroTipo = "";
    private string filtroMaxPrecioStr = "";
    private decimal? filtroMaxPrecio;
    private int? filtroDormitorios;
    private int? filtroBanos;

    private int? selectedListingId;
    private int? hoveredListingId;
    private bool isPanelCollapsed;
    private bool isLoading = true;
    private bool showModal;
    private MapView? mapView;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            isLoading = true;
            allListings = (await ListingRepo.GetActiveListingsAsync()).ToList();
            mapListings = (await ListingRepo.GetActiveListingsForMapAsync()).ToList();
            filteredListings = allListings;
        }
        catch (Exception ex) { loadError = ex.Message; }
        finally { isLoading = false; }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            var searched = await JS.InvokeAsync<string?>("sessionStorage.getItem", "pm_searched");
            showModal = searched is null;
            StateHasChanged();
        }
    }

    private async Task HandleSearchCriteria(SearchModal.SearchCriteria criteria)
    {
        showModal = false;
        if (!string.IsNullOrEmpty(criteria.Operacion)) { filtroOperacion = criteria.Operacion; }
        if (!string.IsNullOrEmpty(criteria.TipoProp))  { filtroTipo = criteria.TipoProp; }
        ApplyFilters();
        if (!string.IsNullOrWhiteSpace(criteria.Lugar))
            await JS.InvokeVoidAsync("mapInterop.geocodeAndFly", criteria.Lugar);
    }

    private void ApplyFilters()
    {
        filtroMaxPrecio = decimal.TryParse(filtroMaxPrecioStr, out var p) ? p : null;

        filteredListings = allListings
            .Where(l => string.IsNullOrEmpty(filtroOperacion) || l.Operacion.ToString() == filtroOperacion)
            .Where(l => string.IsNullOrEmpty(filtroTipo)      || l.TipoPropiedad.ToString() == filtroTipo)
            .Where(l => !filtroMaxPrecio.HasValue              || l.Precio <= filtroMaxPrecio)
            .Where(l => !filtroDormitorios.HasValue            || (l.Dormitorios.HasValue && l.Dormitorios >= filtroDormitorios))
            .Where(l => !filtroBanos.HasValue                  || (l.Banos.HasValue && l.Banos >= filtroBanos))
            .ToList();

        var filteredIds = filteredListings.Select(l => l.Id).ToHashSet();
        mapListings = allListings
            .Where(l => filteredIds.Contains(l.Id))
            .Select(l => new ListingMapDto(
                l.Id, l.Location.Latitud, l.Location.Longitud,
                l.Titulo, l.Precio, l.Moneda,
                l.TipoPropiedad.ToString(), l.Operacion.ToString(),
                l.Fotos.FirstOrDefault()
            )).ToList();
    }

    private void ClearAllFilters()
    {
        filtroOperacion = "";
        filtroTipo = "";
        filtroMaxPrecioStr = "";
        filtroDormitorios = null;
        filtroBanos = null;
        ApplyFilters();
    }

    private void SelectListing(int id) =>
        selectedListingId = selectedListingId == id ? null : id;

    private async Task HoverListing(int? id)
    {
        hoveredListingId = id;
        if (mapView is not null) await mapView.HighlightMarker(id);
    }

    private void TogglePanel() => isPanelCollapsed = !isPanelCollapsed;
}
```

- [ ] **Step 2: Agregar `@using` de SearchModal en _Imports.razor si hace falta**

Verificar que `_Imports.razor` tenga:
```razor
@using PropertyMap.Web.Components.Listings
@using PropertyMap.Web.Components.Layout
```

Si no están, agregarlos.

- [ ] **Step 3: Build**

```
dotnet build PropertyMap.sln
```
Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 4: Arrancar y verificar**

```
dotnet run --project PropertyMap.Web/PropertyMap.Web
```

Verificar:
1. Al abrir la app → aparece el modal de búsqueda encima de todo ✓
2. Escribir ciudad en el modal y hacer click "Buscar" → modal cierra, mapa vuela a la ubicación ✓
3. Al recargar la página → modal NO vuelve a aparecer (sessionStorage) ✓
4. La barra de filtros aparece bajo el navbar con todos los dropdowns ✓
5. Cambiar Operación → cards y markers se filtran ✓
6. Filtrar Dormitorios → funciona ✓
7. Filtrar Baños → funciona ✓
8. Botón "Limpiar" → aparece cuando hay filtros activos, limpia todo ✓
9. Navbar muestra solo logo + Iniciar sesión + Publicar ✓

- [ ] **Step 5: Commit**

```bash
git add PropertyMap.Web/PropertyMap.Web/Components/Pages/Home.razor
git add PropertyMap.Web/PropertyMap.Web/Components/_Imports.razor
git commit -m "feat: integrate Navbar, FilterBar and SearchModal into Home, add baths/bedrooms filters"
```

---

## Self-Review

**Spec coverage:**
- ✓ Modal búsqueda inicial → Task 8 (SearchModal.razor)
- ✓ Barra filtros con texto+dropdowns → Task 7 (FilterBar.razor)
- ✓ Markers casa SVG, color por operación → Task 2
- ✓ Style Mapbox colorido → Task 3 (streets-v12)
- ✓ Toggle satélite/calle → Task 3
- ✓ Navbar logo + auth → Task 5 (Navbar.razor)
- ✓ Filtro dormitorios → Task 7 + Task 9
- ✓ Filtro baños → Task 7 + Task 9
- ✓ Geocodificación texto → mapa → Task 6
- ✓ Hover marker → highlight card → Task 4
- ✓ TipoPropiedad completo → Task 1

**Placeholders:** ninguno.

**Consistencia de tipos:**
- `SearchModal.SearchCriteria` record definido en Task 8, usado en Task 9 ✓
- `FilterBar` parámetros: `Dormitorios int?`, `Banos int?` consistentes con Home.razor ✓
- `OnMarkerHovered EventCallback<int?>` en MapView ↔ `HoverListing(int? id)` en Home ✓
- `mapInterop.geocodeAndFly(query)` definido en Task 6, llamado en Task 8 y Task 9 ✓
