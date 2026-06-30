# Phase 9.5 — Búsqueda Avanzada Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Mover el filtrado de propiedades de client-side a server-side, agregar búsqueda por palabra clave (título/descripción/ciudad/dirección) y paginar la lista lateral, sin tocar el endpoint del mapa.

**Architecture:** Nuevo `PagedResultDto<T>` genérico, nuevo método `IListingRepository.SearchAsync` que arma un `IQueryable` con filtros condicionales + texto libre vía `.Contains()` (traducido a `LIKE`), nuevo endpoint `GET /api/listings/search`, y `Home.razor`/`FilterBar.razor` reescritos para pedir páginas al servidor en vez de filtrar en memoria. `/api/listings/map` no se toca.

**Tech Stack:** EF Core 9 (sin migración — no hay cambios de schema), ASP.NET Core Web API, Blazor Server.

**Spec de referencia:** `docs/superpowers/specs/2026-06-30-phase9-search-design.md`

---

### Task 1: PagedResultDto + IListingRepository.SearchAsync

**Files:**
- Create: `PropertyMap.Core/DTOs/PagedResultDto.cs`
- Modify: `PropertyMap.Core/Interfaces/IListingRepository.cs`
- Modify: `PropertyMap.Infrastructure/Repositories/ListingRepository.cs`

- [ ] **Step 1: Crear el DTO genérico de paginación**

```csharp
namespace PropertyMap.Core.DTOs;

public record PagedResultDto<T>(List<T> Items, int TotalCount, int Page, int PageSize);
```

- [ ] **Step 2: Agregar el método a la interfaz**

En `PropertyMap.Core/Interfaces/IListingRepository.cs`, agregar esta línea dentro de la interfaz, después de `Task<IEnumerable<PropertyListing>> GetActiveListingsAsync();`:

```csharp
    Task<PagedResultDto<PropertyListing>> SearchAsync(
        string? q, string? operacion, string? tipoPropiedad,
        decimal? precioMax, int? dormitoriosMin, int? banosMin,
        int page, int pageSize);
```

El archivo completo queda:

```csharp
using PropertyMap.Core.DTOs;
using PropertyMap.Core.DTOs.Admin;
using PropertyMap.Core.DTOs.Properties;
using PropertyMap.Core.Entities;

namespace PropertyMap.Core.Interfaces;

public interface IListingRepository
{
    Task<IEnumerable<PropertyListing>> GetActiveListingsAsync();
    Task<PagedResultDto<PropertyListing>> SearchAsync(
        string? q, string? operacion, string? tipoPropiedad,
        decimal? precioMax, int? dormitoriosMin, int? banosMin,
        int page, int pageSize);
    Task<IEnumerable<PropertyListing>> GetListingsByPublisherAsync(int publisherId);
    Task<IEnumerable<ListingMapDto>> GetActiveListingsForMapAsync();
    Task<PropertyListing?> GetByIdAsync(int id);
    Task<ListingDetailDto?> GetByIdAsDetailAsync(int id);
    Task<IEnumerable<MyListingDto>> GetMyListingsAsync(int publisherId);
    Task<IEnumerable<PendingListingDto>> GetPendingListingsAsync();
    Task<PropertyListing> AddAsync(PropertyListing listing);
    Task UpdateAsync(PropertyListing listing);
    Task DeleteAsync(int id);
}
```

- [ ] **Step 3: Implementar SearchAsync en ListingRepository.cs**

Agregar este método dentro de la clase `ListingRepository`, inmediatamente después de `GetActiveListingsAsync()`:

```csharp
    public async Task<PagedResultDto<PropertyListing>> SearchAsync(
        string? q, string? operacion, string? tipoPropiedad,
        decimal? precioMax, int? dormitoriosMin, int? banosMin,
        int page, int pageSize)
    {
        var query = ctx.PropertyListings
            .Where(l => l.Estado == EstadoPublicacion.Publicada)
            .Include(l => l.Location)
            .Include(l => l.Publisher)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(l =>
                l.Titulo.Contains(q) || l.Descripcion.Contains(q) ||
                l.Location.Ciudad.Contains(q) || l.Location.DireccionTexto.Contains(q));

        if (!string.IsNullOrWhiteSpace(operacion) && Enum.TryParse<TipoOperacion>(operacion, out var op))
            query = query.Where(l => l.Operacion == op);

        if (!string.IsNullOrWhiteSpace(tipoPropiedad) && Enum.TryParse<TipoPropiedad>(tipoPropiedad, out var tp))
            query = query.Where(l => l.TipoPropiedad == tp);

        if (precioMax.HasValue)
            query = query.Where(l => l.Precio <= precioMax);

        if (dormitoriosMin.HasValue)
            query = query.Where(l => l.Dormitorios.HasValue && l.Dormitorios >= dormitoriosMin);

        if (banosMin.HasValue)
            query = query.Where(l => l.Banos.HasValue && l.Banos >= banosMin);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(l => l.Destacado)
            .ThenByDescending(l => l.FechaPublicacion)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResultDto<PropertyListing>(items, totalCount, page, pageSize);
    }
```

- [ ] **Step 4: Verificar que compila**

Run: `cd C:\Agentes\PropertyMap && dotnet build src/PropertyMap.Web/PropertyMap.Web.sln`
Expected: `Compilación correcta. 0 Errores`

- [ ] **Step 5: Commit**

```bash
cd C:\Agentes\PropertyMap\src
git add PropertyMap.Core/DTOs/PagedResultDto.cs PropertyMap.Core/Interfaces/IListingRepository.cs PropertyMap.Infrastructure/Repositories/ListingRepository.cs
git commit -m "feat(search): add PagedResultDto and IListingRepository.SearchAsync"
```

---

### Task 2: Endpoint GET /api/listings/search

**Files:**
- Modify: `PropertyMap.Api/Controllers/ListingsController.cs`

- [ ] **Step 1: Agregar el endpoint**

El archivo actual de `ListingsController.cs` es:

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using PropertyMap.Core.Interfaces;

namespace PropertyMap.Api.Controllers;

[ApiController]
[Route("api/listings")]
public class ListingsController : ControllerBase
{
    private readonly IListingRepository _listings;
    private readonly IViewTrackingService _viewTracking;

    public ListingsController(IListingRepository listings, IViewTrackingService viewTracking)
    {
        _listings = listings;
        _viewTracking = viewTracking;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var listings = await _listings.GetActiveListingsAsync();
        return Ok(listings);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var listing = await _listings.GetByIdAsDetailAsync(id);
        if (listing == null) return NotFound();

        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            await _viewTracking.TrackViewAsync(id, userId, ip, DateOnly.FromDateTime(DateTime.UtcNow));
        }
        catch { }

        return Ok(listing);
    }

    [HttpGet("map")]
    public async Task<IActionResult> GetForMap()
    {
        var listings = await _listings.GetActiveListingsForMapAsync();
        return Ok(listings);
    }
}
```

Reemplazalo completo por (agrega el método `Search` antes de `GetById`, sin tocar ningún otro endpoint):

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using PropertyMap.Core.Interfaces;

namespace PropertyMap.Api.Controllers;

[ApiController]
[Route("api/listings")]
public class ListingsController : ControllerBase
{
    private readonly IListingRepository _listings;
    private readonly IViewTrackingService _viewTracking;

    public ListingsController(IListingRepository listings, IViewTrackingService viewTracking)
    {
        _listings = listings;
        _viewTracking = viewTracking;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var listings = await _listings.GetActiveListingsAsync();
        return Ok(listings);
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string? q,
        [FromQuery] string? operacion,
        [FromQuery] string? tipoPropiedad,
        [FromQuery] decimal? precioMax,
        [FromQuery] int? dormitoriosMin,
        [FromQuery] int? banosMin,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var result = await _listings.SearchAsync(
            q, operacion, tipoPropiedad, precioMax, dormitoriosMin, banosMin, page, pageSize);

        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var listing = await _listings.GetByIdAsDetailAsync(id);
        if (listing == null) return NotFound();

        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            await _viewTracking.TrackViewAsync(id, userId, ip, DateOnly.FromDateTime(DateTime.UtcNow));
        }
        catch { }

        return Ok(listing);
    }

    [HttpGet("map")]
    public async Task<IActionResult> GetForMap()
    {
        var listings = await _listings.GetActiveListingsForMapAsync();
        return Ok(listings);
    }
}
```

**Nota:** el endpoint `search` se registra ANTES de `{id:int}` en el archivo para evitar cualquier ambigüedad de ruteo entre `/api/listings/search` y `/api/listings/{id:int}` (en la práctica ASP.NET Core ya distingue por el constraint `:int`, pero mantener el orden hace la intención explícita).

- [ ] **Step 2: Verificar que compila**

Run: `cd C:\Agentes\PropertyMap && dotnet build src/PropertyMap.Web/PropertyMap.Web.sln`
Expected: `Compilación correcta. 0 Errores`

- [ ] **Step 3: Correr la suite completa (no debería romperse nada — solo se agrega un endpoint nuevo)**

Run: `cd C:\Agentes\PropertyMap && dotnet test src/PropertyMap.Tests/PropertyMap.Tests.csproj`
Expected: `Correctas! - Con error: 0, Superado: 109, Total: 109`

- [ ] **Step 4: Commit**

```bash
cd C:\Agentes\PropertyMap\src
git add PropertyMap.Api/Controllers/ListingsController.cs
git commit -m "feat(search): add GET /api/listings/search endpoint"
```

---

### Task 3: Tests de integración

**Files:**
- Create: `PropertyMap.Tests/Api/ListingSearchTests.cs`

- [ ] **Step 1: Crear el archivo con las 5 pruebas**

```csharp
using System.Net.Http.Json;
using PropertyMap.Core.DTOs;
using PropertyMap.Core.DTOs.Admin;
using PropertyMap.Core.DTOs.Properties;
using PropertyMap.Core.Entities;
using PropertyMap.Core.Enums;
using Xunit;

namespace PropertyMap.Tests.Api;

public class ListingSearchTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ListingSearchTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private record CreatedIdDto(int Id);

    private static CreateListingRequest BuildListingRequest(
        string titulo, string ciudad = "Bariloche", decimal precio = 80000,
        TipoOperacion operacion = TipoOperacion.Venta) => new(
        Operacion: operacion, TipoPropiedad: TipoPropiedad.Casa,
        Titulo: titulo, Descripcion: "Casa con vista al lago y jardín amplio",
        Precio: precio, Moneda: "USD",
        DireccionTexto: "Av. Búsqueda 1", Ciudad: ciudad, Provincia: "Río Negro",
        Lat: -41.13, Lng: -71.30,
        Superficie: null, SuperficieCubierta: null, Ambientes: null,
        Dormitorios: null, Banos: null, Antiguedad: null,
        Cochera: false, Amenities: []);

    private async Task<int> CreateAndPublishListingAsync(
        string titulo, string ciudad = "Bariloche", decimal precio = 80000,
        TipoOperacion operacion = TipoOperacion.Venta)
    {
        var (pubClient, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);
        await TestAuthHelper.CreatePublisherProfileAsync(pubClient);
        var createResp = await pubClient.PostAsJsonAsync("/api/properties",
            BuildListingRequest(titulo, ciudad, precio, operacion));
        var created = await createResp.Content.ReadFromJsonAsync<CreatedIdDto>();

        var (adminClient, _) = await TestAuthHelper.CreateAuthenticatedAdminAsync(_factory);
        await adminClient.PatchAsJsonAsync($"/api/admin/listings/{created!.Id}/review",
            new ReviewListingRequest(true, null));

        return created.Id;
    }

    [Fact]
    public async Task Search_ByKeywordInTitulo_FindsMatch()
    {
        var listingId = await CreateAndPublishListingAsync("Casa exclusiva en Nahuel Huapi");

        var anonClient = _factory.CreateClient();
        var result = await anonClient.GetFromJsonAsync<PagedResultDto<PropertyListing>>(
            "/api/listings/search?q=Nahuel");

        Assert.Contains(result!.Items, l => l.Id == listingId);
    }

    [Fact]
    public async Task Search_ByKeywordInCiudad_FindsMatch()
    {
        var listingId = await CreateAndPublishListingAsync("Depto centrico", ciudad: "Villa La Angostura");

        var anonClient = _factory.CreateClient();
        var result = await anonClient.GetFromJsonAsync<PagedResultDto<PropertyListing>>(
            "/api/listings/search?q=Angostura");

        Assert.Contains(result!.Items, l => l.Id == listingId);
    }

    [Fact]
    public async Task Search_CombinedFilters_AppliesAll()
    {
        var matchId = await CreateAndPublishListingAsync(
            "Cabaña de montaña", precio: 60000, operacion: TipoOperacion.Venta);
        var noMatchPriceId = await CreateAndPublishListingAsync(
            "Cabaña cara de montaña", precio: 500000, operacion: TipoOperacion.Venta);
        var noMatchOperacionId = await CreateAndPublishListingAsync(
            "Cabaña de montaña en alquiler", precio: 60000, operacion: TipoOperacion.Alquiler);

        var anonClient = _factory.CreateClient();
        var result = await anonClient.GetFromJsonAsync<PagedResultDto<PropertyListing>>(
            "/api/listings/search?q=montaña&operacion=Venta&precioMax=100000");

        Assert.Contains(result!.Items, l => l.Id == matchId);
        Assert.DoesNotContain(result.Items, l => l.Id == noMatchPriceId);
        Assert.DoesNotContain(result.Items, l => l.Id == noMatchOperacionId);
    }

    [Fact]
    public async Task Search_Pagination_RespectsPageAndPageSizeAndReturnsCorrectTotalCount()
    {
        var marker = Guid.NewGuid().ToString("N")[..8];
        for (var i = 0; i < 5; i++)
            await CreateAndPublishListingAsync($"Propiedad paginacion {marker} {i}");

        var anonClient = _factory.CreateClient();
        var page1 = await anonClient.GetFromJsonAsync<PagedResultDto<PropertyListing>>(
            $"/api/listings/search?q={marker}&page=1&pageSize=2");
        var page2 = await anonClient.GetFromJsonAsync<PagedResultDto<PropertyListing>>(
            $"/api/listings/search?q={marker}&page=2&pageSize=2");

        Assert.Equal(5, page1!.TotalCount);
        Assert.Equal(2, page1.Items.Count);
        Assert.Equal(2, page2!.Items.Count);
        Assert.Empty(page1.Items.Select(l => l.Id).Intersect(page2.Items.Select(l => l.Id)));
    }

    [Fact]
    public async Task Search_NoResults_ReturnsEmptyItemsWithZeroTotalCount()
    {
        var anonClient = _factory.CreateClient();
        var result = await anonClient.GetFromJsonAsync<PagedResultDto<PropertyListing>>(
            "/api/listings/search?q=textoquenuncavaaexistirxyz123");

        Assert.Empty(result!.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task GetForMap_StillReturnsAllListings_UnaffectedBySearchChanges()
    {
        var listingId = await CreateAndPublishListingAsync("Propiedad para verificar mapa intacto");

        var anonClient = _factory.CreateClient();
        var mapResp = await anonClient.GetAsync("/api/listings/map");
        var mapListings = await mapResp.Content.ReadFromJsonAsync<List<ListingMapDto>>();

        Assert.Contains(mapListings!, l => l.Id == listingId);
    }
}
```

- [ ] **Step 2: Correr los tests nuevos**

Run: `cd C:\Agentes\PropertyMap && dotnet test src/PropertyMap.Tests/PropertyMap.Tests.csproj --filter "FullyQualifiedName~ListingSearchTests"`
Expected: `Correctas! - Con error: 0, Superado: 6, Total: 6`

- [ ] **Step 3: Correr la suite completa**

Run: `cd C:\Agentes\PropertyMap && dotnet test src/PropertyMap.Tests/PropertyMap.Tests.csproj`
Expected: `Correctas! - Con error: 0, Superado: 115, Total: 115`

- [ ] **Step 4: Commit**

```bash
cd C:\Agentes\PropertyMap\src
git add PropertyMap.Tests/Api/ListingSearchTests.cs
git commit -m "test: add integration tests for listing search and pagination"
```

---

### Task 4: Cliente Blazor — servicio, FilterBar, Home.razor

**Files:**
- Modify: `PropertyMap.Web/PropertyMap.Web/Services/IListingApiService.cs`
- Modify: `PropertyMap.Web/PropertyMap.Web/Services/ListingApiService.cs`
- Modify: `PropertyMap.Web/PropertyMap.Web/Components/Listings/FilterBar.razor`
- Modify: `PropertyMap.Web/PropertyMap.Web/Components/Pages/Home.razor`
- Modify: `PropertyMap.Web/PropertyMap.Web/wwwroot/app.css`

- [ ] **Step 1: Agregar SearchAsync a IListingApiService**

Reemplazar el contenido completo de `IListingApiService.cs`:

```csharp
using PropertyMap.Core.DTOs;
using PropertyMap.Core.Entities;

namespace PropertyMap.Web.Services;

public interface IListingApiService
{
    Task<IEnumerable<PropertyListing>> GetActiveListingsAsync();
    Task<PagedResultDto<PropertyListing>> SearchAsync(
        string? q, string? operacion, string? tipoPropiedad,
        decimal? precioMax, int? dormitoriosMin, int? banosMin,
        int page, int pageSize);
    Task<IEnumerable<ListingMapDto>> GetActiveListingsForMapAsync();
    Task<ListingDetailDto?> GetByIdAsync(int id);
}
```

- [ ] **Step 2: Implementar SearchAsync en ListingApiService**

Reemplazar el contenido completo de `ListingApiService.cs`:

```csharp
using System.Net.Http.Json;
using PropertyMap.Core.DTOs;
using PropertyMap.Core.Entities;

namespace PropertyMap.Web.Services;

public class ListingApiService : IListingApiService
{
    private readonly HttpClient _http;

    public ListingApiService(HttpClient http)
    {
        _http = http;
    }

    public async Task<IEnumerable<PropertyListing>> GetActiveListingsAsync()
    {
        return await _http.GetFromJsonAsync<IEnumerable<PropertyListing>>("/api/listings")
               ?? Enumerable.Empty<PropertyListing>();
    }

    public async Task<PagedResultDto<PropertyListing>> SearchAsync(
        string? q, string? operacion, string? tipoPropiedad,
        decimal? precioMax, int? dormitoriosMin, int? banosMin,
        int page, int pageSize)
    {
        var queryParts = new List<string> { $"page={page}", $"pageSize={pageSize}" };
        if (!string.IsNullOrWhiteSpace(q)) queryParts.Add($"q={Uri.EscapeDataString(q)}");
        if (!string.IsNullOrWhiteSpace(operacion)) queryParts.Add($"operacion={Uri.EscapeDataString(operacion)}");
        if (!string.IsNullOrWhiteSpace(tipoPropiedad)) queryParts.Add($"tipoPropiedad={Uri.EscapeDataString(tipoPropiedad)}");
        if (precioMax.HasValue) queryParts.Add($"precioMax={precioMax.Value}");
        if (dormitoriosMin.HasValue) queryParts.Add($"dormitoriosMin={dormitoriosMin.Value}");
        if (banosMin.HasValue) queryParts.Add($"banosMin={banosMin.Value}");

        var url = $"/api/listings/search?{string.Join("&", queryParts)}";
        return await _http.GetFromJsonAsync<PagedResultDto<PropertyListing>>(url)
               ?? new PagedResultDto<PropertyListing>([], 0, page, pageSize);
    }

    public async Task<IEnumerable<ListingMapDto>> GetActiveListingsForMapAsync()
    {
        return await _http.GetFromJsonAsync<IEnumerable<ListingMapDto>>("/api/listings/map")
               ?? Enumerable.Empty<ListingMapDto>();
    }

    public async Task<ListingDetailDto?> GetByIdAsync(int id)
    {
        try
        {
            return await _http.GetFromJsonAsync<ListingDetailDto>($"/api/listings/{id}");
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }
}
```

- [ ] **Step 3: Agregar el input de palabra clave a FilterBar.razor**

En `PropertyMap.Web/PropertyMap.Web/Components/Listings/FilterBar.razor`, insertar este bloque de markup inmediatamente después del `</div>` que cierra `filter-search-wrap` (el bloque de geocoding, líneas 7-38 del archivo original) y antes del comentario `@* ── Operación ── *@`:

```razor
    @* ── Búsqueda por palabra clave ── *@
    <div class="filter-keyword-wrap">
        <input class="filter-keyword-input"
               type="text"
               placeholder="Buscar palabra clave..."
               value="@Keyword"
               @oninput="OnKeywordInput"
               autocomplete="off"
               aria-label="Buscar por palabra clave" />
    </div>
```

En la sección `@code`, agregar estos parámetros inmediatamente después de `[Parameter] public string TipoProp { get; set; } = "";`:

```csharp
    [Parameter] public string Keyword { get; set; } = "";
    [Parameter] public EventCallback<string> OnKeywordChanged { get; set; }
```

Modificar la propiedad `HasActiveFilters` existente:
```csharp
    private bool HasActiveFilters =>
        !string.IsNullOrEmpty(Operacion) || !string.IsNullOrEmpty(MaxPrecio) ||
        Dormitorios.HasValue || Banos.HasValue || !string.IsNullOrEmpty(TipoProp) ||
        !string.IsNullOrEmpty(Keyword);
```

Agregar, después del campo `private CancellationTokenSource? debounceCts;` ya existente, un campo nuevo:
```csharp
    private CancellationTokenSource? keywordDebounceCts;
```

Y agregar este método nuevo, después de `ClearSearch()`:
```csharp
    private async Task OnKeywordInput(ChangeEventArgs e)
    {
        var value = e.Value?.ToString() ?? "";
        keywordDebounceCts?.Cancel();
        keywordDebounceCts = new CancellationTokenSource();
        var token = keywordDebounceCts.Token;
        try
        {
            await Task.Delay(250, token);
            if (!token.IsCancellationRequested)
                await OnKeywordChanged.InvokeAsync(value);
        }
        catch (OperationCanceledException) { /* tecla nueva: se cancela esta búsqueda */ }
    }
```

- [ ] **Step 4: Reescribir Home.razor**

Reemplazar el bloque `<FilterBar ... />` (líneas 17-28 del archivo original) por:

```razor
    <FilterBar
        Operacion="@filtroOperacion"
        MaxPrecio="@filtroMaxPrecioStr"
        Dormitorios="@filtroDormitorios"
        Banos="@filtroBanos"
        TipoProp="@filtroTipo"
        Keyword="@filtroKeyword"
        OnOperacionChanged="v => { filtroOperacion = v; _ = RunSearchAsync(resetPage: true); }"
        OnMaxPrecioChanged="v => { filtroMaxPrecioStr = v; _ = RunSearchAsync(resetPage: true); }"
        OnDormitoriosChanged="v => { filtroDormitorios = v; _ = RunSearchAsync(resetPage: true); }"
        OnBanosChanged="v => { filtroBanos = v; _ = RunSearchAsync(resetPage: true); }"
        OnTipoChanged="v => { filtroTipo = v; _ = RunSearchAsync(resetPage: true); }"
        OnKeywordChanged="v => { filtroKeyword = v; _ = RunSearchAsync(resetPage: true); }"
        OnClearAll="ClearAllFilters" />
```

Dentro del componente `ListingsPanel`, después de la línea `OnToggleCollapse="TogglePanel" />`, agregar controles de paginación:

```razor
        @if (!isPanelCollapsed && _totalCount > 0)
        {
            <div class="pm-pagination">
                <button class="btn-ghost" disabled="@(_currentPage <= 1)" @onclick="PrevPageAsync">
                    ← Anterior
                </button>
                <span>Mostrando @(((_currentPage - 1) * PageSize) + 1)–@(Math.Min(_currentPage * PageSize, _totalCount)) de @_totalCount</span>
                <button class="btn-ghost" disabled="@(_currentPage * PageSize >= _totalCount)" @onclick="NextPageAsync">
                    Siguiente →
                </button>
            </div>
        }
```

Reemplazar el bloque `@code { ... }` completo (desde `private List<PropertyListing> allListings = [];` hasta el cierre de la clase) por:

```csharp
@code {
    private List<PropertyListing> filteredListings = [];
    private List<ListingMapDto> mapListings = [];
    private string? loadError;

    private string filtroOperacion = "";
    private string filtroTipo = "";
    private string filtroMaxPrecioStr = "";
    private decimal? filtroMaxPrecio;
    private int? filtroDormitorios;
    private int? filtroBanos;
    private string filtroKeyword = "";

    private const int PageSize = 20;
    private int _currentPage = 1;
    private int _totalCount;

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
            mapListings = (await ListingApi.GetActiveListingsForMapAsync()).ToList();
            await RunSearchAsync(resetPage: true);
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
        if (!string.IsNullOrEmpty(criteria.Operacion)) filtroOperacion = criteria.Operacion;
        if (!string.IsNullOrEmpty(criteria.TipoProp))  filtroTipo = criteria.TipoProp;
        await RunSearchAsync(resetPage: true);
        if (criteria.Lat.HasValue && criteria.Lng.HasValue)
            await JS.InvokeVoidAsync("mapInterop.flyTo", criteria.Lat.Value, criteria.Lng.Value);
        else if (!string.IsNullOrWhiteSpace(criteria.Lugar))
            await JS.InvokeVoidAsync("mapInterop.geocodeAndFly", criteria.Lugar);
    }

    private async Task RunSearchAsync(bool resetPage)
    {
        filtroMaxPrecio = decimal.TryParse(filtroMaxPrecioStr, out var p) ? p : null;
        if (resetPage) _currentPage = 1;

        try
        {
            isLoading = true;
            var result = await ListingApi.SearchAsync(
                q: string.IsNullOrWhiteSpace(filtroKeyword) ? null : filtroKeyword,
                operacion: string.IsNullOrEmpty(filtroOperacion) ? null : filtroOperacion,
                tipoPropiedad: string.IsNullOrEmpty(filtroTipo) ? null : filtroTipo,
                precioMax: filtroMaxPrecio,
                dormitoriosMin: filtroDormitorios,
                banosMin: filtroBanos,
                page: _currentPage,
                pageSize: PageSize);

            filteredListings = result.Items;
            _totalCount = result.TotalCount;
        }
        catch (Exception ex) { loadError = ex.Message; }
        finally { isLoading = false; }

        StateHasChanged();
    }

    private async Task PrevPageAsync()
    {
        if (_currentPage <= 1) return;
        _currentPage--;
        await RunSearchAsync(resetPage: false);
    }

    private async Task NextPageAsync()
    {
        if (_currentPage * PageSize >= _totalCount) return;
        _currentPage++;
        await RunSearchAsync(resetPage: false);
    }

    private void ClearAllFilters()
    {
        filtroOperacion = "";
        filtroTipo = "";
        filtroMaxPrecioStr = "";
        filtroDormitorios = null;
        filtroBanos = null;
        filtroKeyword = "";
        _ = RunSearchAsync(resetPage: true);
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

**Importante:** `mapListings` ya NO se recalcula en cada filtro — se carga una sola vez en `OnInitializedAsync` desde `/api/listings/map` y queda fijo, tal como decide la spec (mapa y lista lateral son vistas independientes a partir de ahora).

- [ ] **Step 5: Agregar CSS para los controles de paginación y el input de palabra clave**

Agregar al final de `app.css`:

```css
/* ── Búsqueda avanzada (Phase 9.5) ─────────────────────────────────── */
.filter-keyword-wrap {
    display: flex;
    align-items: center;
}

.filter-keyword-input {
    padding: var(--space-2) var(--space-3);
    border: 1px solid var(--color-border);
    border-radius: var(--radius-md);
    font-size: var(--text-sm);
    background: var(--color-surface);
    color: var(--color-ink);
    min-width: 180px;
}

.filter-keyword-input:focus {
    outline: none;
    border-color: var(--color-primary);
}

.pm-pagination {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-2);
    padding: var(--space-3);
    border-top: 1px solid var(--color-border);
    font-size: var(--text-sm);
    color: var(--color-muted);
}

.pm-pagination button:disabled {
    opacity: 0.4;
    cursor: not-allowed;
}
```

Todos los tokens usados (`--color-border`, `--radius-md`, `--text-sm`, `--color-surface`, `--color-ink`, `--color-primary`, `--color-muted`, `--space-2`, `--space-3`) ya están confirmados en `wwwroot/css/tokens.css` — copiar el bloque tal cual, sin ajustes.

- [ ] **Step 6: Verificar que compila**

Run: `cd C:\Agentes\PropertyMap && dotnet build src/PropertyMap.Web/PropertyMap.Web.sln`
Expected: `Compilación correcta. 0 Errores`

- [ ] **Step 7: Commit**

```bash
cd C:\Agentes\PropertyMap\src
git add PropertyMap.Web/PropertyMap.Web/Services/IListingApiService.cs PropertyMap.Web/PropertyMap.Web/Services/ListingApiService.cs PropertyMap.Web/PropertyMap.Web/Components/Listings/FilterBar.razor PropertyMap.Web/PropertyMap.Web/Components/Pages/Home.razor PropertyMap.Web/PropertyMap.Web/wwwroot/app.css
git commit -m "feat(search): wire paginated server-side search into Home.razor and FilterBar"
```

---

### Task 5: Verificación final

**Files:** ninguno (solo verificación)

- [ ] **Step 1: Correr toda la suite de tests**

Run: `cd C:\Agentes\PropertyMap && dotnet test src/PropertyMap.Tests/PropertyMap.Tests.csproj`
Expected: `Correctas! - Con error: 0, Superado: 115` (109 previos + 6 de ListingSearchTests)

- [ ] **Step 2: Correr el build completo de la solución**

Run: `cd C:\Agentes\PropertyMap && dotnet build src/PropertyMap.Web/PropertyMap.Web.sln`
Expected: `Compilación correcta. 0 Errores`

- [ ] **Step 3: Verificación manual en navegador**

Levantar API + Web (ver patrón usado en Phase 9.1 para esto: migrar LocalDB si hace falta, `dotnet run` en ambos proyectos), abrir la home, y confirmar:
- Escribir una palabra clave filtra la lista lateral (con debounce, no en cada tecla) sin recargar la página completa.
- Combinar palabra clave + un filtro de dropdown (ej. Operación) filtra por ambos a la vez.
- Con más de 20 resultados, aparecen los controles de paginación y "Siguiente"/"Anterior" funcionan.
- El mapa sigue mostrando todas las propiedades sin importar los filtros de la lista (comportamiento esperado según el spec, no es un bug).
- "Limpiar" resetea todos los filtros incluyendo la palabra clave y vuelve a página 1.
