using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PropertyMap.Core.Entities;
using PropertyMap.Core.Enums;

namespace PropertyMap.Infrastructure.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext context, UserManager<ApplicationUser> userManager)
    {
        if (await context.PropertyListings.AnyAsync()) return;

        // Seed publisher user
        var publisherUser = new ApplicationUser
        {
            Id = "seed-publisher-user",
            UserName = "contacto@savoiaaltolaguirre.com.ar",
            Email = "contacto@savoiaaltolaguirre.com.ar",
            EmailConfirmed = true,
            Nombre = "Savoia Alto",
            Apellido = "Laguirre",
            Estado = EstadoUsuario.Activo,
            FechaRegistro = DateTime.UtcNow,
            PhoneNumber = "+54 9 2954 520117"
        };

        if (await userManager.FindByIdAsync(publisherUser.Id) == null)
        {
            await userManager.CreateAsync(publisherUser, "Savoia123!");
            await userManager.AddToRoleAsync(publisherUser, "Publisher");
        }

        var publisher = new Publisher
        {
            Nombre = "Savoia Alto Laguirre",
            Email = "contacto@savoiaaltolaguirre.com.ar",
            Telefono = "+54 9 2954 520117",
            Tipo = TipoPublicador.Inmobiliaria,
            UserId = publisherUser.Id
        };
        context.Publishers.Add(publisher);
        await context.SaveChangesAsync();

        // Seed locations
        var locations = new List<Location>
        {
            new() { DireccionTexto = "Armesto 586", Ciudad = "Santa Rosa", Provincia = "La Pampa", Latitud = -36.64269, Longitud = -64.31722 },
            new() { DireccionTexto = "Av. España 316", Ciudad = "Santa Rosa", Provincia = "La Pampa", Latitud = -36.62551, Longitud = -64.28710 },
            new() { DireccionTexto = "Emilio Civit 2765", Ciudad = "Santa Rosa", Provincia = "La Pampa", Latitud = -36.64751, Longitud = -64.31145 },
            new() { DireccionTexto = "Tierra del Fuego 133", Ciudad = "Santa Rosa", Provincia = "La Pampa", Latitud = -36.60744, Longitud = -64.28384 },
            new() { DireccionTexto = "Arenales 437", Ciudad = "Santa Rosa", Provincia = "La Pampa", Latitud = -36.61671, Longitud = -64.27236 },
            new() { DireccionTexto = "La Rioja 307", Ciudad = "Santa Rosa", Provincia = "La Pampa", Latitud = -36.61399, Longitud = -64.29120 },
            new() { DireccionTexto = "General Pico 464", Ciudad = "Santa Rosa", Provincia = "La Pampa", Latitud = -36.62163, Longitud = -64.29390 },
            new() { DireccionTexto = "Paloma Torcaza 725", Ciudad = "Toay", Provincia = "La Pampa", Latitud = -36.64019, Longitud = -64.37139 }
        };
        context.Locations.AddRange(locations);
        await context.SaveChangesAsync();

        // Seed listings with PropertyImage rows
        var listingsData = new[]
        {
            new { Titulo = "Casa en Nueva Vista - Inti Hue", Tipo = TipoPropiedad.Casa, Op = TipoOperacion.Venta, Precio = 85000m, Moneda = "USD", Sup = 97m, Dorms = 1, Banos = 1, Ambientes = 2, Slug = "armesto-586", LocationIndex = 0, FotoCount = 3 },
            new { Titulo = "Torre Av. España - Depto 2 dorm", Tipo = TipoPropiedad.Departamento, Op = TipoOperacion.Venta, Precio = 168000m, Moneda = "USD", Sup = 168m, Dorms = 2, Banos = 2, Ambientes = 4, Slug = "av-espana-316", LocationIndex = 1, FotoCount = 4 },
            new { Titulo = "Casa 3 dorm con cochera - E. Civit", Tipo = TipoPropiedad.Casa, Op = TipoOperacion.Venta, Precio = 120000m, Moneda = "USD", Sup = 142m, Dorms = 3, Banos = 2, Ambientes = 5, Slug = "emilio-civit-2765", LocationIndex = 2, FotoCount = 2 },
            new { Titulo = "Depto 1 dorm - Tierra del Fuego", Tipo = TipoPropiedad.Departamento, Op = TipoOperacion.Venta, Precio = 55000m, Moneda = "USD", Sup = 71m, Dorms = 1, Banos = 1, Ambientes = 2, Slug = "tierra-del-fuego-133", LocationIndex = 3, FotoCount = 3 },
            new { Titulo = "Monoambiente - Villa Alonso", Tipo = TipoPropiedad.Monoambiente, Op = TipoOperacion.Venta, Precio = 35000m, Moneda = "USD", Sup = 28m, Dorms = 1, Banos = 1, Ambientes = 1, Slug = "arenales-437", LocationIndex = 4, FotoCount = 2 },
            new { Titulo = "Depto céntrico - La Rioja esq. Catamarca", Tipo = TipoPropiedad.Departamento, Op = TipoOperacion.Alquiler, Precio = 180000m, Moneda = "ARS", Sup = 32m, Dorms = 1, Banos = 1, Ambientes = 2, Slug = "la-rioja-307", LocationIndex = 5, FotoCount = 3 },
            new { Titulo = "Casa 3 dorm con pileta - Gral. Pico", Tipo = TipoPropiedad.Casa, Op = TipoOperacion.Venta, Precio = 98000m, Moneda = "USD", Sup = 114m, Dorms = 3, Banos = 2, Ambientes = 5, Slug = "pico-464", LocationIndex = 6, FotoCount = 4 },
            new { Titulo = "Casa premium con pileta - Toay", Tipo = TipoPropiedad.Casa, Op = TipoOperacion.Venta, Precio = 195000m, Moneda = "USD", Sup = 239m, Dorms = 3, Banos = 2, Ambientes = 7, Slug = "torcaza-725", LocationIndex = 7, FotoCount = 5 }
        };

        foreach (var data in listingsData)
        {
            var listing = new PropertyListing
            {
                Publisher = publisher,
                Location = locations[data.LocationIndex],
                Titulo = data.Titulo,
                Descripcion = $"Propiedad en {locations[data.LocationIndex].DireccionTexto}, {locations[data.LocationIndex].Ciudad}.",
                Precio = data.Precio,
                Moneda = data.Moneda,
                TipoPropiedad = data.Tipo,
                Operacion = data.Op,
                Superficie = data.Sup,
                Ambientes = data.Ambientes,
                Dormitorios = data.Dorms,
                Banos = data.Banos,
                Cochera = false,
                Estado = EstadoPublicacion.Publicada,
                FechaPublicacion = DateTime.UtcNow
            };

            for (int i = 0; i < data.FotoCount; i++)
            {
                listing.Images.Add(new PropertyImage
                {
                    Url = $"/images/properties/{data.Slug}/img{i + 1}.jpg",
                    Orden = i,
                    EsPrincipal = i == 0
                });
            }

            context.PropertyListings.Add(listing);
        }

        await context.SaveChangesAsync();
    }

    public static async Task SeedPlansAsync(AppDbContext context)
    {
        if (await context.Plans.AnyAsync()) return;

        context.Plans.AddRange(
            new Plan
            {
                Nombre = "Gratuito", Slug = "gratuito", PrecioMensual = 0m, Moneda = "ARS",
                MaxPublicaciones = 3, DestacadosIncluidos = 0, EstadisticasAvanzadas = false, Activo = true
            },
            new Plan
            {
                Nombre = "Profesional", Slug = "profesional", PrecioMensual = 15000m, Moneda = "ARS",
                MaxPublicaciones = 20, DestacadosIncluidos = 3, EstadisticasAvanzadas = true, Activo = true
            },
            new Plan
            {
                Nombre = "Premium", Slug = "premium", PrecioMensual = 35000m, Moneda = "ARS",
                MaxPublicaciones = null, DestacadosIncluidos = 10, EstadisticasAvanzadas = true, Activo = true
            }
        );
        await context.SaveChangesAsync();
    }
}
