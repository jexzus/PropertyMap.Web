# PropertyMap

Aplicación web (Blazor Web App, .NET 9) para publicar y explorar propiedades sobre un mapa interactivo.

## Requisitos

- **.NET 9 SDK**
- **SQL Server LocalDB** (viene con Visual Studio 2022/2025, o el instalable "SQL Server Express LocalDB")

## Cómo ejecutar

1. Clonar el repositorio.
2. Abrir `PropertyMap.Web/PropertyMap.Web.sln` en Visual Studio y ejecutar (**F5**), o desde terminal:

   ```bash
   dotnet run --project PropertyMap.Web/PropertyMap.Web
   ```

3. Listo. Al iniciar, la aplicación automáticamente:
   - crea y **migra** la base de datos `PropertyMapDb` en LocalDB,
   - la **puebla** con propiedades de ejemplo.

   La app queda disponible en `http://localhost:5187` (o el puerto definido en `launchSettings.json`).

> No hace falta configurar la cadena de conexión: usa `(localdb)\mssqllocaldb` por defecto.
> Si LocalDB tarda en levantar (arranque en frío), la app reintenta sola.

## Mapa

- **Por defecto funciona sin configurar nada**: usa **MapLibre + OpenFreeMap** (mapas
  gratuitos, sin token). Apenas clonás y ejecutás, el mapa anda.
- Si querés usar **Mapbox**, agregá tu token público (`pk....`) — el token **no se versiona**
  (lo bloquea el secret-scanning de GitHub y es buena práctica no commitearlo). Opciones:

  ```bash
  # opción recomendada (no toca archivos del repo):
  dotnet user-secrets set "Mapbox:AccessToken" "pk...." --project PropertyMap.Web/PropertyMap.Web
  ```

  o bien, localmente, completá `Mapbox:AccessToken` en `appsettings.json` (sin commitearlo).
  La app detecta el token y cambia a Mapbox automáticamente; si no hay, sigue con MapLibre.

## Funcionalidades

- Mapa con las propiedades geolocalizadas según su dirección.
- Buscador de direcciones con **autocompletado** y marcador sobre el lugar buscado.
- **Publicación** de propiedades (`/publicar`): wizard con carga de fotos, ubicación
  automática por dirección y vista previa.
- **Detalle** de cada propiedad con galería, características, comodidades y mapa de ubicación.

## Estructura

- `PropertyMap.Core` — entidades, enums, interfaces y DTOs.
- `PropertyMap.Infrastructure` — EF Core (`AppDbContext`, repositorios, migraciones y seed).
- `PropertyMap.Web/PropertyMap.Web` — aplicación Blazor (componentes, páginas, `wwwroot`).
- `PropertyMap.Web/PropertyMap.Web.Client` — proyecto WebAssembly cliente.

## Notas

- Las fotos subidas al publicar se guardan como archivos en `wwwroot/uploads/`
  (carpeta excluida de git; se crea sola al subir la primera foto).
