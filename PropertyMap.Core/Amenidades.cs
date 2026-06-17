namespace PropertyMap.Core;

/// <summary>
/// Catálogo central de amenities/características, compartido entre el wizard
/// de publicación (chips seleccionables) y la vista de detalle.
/// </summary>
public static class Amenidades
{
    public static readonly IReadOnlyList<string> Catalogo =
    [
        "Balcón",
        "Cochera",
        "Pileta",
        "Parrilla",
        "Gimnasio",
        "SUM",
        "Seguridad 24hs",
        "Pet-friendly",
        "Apto profesional",
        "Amoblado",
        "Aire acondicionado",
        "Calefacción",
        "Laundry",
        "Terraza",
        "Baulera",
        "Solárium"
    ];
}
