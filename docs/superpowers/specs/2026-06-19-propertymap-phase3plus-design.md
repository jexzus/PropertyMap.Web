# PropertyMap — Diseño Phase 3+ (API REST + Domain Model Completo)

**Fecha:** 2026-06-19  
**Estado:** Aprobado  
**Alcance:** Migración a arquitectura separada (API REST) + domain model completo + plan de fases 3–9

---

## 1. Contexto

PropertyMap es una plataforma web inmobiliaria con mapa interactivo construida en Blazor Web App (.NET 9). Las Phases 1 y 2 completaron la UI principal: mapa Leaflet/MapLibre, filtros, cards de propiedades, página de detalle, y design system OKLCH.

El roadmap define una plataforma completa con: portal inmobiliario, geolocalización, marketplace, reputación, notificaciones inteligentes y monetización.

---

## 2. Decisión Arquitectural

### Patrón elegido: API REST separada (Opción A)

Agregar un nuevo proyecto `PropertyMap.Api` a la solución existente. Blazor deja de acceder a la base de datos directamente y consume la API via `HttpClient` con JWT Bearer tokens.

**Razones:**
- Separación real de responsabilidades
- Reutiliza `PropertyMap.Core` e `PropertyMap.Infrastructure` sin duplicación
- Habilita apps móviles futuras en la misma API
- Deployable de forma independiente en MonsterASP.NET

### Estructura final de la solución (6 proyectos)

```
PropertyMap.sln
├── PropertyMap.Core           ← entidades, enums, interfaces, DTOs
├── PropertyMap.Infrastructure ← AppDbContext, repos, servicios, migrations
├── PropertyMap.Api            ← NEW: Web API REST, JWT, controllers
├── PropertyMap.Web            ← Blazor server host (refactor → HttpClient)
├── PropertyMap.Web.Client     ← Blazor WASM client
└── PropertyMap.Tests          ← xUnit (expandir con API tests)
```

### Flujo de datos

```
Browser / App Móvil
      ↓ HTTP + JWT Bearer
PropertyMap.Api  (ASP.NET Core Web API)
      ↓ interfaces
PropertyMap.Infrastructure
      ↓ EF Core 9
SQL Server (MonsterASP.NET)
      ↑
PropertyMap.Web (Blazor)
  → consume misma API via HttpClient
```

---

## 3. Autenticación

### JWT + Refresh Tokens

- **Access Token:** 15 minutos, firmado con clave simétrica
- **Refresh Token:** 7 días, rotante (se invalida al usar), almacenado en `ApplicationUser`
- **Almacenamiento cliente:** `localStorage` (Blazor WASM) / cookie httpOnly (servidor)

### Flujos

| Flujo | Endpoints |
|---|---|
| Registro | `POST /auth/register` → email con token 6 dígitos → `POST /auth/verify-email` |
| Login | `POST /auth/login` → `{ accessToken, refreshToken, expiry }` |
| Refresh | `POST /auth/refresh` → nuevo par de tokens |
| Forgot password | `POST /auth/forgot-password` → email con link (token 1h) |
| Reset password | `POST /auth/reset-password` → nueva contraseña |

### Validaciones de contraseña
- Mínimo 8 caracteres
- 1 mayúscula, 1 número, 1 símbolo

### Roles del sistema
- `Admin` — gestión global de la plataforma
- `Publisher` — inmobiliarias, corredores, agentes
- `User` — usuarios regulares (compradores/inquilinos)

---

## 4. Domain Model

### Cambios a entidades existentes

| Entidad / Campo | Cambio |
|---|---|
| `PropertyListing.Fotos` (JSON) | Reemplazar por tabla `PropertyImage` normalizada |
| `PropertyListing.Estado` | Migrar de `EstadoPropiedad` a `EstadoPublicacion` (7 estados) |
| `TipoOperacion` | Agregar: Permuta, Subasta, ProyectoEnConstruccion |
| `TipoPropiedad` | Agregar: Monoambiente, Galpon |
| `Publisher.UserId` (string) | → FK tipada a `ApplicationUser` |

### ApplicationUser (extiende IdentityUser)

```
Nombre: string
Apellido: string
Telefono: string?
AvatarUrl: string?
Estado: EstadoUsuario
FechaRegistro: DateTime
RefreshToken: string?
RefreshTokenExpiry: DateTime?
```

### Entidades nuevas

#### PropertyImage
```
Id, PropertyListingId → PropertyListing
Url: string
Orden: int
EsPrincipal: bool
```

#### PropertyView
```
Id, PropertyListingId → PropertyListing
UserId: string? (nullable, vistas anónimas)
FechaVista: DateTime
IpAddress: string?
```

#### PropertyFavorite
```
Id, PropertyListingId → PropertyListing
UserId → ApplicationUser
FechaAgregado: DateTime
[UNIQUE: UserId + PropertyListingId]
```

#### PropertyRating (solo AlquilerTemporario)
```
Id, PropertyListingId → PropertyListing
UserId → ApplicationUser
PuntajeUbicacion: int (1-5)
PuntajeEstado: int (1-5)
PuntajePrecioCalidad: int (1-5)
Comentario: string?
FechaValoracion: DateTime
[UNIQUE: UserId + PropertyListingId]
```

#### AgentRating
```
Id, PublisherId → Publisher
UserId → ApplicationUser
PuntajeAtencion: int (1-5)
PuntajeRapidez: int (1-5)
PuntajeTransparencia: int (1-5)
PuntajeProfesionalismo: int (1-5)
Comentario: string?
FechaValoracion: DateTime
[UNIQUE: UserId + PublisherId]
```

#### PropertyQuestion
```
Id, PropertyListingId → PropertyListing
UserId → ApplicationUser
Mensaje: string
FechaPregunta: DateTime
Answers → PropertyAnswer[]
```

#### PropertyAnswer
```
Id, PropertyQuestionId → PropertyQuestion
PublisherId → Publisher
Mensaje: string
FechaRespuesta: DateTime
```

#### Notification
```
Id, UserId → ApplicationUser
Tipo: TipoNotificacion
Titulo: string, Mensaje: string
Leida: bool
UrlAccion: string?
FechaCreacion: DateTime
```

#### NotificationPreference (1:1 con User)
```
Id, UserId → ApplicationUser [UNIQUE]
RecibirEmail: bool
RecibirPush: bool
NuevasConsultas: bool
NuevasRespuestas: bool
AlertasCoincidencia: bool
```

#### Alert (búsqueda guardada)
```
Id, UserId → ApplicationUser
Nombre: string?
Operacion: TipoOperacion?
TipoPropiedad: TipoPropiedad?
Ciudad: string?
PrecioMax: decimal?
Moneda: string?
DormitoriosMin: int?
Activa: bool
FechaCreacion: DateTime
```

#### Report
```
Id, PropertyListingId → PropertyListing
UserId → ApplicationUser
Motivo: MotivoReporte
Descripcion: string?
Estado: EstadoReporte
FechaReporte: DateTime
```

#### Plan
```
Id, Nombre: string, Slug: string
PrecioMensual: decimal, Moneda: string
MaxPublicaciones: int? (null = ilimitado)
DestacadosIncluidos: int
EstadisticasAvanzadas: bool
Activo: bool
```

#### Subscription
```
Id, UserId → ApplicationUser
PlanId → Plan
Estado: EstadoSuscripcion
FechaInicio: DateTime
FechaVencimiento: DateTime
AutoRenovar: bool
```

#### AuditLog
```
Id, UserId: string?
Accion: string
Entidad: string, EntidadId: string
Detalles: string? (JSON)
FechaAccion: DateTime
IpAddress: string?
```

### Nuevos Enums

```csharp
EstadoUsuario:      Activo | Suspendido | PendienteVerificacion | Eliminado
EstadoPublicacion:  Borrador | PendienteAprobacion | Publicada | Pausada | Vendida | Alquilada | Eliminada
TipoNotificacion:   NuevaConsulta | NuevaRespuesta | AlertaCoincidencia | Aprobacion | Suspension
MotivoReporte:      Estafa | InformacionFalsa | Duplicado | Spam | Otro
EstadoReporte:      Pendiente | EnRevision | Resuelto | Rechazado
EstadoSuscripcion:  Activa | Vencida | Cancelada | PendientePago
```

### Diagrama de relaciones

```
ApplicationUser ──1:1──► NotificationPreference
ApplicationUser ──1:N──► Notification
ApplicationUser ──1:N──► PropertyFavorite
ApplicationUser ──1:N──► PropertyRating
ApplicationUser ──1:N──► AgentRating
ApplicationUser ──1:N──► PropertyQuestion
ApplicationUser ──1:N──► Alert
ApplicationUser ──1:1──► Subscription
ApplicationUser ──1:1──► Publisher  (rol Publisher)

Publisher ──1:N──► PropertyListing
Publisher ──1:N──► AgentRating
Publisher ──1:N──► PropertyAnswer

PropertyListing ──1:N──► PropertyImage
PropertyListing ──1:N──► PropertyView
PropertyListing ──1:N──► PropertyFavorite
PropertyListing ──1:N──► PropertyRating
PropertyListing ──1:N──► PropertyQuestion
PropertyListing ──1:N──► Report
PropertyListing ──N:1──► Location
```

---

## 5. Servicios Externos (MonsterASP.NET)

| Necesidad | Solución | Notas |
|---|---|---|
| Emails | MailKit + SMTP MonsterASP | Verificación, reset password, alertas |
| Imágenes | FileSystem `/uploads/` | MVP; migrable a Cloudinary luego |
| Push / Real-time | SignalR in-process | Notificaciones in-app |
| IA descripciones | Claude API (Phase 8) | claude-sonnet-4-6 |

---

## 6. Plan de Fases

### Phase 3 — Foundation & API *(próxima)*
**Objetivo:** Cimiento arquitectural completo.

- Crear 15 entidades nuevas en `PropertyMap.Core`
- Nueva migración EF Core (masiva), adaptar DbSeeder
- Crear proyecto `PropertyMap.Api` (ASP.NET Core Web API, .NET 9)
- Configurar JWT, CORS, Swagger/OpenAPI
- Implementar endpoints de auth: register, login, verify-email, refresh, forgot/reset-password
- Servicio de email con MailKit
- Refactor `PropertyMap.Web`: reemplazar inyección directa de repos → `HttpClient` a la API

### Phase 4 — Publisher CRUD
**Objetivo:** Inmobiliarios publican y gestionan propiedades.

- API: CRUD completo de propiedades con control de estados
- Subida de imágenes: `POST /properties/{id}/images`, guardado en `/uploads/`
- `PropertyImage` normalizada en DB
- Publisher dashboard en Blazor: mis propiedades, editar, pausar, eliminar
- Flujo de aprobación admin: Borrador → Pendiente → Publicada
- Conectar wizard de publicación existente a la API

### Phase 5 — User Features
**Objetivo:** Usuarios regulares se registran e interactúan.

- Registro y perfil de usuario regular (avatar, datos)
- Favoritos: guardar/quitar propiedad, listado personal
- Consultas: preguntas por propiedad, respuestas del publisher
- View tracking: contar vistas por propiedad (autenticado + anónimo por IP)

### Phase 6 — Reputación
**Objetivo:** Generar confianza en la plataforma.

- Valoración de propiedades (solo AlquilerTemporario, 3 aspectos)
- Valoración de agentes (4 aspectos, solo usuarios que consultaron)
- Ranking automático: 40% rating + 30% tiempo respuesta + 20% operaciones + 10% antigüedad
- Endpoints públicos: Top Agentes, Top Inmobiliarias

### Phase 7 — Inteligencia
**Objetivo:** Diferenciador principal frente a portales estáticos.

- Alertas de búsqueda: usuario guarda criterios, sistema matchea nuevas publicaciones
- Notificaciones in-app: SignalR, centro de notificaciones en navbar
- Notificaciones email: trigger automático al publicar propiedad que matchea alerta
- Reportes y moderación: usuarios reportan, admin revisa dashboard

### Phase 8 — Monetización
**Objetivo:** Revenue y features premium.

- Planes y suscripciones: Gratuito / Profesional / Premium
- Dashboard de estadísticas para publishers: vistas, favoritos, consultas, conversiones
- Destacados: prioridad visual en mapa y listado
- IA para descripción automática: Claude API (claude-sonnet-4-6)

### Phase 9 — Escala & Calidad
**Objetivo:** Producción real con tráfico real.

- Clustering de markers (Leaflet.markercluster) para muchas propiedades
- Búsqueda avanzada: full-text search SQL Server o PostGIS
- Audit logs completos con trazabilidad
- Rate limiting en API, security audit
- Testing exhaustivo: API integration tests, E2E con Playwright

---

## 7. Principios de Implementación

- Cada phase produce algo **funcional y deployable** — sin esperar al final
- Domain-first: las entidades y migraciones van antes que los endpoints
- Un controller por dominio (AuthController, PropertiesController, UsersController, etc.)
- Repositorios genéricos + específicos según necesidad
- Soft delete en entidades críticas (usuarios, propiedades)
- Validación en API boundary (FluentValidation o DataAnnotations)
- Logging estructurado con Serilog

---

## 8. Fuera de Scope (por ahora)

- App móvil nativa (la API la habilitará cuando llegue el momento)
- ElasticSearch (SQL Server full-text es suficiente para MVP)
- Pasarela de pago real (Subscriptions Phase 8 puede ser manual inicialmente)
- CDN para imágenes (FileSystem es suficiente en MonsterASP para MVP)
