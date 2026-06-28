# Phase 9.2 — Testing Exhaustivo (gaps de integration tests) — Design

**Status:** Approved
**Scope:** Segundo sub-proyecto de Phase 9 (Escala & Calidad). Orden completo de Phase 9: 1) Clustering (✅), 2) Testing exhaustivo (este spec), 3) Security audit, 4) Audit logs, 5) Búsqueda avanzada.

## Contexto

El roadmap original describe esta tarea como "API integration tests, E2E". Se decidió acotar el alcance a **solo cerrar gaps de integration tests API**, dejando E2E con Playwright fuera de scope por ahora (decisión explícita del usuario — la suite E2E real, si se hace, será un sub-proyecto separado más adelante).

La cobertura actual (xUnit + EF InMemory + `WebApplicationFactory`, patrón establecido en `TestWebApplicationFactory.cs`/`TestAuthHelper`) cubre la mayoría de controllers vía tests dedicados o indirectos, pero faltan tests dedicados para 3 controllers:

- `AdminController` — solo cubierto parcialmente (vía `ReportsControllerTests`, que ejercita los endpoints de reportes, pero no `GetPending`/`Review`/`GetAll`)
- `ListingsController` — solo cubierto indirectamente (vía `AuthControllerTests`, `DestacadoTests`, `ViewTrackingTests`)
- `NotificationsController` — solo cubierto indirectamente (vía `AlertMatchingTests`, que dispara notificaciones pero no ejercita los endpoints propios del controller)

## Bug encontrado y resuelto durante el brainstorming (fuera de este plan, ya cerrado)

Durante el análisis de qué cubrir, se confirmó que `GET /api/listings` y `GET /api/admin/listings` (ambos llaman a `ListingRepository.GetActiveListingsAsync()`) crasheaban con 500 por un ciclo de serialización JSON (`Listing→Publisher→Listings→Publisher→...`, profundidad máxima excedida). Esto ya fue corregido y pusheado **antes** de este plan (commit `56a0d37`, `[JsonIgnore]` en `Publisher.Listings` y `Location.Listings`) — los tests nuevos de este plan asumen que el fix ya está en `master` y por lo tanto pueden ejercitar `GetAll` normalmente sin necesidad de workarounds.

## Alcance

Solo agregar tests de integración nuevos. Sin tocar producción salvo que algún test revele un bug nuevo (en cuyo caso se trata como hallazgo de review, igual que en fases anteriores). Sin nueva infraestructura de testing — mismo patrón `IClassFixture<TestWebApplicationFactory>` + `TestAuthHelper` que ya usan los 15 archivos de test existentes.

## Archivos nuevos

### `PropertyMap.Tests/Api/AdminControllerTests.cs`

- `GetPending_ReturnsOnlyPendingListings` — crea listings en estados `PendienteAprobacion` y `Publicada`, confirma que `GET /api/admin/listings/pending` devuelve solo los pendientes.
- `Review_Aprobar_PublishesListingAndTriggersAlerts` — aprueba un listing pendiente, confirma `Estado == Publicada` (vía query directa a `AppDbContext`, no solo `Assert.NotNull`, siguiendo la lección aprendida en Phase 7 sobre asserts débiles) y que se generó al menos una notificación si había una alerta activa que matchea (reusar patrón de `AlertMatchingTests`).
- `Review_Rechazar_SetsBorradorWithMotivo` — rechaza un listing pendiente con motivo, confirma `Estado == Borrador`.
- `Review_NotPending_ReturnsBadRequest` — intenta revisar un listing que ya está `Publicada`, espera 400.
- `Review_NotFound_Returns404` — id inexistente, espera 404.
- `GetAll_ReturnsActiveListings` — confirma que `GET /api/admin/listings` devuelve 200 (no 500) con los listings publicados — este es el test que hubiera detectado el bug del ciclo JSON si hubiera existido antes.
- `Endpoints_RequireAdminRole_RejectsOtherRoles` — un usuario autenticado sin rol Admin (ej. Publisher) recibe 403 al llamar cualquiera de los endpoints de este controller.

### `PropertyMap.Tests/Api/ListingsControllerTests.cs`

- `GetAll_ReturnsOnlyPublishedListings` — crea listings en varios estados, confirma que `GET /api/listings` devuelve 200 y solo incluye `Publicada`.
- `GetAll_NoCrashOnPublisherNavigation` — test de regresión explícito para el bug del ciclo JSON: confirma que la respuesta deserializa correctamente como `List<PropertyListing>` sin excepción, con al menos un listing cuyo Publisher no es null.
- `GetById_ReturnsDetailAndTracksView` — pide detalle de un listing publicado, confirma 200 con los campos esperados (Titulo, Operacion, Publisher, Images).
- `GetById_NotFound_Returns404` — id inexistente.
- `GetForMap_ReturnsMapDtos` — confirma que `GET /api/listings/map` devuelve `ListingMapDto` con campos mínimos (Id, Lat, Lng, Operacion) — complementa (no duplica) la cobertura indirecta ya existente en `DestacadoTests`.

### `PropertyMap.Tests/Api/NotificationsControllerTests.cs`

- `GetMine_ReturnsOnlyOwnNotifications` — crea notificaciones para 2 usuarios distintos (insertando directamente vía `AppDbContext`, ya que no hay endpoint público de creación — las notificaciones se generan internamente por `AlertMatchingService`), confirma que cada usuario solo ve las suyas.
- `GetMine_RespectsTakeParameter` — crea más notificaciones que el `take` pedido, confirma que se respeta el límite.
- `GetUnreadCount_CountsOnlyUnread` — mezcla de leídas/no leídas, confirma el conteo.
- `MarkAsRead_SetsLeidaTrue_DoesNotAffectOthers` — marca una como leída, confirma vía `AppDbContext` que solo esa cambió.
- `MarkAsRead_OtherUsersNotification_DoesNotAffectIt` — usuario A intenta marcar como leída una notificación de usuario B; confirma (vía `AppDbContext`) que la notificación de B sigue sin leer — `MarkAsReadAsync(id, userId)` ya filtra por userId en el repositorio, este test verifica que ese filtro funciona end-to-end.
- `MarkAllAsRead_MarksOnlyCurrentUsersNotifications` — usuario A tiene notificaciones sin leer, usuario B también; A llama `read-all`; confirma que las de A quedan leídas y las de B no.

## Testing del testing

Cada test nuevo sigue TDD: se escribe, se corre y se confirma que pasa contra el código actual (no son tests que deban fallar primero, ya que ejercitan funcionalidad existente, no nueva — la única excepción conceptual es `GetAll_NoCrashOnPublisherNavigation`, que históricamente habría fallado antes del fix ya mergeado). Verificación final: `dotnet test src/PropertyMap.Tests/PropertyMap.Tests.csproj` con conteo total esperado de 83 + 18 tests nuevos = 101.

## Fuera de scope

- Suite E2E con Playwright contra la app corriendo (decisión explícita del usuario, queda para un sub-proyecto futuro si se decide hacerlo).
- Tests para `AuthController`, `ConsultasController`, `FavoritesController`, `PlansController`, `PropertiesController`, `PublisherController`, `RatingsController`, `ReportsController`, `StatsController`, `SubscriptionsController`, `UserController` — ya tienen cobertura dedicada.
- Tests de SignalR Hub (`NotificationsHub`) o `EmailService` — no mencionados en el roadmap de esta tarea, quedan como posible deuda técnica a evaluar en `Phase 9.4 — Audit logs` o por separado.
