# Ocultar datos sensibles de Publisher en endpoints públicos — Design

**Status:** Approved
**Origen:** Follow-up `task_41c244d0`, spawneado durante code review de "Phase 9.5 — Búsqueda Avanzada".

## Contexto

`GET /api/listings` y `GET /api/listings/search` (ambos sin autenticación) serializan la entidad `PropertyListing` cruda, incluyendo `Publisher.Email`, `Publisher.Telefono` y `Publisher.UserId` (FK interno a `AspNetUsers.Id`) en texto plano. Es preexistente (idéntico desde antes en `GetAll`, no introducido por Phase 9.5).

Se descartó la opción original de crear un DTO nuevo (`ListingSummaryDto`) porque `PropertyCard.razor`/`ListingsPanel.razor` reciben la entidad `PropertyListing` completa como parámetro tipado — introducir un DTO obligaría a tocar esos componentes Razor también, ampliando el alcance sin necesidad real. Se confirmó (grep) que ningún código de Blazor ni ninguna proyección a DTO existente (`ListingDetailDto`, `MyListingDto`, `PendingListingDto`) usa `Publisher.Email`/`Telefono`/`UserId` — todas esas proyecciones copian los campos que sí necesitan manualmente en C#, sin depender de la serialización JSON directa de la entidad.

## Alcance

Agregar `[JsonIgnore]` a `Publisher.Email`, `Publisher.Telefono` y `Publisher.UserId`, mismo patrón ya usado para `Publisher.Listings` (agregado en `56a0d37` para resolver el ciclo de serialización JSON). Esto bloquea la fuga en cualquier endpoint actual o futuro que serialice la entidad `Publisher` (directa o anidada vía `PropertyListing.Publisher`), sin afectar las proyecciones a DTO existentes (que no pasan por la serialización automática de la entidad).

## Testing

Un test de integración nuevo que confirme, vía `GET /api/listings`, que el JSON de respuesta NO contiene las claves `email`, `telefono` ni `userId` dentro del objeto `publisher` anidado (deserializando a `JsonDocument` y verificando ausencia de propiedades, en vez de deserializar a un tipo C# que ya las ignoraría silenciosamente).

## Fuera de scope

- Cualquier cambio a `PropertyCard.razor`, `ListingsPanel.razor`, o introducción de un DTO de listado nuevo — innecesario dado el approach elegido.
- `Publisher.Nombre`, `Publisher.LogoUrl`, `Publisher.Tipo` quedan visibles (son datos públicos del perfil de publisher, ya mostrados en la UI de detalle de propiedad).
