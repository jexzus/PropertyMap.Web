using Microsoft.EntityFrameworkCore;
using PropertyMap.Core.Entities;
using PropertyMap.Core.Enums;

namespace PropertyMap.Infrastructure.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext ctx)
    {
        if (await ctx.PropertyListings.AnyAsync()) return;

        var publisher = new Publisher
        {
            Nombre = "Savoia Alto Laguirre",
            Email = "contacto@savoiaaltolaguirre.com.ar",
            Telefono = "+54 9 2954 520117",
            Tipo = TipoPublicador.Inmobiliaria,
            UserId = "seed-user"
        };
        ctx.Publishers.Add(publisher);

        var locations = new[]
        {
            // 0 - Armesto 586 (Inti Hue, Nueva Vista)
            new Location { Latitud = -36.5985, Longitud = -64.2640, DireccionTexto = "Armesto 586",          Ciudad = "Santa Rosa", Provincia = "La Pampa" },
            // 1 - Av. España 316 (esq. Dante Alighieri)
            new Location { Latitud = -36.6200, Longitud = -64.2895, DireccionTexto = "Av. España 316",       Ciudad = "Santa Rosa", Provincia = "La Pampa" },
            // 2 - Emilio Civit 2765
            new Location { Latitud = -36.6340, Longitud = -64.2695, DireccionTexto = "Emilio Civit 2765",    Ciudad = "Santa Rosa", Provincia = "La Pampa" },
            // 3 - Tierra del Fuego 133
            new Location { Latitud = -36.6148, Longitud = -64.2960, DireccionTexto = "Tierra del Fuego 133", Ciudad = "Santa Rosa", Provincia = "La Pampa" },
            // 4 - Arenales 437 (Villa Alonso)
            new Location { Latitud = -36.6090, Longitud = -64.3010, DireccionTexto = "Arenales 437",         Ciudad = "Santa Rosa", Provincia = "La Pampa" },
            // 5 - La Rioja 307 (esq. Catamarca)
            new Location { Latitud = -36.6230, Longitud = -64.2840, DireccionTexto = "La Rioja 307",         Ciudad = "Santa Rosa", Provincia = "La Pampa" },
            // 6 - General Pico 464
            new Location { Latitud = -36.6175, Longitud = -64.2855, DireccionTexto = "General Pico 464",     Ciudad = "Santa Rosa", Provincia = "La Pampa" },
            // 7 - Paloma Torcaza 725, Toay
            new Location { Latitud = -36.6695, Longitud = -64.3710, DireccionTexto = "Paloma Torcaza 725",   Ciudad = "Toay",       Provincia = "La Pampa" },
        };
        ctx.Locations.AddRange(locations);
        await ctx.SaveChangesAsync();

        ctx.PropertyListings.AddRange(
            new PropertyListing
            {
                Titulo = "Casa en Nueva Vista - Inti Hue",
                Descripcion = "Amplio living comedor con salida a galería y parrilla, cocina, baño, dormitorio y patio. Construcción moderna con pisos de porcelanato, carpinterías PVC con DVH, mesada de granito negro. Terreno 588m² (15x39.20). Superficie construida 97m².",
                Precio = 85000, Moneda = "USD",
                TipoPropiedad = TipoPropiedad.Casa, Operacion = TipoOperacion.Venta,
                Superficie = 97, Ambientes = 3, Dormitorios = 1, Banos = 1,
                Fotos = ["/images/properties/armesto-586/1.jpg", "/images/properties/armesto-586/2.jpg",
                         "/images/properties/armesto-586/3.jpg", "/images/properties/armesto-586/4.jpg",
                         "/images/properties/armesto-586/5.jpg", "/images/properties/armesto-586/6.jpg",
                         "/images/properties/armesto-586/7.jpg", "/images/properties/armesto-586/8.jpg"],
                LocationId = locations[0].Id, PublisherId = publisher.Id
            },
            new PropertyListing
            {
                Titulo = "Torre Av. España - Depto 2 dorm con amenities",
                Descripcion = "Departamentos de 2 y 3 dormitorios en torre. Estar-comedor amplio, cocina integrada, balcón-terraza, 2 baños. Amenities: SUM con parrilla, piscina climatizada en último piso, gimnasio, sauna y domótica. Cocheras en 2 subsuelos.",
                Precio = 168000, Moneda = "USD",
                TipoPropiedad = TipoPropiedad.Departamento, Operacion = TipoOperacion.Venta,
                Superficie = 168, Ambientes = 4, Dormitorios = 2, Banos = 2,
                Fotos = ["/images/properties/av-espana-316/1.jpeg", "/images/properties/av-espana-316/2.png",
                         "/images/properties/av-espana-316/3.png",  "/images/properties/av-espana-316/4.png",
                         "/images/properties/av-espana-316/5.png",  "/images/properties/av-espana-316/6.png",
                         "/images/properties/av-espana-316/7.png",  "/images/properties/av-espana-316/8.png"],
                LocationId = locations[1].Id, PublisherId = publisher.Id
            },
            new PropertyListing
            {
                Titulo = "Casa 3 dorm con cochera - E. Civit",
                Descripcion = "Estar comedor amplio y luminoso, cocina amoblada, lavadero, 3 dormitorios con placares, 2 baños completos, galería, patio y cochera. Carpinterías PVC con DVH. Entrega Julio 2026. Terreno 600m², construida 142m².",
                Precio = 120000, Moneda = "USD",
                TipoPropiedad = TipoPropiedad.Casa, Operacion = TipoOperacion.Venta,
                Superficie = 142, Ambientes = 6, Dormitorios = 3, Banos = 2,
                Fotos = ["/images/properties/emilio-civit-2765/1.jpg", "/images/properties/emilio-civit-2765/2.jpg",
                         "/images/properties/emilio-civit-2765/3.jpg", "/images/properties/emilio-civit-2765/4.jpg",
                         "/images/properties/emilio-civit-2765/5.jpg", "/images/properties/emilio-civit-2765/6.jpg",
                         "/images/properties/emilio-civit-2765/7.jpg", "/images/properties/emilio-civit-2765/8.jpg"],
                LocationId = locations[2].Id, PublisherId = publisher.Id
            },
            new PropertyListing
            {
                Titulo = "Depto 1 dorm con cochera y patio - Tierra del Fuego",
                Descripcion = "Amplio departamento con cochera y patio interno, a una cuadra y media de Av. Spinetto. Superficie 71m². Excelente ubicación, zona tranquila.",
                Precio = 55000, Moneda = "USD",
                TipoPropiedad = TipoPropiedad.Departamento, Operacion = TipoOperacion.Venta,
                Superficie = 71, Ambientes = 2, Dormitorios = 1, Banos = 1,
                Fotos = ["/images/properties/tierra-del-fuego-133/1.jpeg", "/images/properties/tierra-del-fuego-133/2.jpeg",
                         "/images/properties/tierra-del-fuego-133/3.jpeg", "/images/properties/tierra-del-fuego-133/4.jpeg",
                         "/images/properties/tierra-del-fuego-133/5.jpeg"],
                LocationId = locations[3].Id, PublisherId = publisher.Id
            },
            new PropertyListing
            {
                Titulo = "Monoambiente con cochera - Villa Alonso",
                Descripcion = "Monoambiente de 28m² en 2do piso con cochera cubierta al frente. Zona buscada por estudiantes, tranquila. Ambiente luminoso con sector dormitorio y estar, cocina separada, pequeño balcón. Construcción de calidad.",
                Precio = 35000, Moneda = "USD",
                TipoPropiedad = TipoPropiedad.Departamento, Operacion = TipoOperacion.Venta,
                Superficie = 28, Ambientes = 1, Dormitorios = 1, Banos = 1,
                Fotos = ["/images/properties/arenales-437/1.jpg", "/images/properties/arenales-437/2.jpg",
                         "/images/properties/arenales-437/3.jpg", "/images/properties/arenales-437/4.jpg",
                         "/images/properties/arenales-437/5.jpg", "/images/properties/arenales-437/6.jpg"],
                LocationId = locations[4].Id, PublisherId = publisher.Id
            },
            new PropertyListing
            {
                Titulo = "Depto céntrico - La Rioja esq. Catamarca",
                Descripcion = "Cocina, comedor y habitación integrados, baño. Segundo piso por escaleras. Muy buen estado, complejo tranquilo cerca del centro. Superficie 32m². Expensas $77.000.",
                Precio = 180000, Moneda = "ARS",
                TipoPropiedad = TipoPropiedad.Departamento, Operacion = TipoOperacion.Alquiler,
                Superficie = 32, Ambientes = 1, Dormitorios = 1, Banos = 1,
                Fotos = ["/images/properties/la-rioja-307/1.jpeg", "/images/properties/la-rioja-307/2.jpeg",
                         "/images/properties/la-rioja-307/3.jpeg", "/images/properties/la-rioja-307/5.jpeg",
                         "/images/properties/la-rioja-307/6.jpeg"],
                LocationId = locations[5].Id, PublisherId = publisher.Id
            },
            new PropertyListing
            {
                Titulo = "Casa 3 dorm con pileta - Gral. Pico",
                Descripcion = "Planta baja: living-comedor con cocina integrada, toilette, lavadero, jardín con pileta. Planta alta: 3 dormitorios con balcones y baño. Gran iluminación natural. Superficie construida 114m², terreno 187m².",
                Precio = 98000, Moneda = "USD",
                TipoPropiedad = TipoPropiedad.Casa, Operacion = TipoOperacion.Venta,
                Superficie = 114, Ambientes = 5, Dormitorios = 3, Banos = 2,
                Fotos = ["/images/properties/pico-464/1.jpg", "/images/properties/pico-464/2.jpg",
                         "/images/properties/pico-464/3.jpg", "/images/properties/pico-464/4.jpg",
                         "/images/properties/pico-464/5.jpg"],
                LocationId = locations[6].Id, PublisherId = publisher.Id
            },
            new PropertyListing
            {
                Titulo = "Casa premium con pileta y parque - Toay",
                Descripcion = "Amplio estar-comedor con cocina integrada y salida a galería. 3 dormitorios (principal con vestidor), escritorio, 2 baños, lavadero. Galería semicubierta, parque 973m² con pileta y solárium. Riego automático, persianas automatizadas, alarma. Superficie construida 239m².",
                Precio = 195000, Moneda = "USD",
                TipoPropiedad = TipoPropiedad.Casa, Operacion = TipoOperacion.Venta,
                Superficie = 239, Ambientes = 7, Dormitorios = 3, Banos = 2,
                Fotos = ["/images/properties/torcaza-725/1.jpg", "/images/properties/torcaza-725/2.jpg",
                         "/images/properties/torcaza-725/3.jpg", "/images/properties/torcaza-725/4.jpg",
                         "/images/properties/torcaza-725/5.jpg", "/images/properties/torcaza-725/6.jpg",
                         "/images/properties/torcaza-725/7.jpg"],
                LocationId = locations[7].Id, PublisherId = publisher.Id
            }
        );
        await ctx.SaveChangesAsync();
    }
}
