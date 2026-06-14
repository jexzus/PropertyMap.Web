# PropertyMap — Phase 2 Design Spec
**Date:** 2026-06-13  
**Stack:** Blazor Web App .NET 9 · Mapbox GL JS v3.4.0 · EF Core 9 · SQL Server LocalDB  
**CSS:** Custom design system, OKLCH color tokens, Inter font

---

## 1. Contexto y alcance

Phase 2 convierte PropertyMap de un mapa básico en una experiencia de búsqueda de propiedades estilo Zillow/Airbnb. Incluye:

- Rediseño completo de UI (Zillow-style)
- Modal de búsqueda inicial (onboarding)
- Barra de búsqueda y filtros persistentes bajo el navbar
- Markers de mapa con iconos de casa, coloreados por operación
- Cards con carrusel de imágenes y efectos hover 3D
- Sincronización hover card ↔ marker en mapa
- Popup al click en marker con mini-card
- Página de detalle en nueva pestaña
- Seed data real (8 propiedades, Santa Rosa La Pampa)
- Imágenes reales en `wwwroot/images/properties/`
- Navbar con autenticación y botón "Publicar"

---

## 2. Navbar

### Layout
```
[PropertyMap logo]  [Iniciar sesión] [Publicar]
```
El navbar NO tiene barra de búsqueda — esa vive en la barra de filtros (Sección 4), que queda fija debajo del navbar. Esta separación evita duplicar el input de búsqueda y mantiene el navbar limpio.

### Componentes
- **Logo**: link a `/`, tipografía bold, sin ícono. Componente `Navbar.razor` separado (no inline en Home).
- **Iniciar sesión**: `btn-ghost`, link a `/Account/Login`
- **Publicar**: `btn-primary`
  - Usuario NO autenticado → `/Account/Register?role=publisher`
  - Usuario autenticado con rol `Publisher` → `/publisher/dashboard`
  - Usuario autenticado SIN rol Publisher → `/publisher/upgrade` (página para solicitar rol publicador)

### Autenticación
- ASP.NET Core Identity ya integrado
- `<AuthorizeView>` controla qué se muestra:
  - No autenticado: "Iniciar sesión" + "Publicar" (→ registro)
  - Autenticado como publicador: "Mi panel" + "Publicar" (→ dashboard)
  - Rol futuro: admin

---

## 3. Modal de búsqueda inicial (Onboarding)

### Comportamiento
- Aparece automáticamente al entrar a la app por primera vez (o cuando no hay búsqueda activa en sessionStorage)
- Overlay semitransparente sobre todo el UI
- Se puede cerrar con X o con Escape → muestra resultados generales del área del usuario
- No bloquea si el usuario lo cierra: la app funciona igual

### Campos
| Campo | Tipo | Opciones |
|-------|------|---------|
| ¿Dónde querés buscar? | Text input con autocomplete | Ciudad, barrio, dirección, provincia |
| Tipo de operación | Toggle / radio buttons | Comprar · Alquilar · Temporario |
| Tipo de propiedad | Multi-select dropdown | Casa, Departamento, Duplex, PH, Complejo, Terreno, Campo, Cochera, Local, Oficina |

### Acción
- Botón "Buscar" → cierra modal, aplica filtros, centra mapa en la ubicación ingresada
- Guarda preferencias en `sessionStorage` para no volver a mostrar en la misma sesión

### Implementación Blazor
- Componente `SearchModal.razor` con parámetro `IsVisible` + `EventCallback OnSearch(SearchCriteria)`
- `SearchCriteria` DTO: `{ string Lugar, TipoOperacion? Operacion, List<TipoPropiedad> Tipos }`
- Home.razor gestiona el estado: `showModal = true` inicialmente
- Al cerrar o buscar: `await JS.InvokeVoidAsync("sessionStorage.setItem", "searched", "1")` → no vuelve a aparecer en la sesión
- Al cargar: `showModal = !(await JS.InvokeAsync<string>("sessionStorage.getItem", "searched") == "1")`

---

## 4. Barra de filtros (bajo el navbar)

### Layout Zillow-style
```
[ 🔍 Ciudad, barrio, dirección... ]  [En venta ▼]  [Precio ▼]  [Dormitorios ▼]  [Tipo ▼]  [Más filtros ▼]
```

### Filtros
| Filtro | Tipo | Detalle |
|--------|------|---------|
| Búsqueda texto | Input | Geocodifica con Mapbox Geocoding API, centra mapa |
| Operación | Dropdown | En venta / En alquiler / Temporario |
| Precio | Dropdown/Range | Precio mín–máx, con selector de moneda (USD / ARS) |
| Dormitorios | Dropdown | 1, 2, 3, 4, 5+ |
| Tipo de propiedad | Multi-select | Casa, Departamento, Duplex, PH, Terreno, Campo, Local, etc. |
| Más filtros | Panel expandible | Baños, Superficie mín/máx, Cochera, Jardín |

### Estado
- Todos los filtros se guardan en el estado de `Home.razor`
- `ApplyFilters()` filtra `allListings` en memoria y sincroniza `mapListings`
- Los markers del mapa se actualizan para mostrar solo propiedades filtradas (markers restantes se ocultan/eliminan)

### Geocodificación
- Mapbox Geocoding API (`/geocoding/v5/mapbox.places/{query}.json`)
- Llamada desde JS Interop: `mapInterop.geocodeAndFly(query)` → centra mapa
- El token Mapbox ya disponible via `window.MAPBOX_TOKEN`

---

## 5. Markers del mapa

### Diseño
- **Forma**: ícono SVG de casa (no óvalo)
- **Color por operación**:
  - `Venta` → crimson (`oklch(0.45 0.22 15)`) — rojo oscuro
  - `Alquiler` → verde (`oklch(0.55 0.18 145)`)
  - `AlquilerTemporario` → naranja (`oklch(0.65 0.18 60)`)
- **Estado hover**: escala 1.3, sombra de color correspondiente, z-index elevado
- **Estado selected**: relleno sólido + borde blanco + escala 1.4
- **Estado highlighted** (hover desde card): mismo efecto que hover

### SVG del ícono
```svg
<!-- Casa simple: tejado + cuerpo -->
<svg width="28" height="32" viewBox="0 0 28 32">
  <path d="M14 2 L26 14 L22 14 L22 30 L6 30 L6 14 L2 14 Z" 
        fill="currentColor" stroke="white" stroke-width="1.5"/>
  <rect x="11" y="20" width="6" height="10" fill="white" opacity="0.6"/>
</svg>
```

### Popup al click en marker
- Mini-card flotante anclada al marker (280px wide)
- Contenido: foto principal, precio, título, dirección, CTA "Ver detalle →"
- Click en CTA → abre `/property/{id}` en nueva pestaña
- Click fuera del popup → cierra
- Solo un popup a la vez

---

## 6. Property Cards (panel izquierdo)

### Layout compacto (Zillow-style)
```
┌──────────────────────────┐
│   [carousel fotos]       │
│   [badge operación]      │
├──────────────────────────┤
│ Precio                   │
│ Título                   │
│ 📍 Dirección, Ciudad     │
│ ▣ 85m²  🛏 3 dorm  🚿 2 │
└──────────────────────────┘
```

### Carrusel
- CSS puro: `carousel-track` con `transform: translateX(-N%)`, `transition: 320ms cubic-bezier(0.16,1,0.3,1)`
- Prev/Next buttons, dots de navegación (máx 5 dots)
- `@onclick:stopPropagation` en controles para no disparar navegación

### Hover effects
- `translateY(-4px) scale(1.005)` + shadow upgrade
- `will-change: transform` para performance GPU
- `transition: all 280ms cubic-bezier(0.16,1,0.3,1)`

### Estado `highlighted`
- Borde azul (`oklch(0.52 0.12 220)`)
- Box-shadow azul sutil
- Activado cuando el marker correspondiente está hovered en el mapa

### Click
- `await JS.InvokeVoidAsync("open", $"/property/{Listing.Id}", "_blank")` → nueva pestaña
- No navega en la misma ventana (evita recargar mapa)

---

## 7. Sincronización Card ↔ Marker

### Hover card → highlight marker
```
PropertyCard.HandleMouseEnter()
  → OnHover.InvokeAsync(id)           [EventCallback]
  → Home.HoverListing(id)
  → mapView.HighlightMarker(id)       [método público]
  → JS: mapInterop.highlightMarker(id) [agrega CSS .hovered]
```

### Hover marker → highlight card
```
JS: marker.addEventListener('mouseenter')
  → dotNetRef.invokeMethodAsync('OnMarkerHover', id)   [JSInvokable]
  → Home.hoveredListingId = id
  → ListingsPanel recibe HoveredListingId
  → PropertyCard recibe IsHighlighted=true
```

### Click marker → popup
```
JS: marker.addEventListener('click')
  → dotNetRef.invokeMethodAsync('OnMarkerClick', id)   [JSInvokable]
  → MapView.ShowPopup(id) → popupListing = mapListings.Find(id)
  → Popup HTML renderizado con Blazor (no JS puro)
```

---

## 8. Página de detalle (`/property/{id}`)

- Nueva pestaña, sin recargar mapa
- Navbar mínimo con logo + "← Volver al mapa"
- Galería principal: imagen grande + thumbnails horizontales + prev/next + contador
- Info: badge operación, título, dirección completa (calle, ciudad, provincia), precio destacado
- Stats grid: superficie, ambientes, dormitorios, baños
- Descripción completa
- Card de publicador: avatar con inicial, nombre, teléfono clickeable
- `@rendermode InteractiveServer`

---

## 9. Datos y seed

### Modelo `PropertyListing`
Campos clave: `Id`, `Titulo`, `Descripcion`, `Operacion (TipoOperacion)`, `TipoPropiedad`, `Precio`, `Moneda`, `Superficie`, `Ambientes`, `Dormitorios`, `Banos`, `Fotos (List<string>)`, `Location (PropertyLocation)`, `Publisher (Publisher)`, `IsActive`

### `PropertyLocation`
`Latitud`, `Longitud`, `DireccionTexto`, `Ciudad`, `Provincia`

### Seed data
8 propiedades reales de inmobiliaria "Savoia Alto Laguirre", Santa Rosa y Toay, La Pampa:
- armesto-586, av-espana-316, emilio-civit-2765, tierra-del-fuego-133
- arenales-437, la-rioja-307, pico-464, torcaza-725

### Imágenes
- Dev: archivos físicos en `wwwroot/images/properties/{slug}/`
- URLs: `/images/properties/{slug}/1.jpg`
- Producción (futuro): Azure Blob Storage o similar, URLs absolutas en DB

---

## 10. Almacenamiento de imágenes — estrategia

| Entorno | Estrategia | Implementación |
|---------|-----------|----------------|
| Development | Archivos en `wwwroot/images/` | Paths relativos en DB |
| Staging/Prod | Azure Blob Storage | URLs absolutas en DB (field `Fotos`) |
| Upload (futuro) | API de Publisher, form multipart | `IFormFile` → save → store URL |

La columna `Fotos` en DB es `nvarchar(max)` con JSON serializado (`List<string>`), lo que permite cambiar de `/images/...` a `https://cdn.../...` sin cambio de esquema.

---

## 11. Autenticación y roles

- ASP.NET Core Identity (ya integrado)
- Roles: `Publisher`, `Admin` (futuro)
- Usuario `Publisher`: puede crear/editar/eliminar sus propias propiedades
- Usuario anónimo: solo lectura + búsqueda
- Panel publicador: `/publisher/dashboard` (CRUD de propiedades propias)
- Registro de publicador: flujo separado que asigna rol `Publisher`

---

## 12. Arquitectura de componentes

```
Home.razor
├── Navbar.razor               ← componente separado (logo + auth buttons)
├── SearchModal.razor          ← NUEVO
├── FilterBar.razor            ← NUEVO  
├── split-layout
│   ├── ListingsPanel.razor
│   │   └── PropertyCard.razor (×N)
│   ├── resize-handle
│   └── MapView.razor
│       └── .map-card-popup (Blazor, no JS)
PropertyDetail.razor           ← página aparte
```

---

## 13. Pendientes Phase 2 (no implementados aún)

| Feature | Componente | Prioridad |
|---------|-----------|-----------|
| Modal de búsqueda inicial | `SearchModal.razor` | Alta |
| Barra de filtros completa (reemplaza chips actuales) | `FilterBar.razor` | Alta |
| Markers SVG casa con color por operación | `map-interop.js` | Alta |
| Geocodificación texto → mapa | `map-interop.js` + `FilterBar` | Alta |
| Hover marker → highlight card | `map-interop.js` + `MapView.razor` | Media |
| Clustering markers (muchas propiedades) | `map-interop.js` | Baja |
| Navbar refactor (search centrado) | `Home.razor` / nav CSS | Alta |

---

## 14. Fuera de scope (Phase 3+)

- CRUD completo de publicaciones con map picker de coordenadas
- Panel admin para moderar publicaciones
- Sistema de búsqueda con ElasticSearch / PostGIS
- Notificaciones (alertas de precio, nuevas propiedades)
- Favoritos / guardados por usuario
- Mensajería entre publicador y interesado
