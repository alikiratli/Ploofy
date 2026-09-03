using Ploofy.App.Localization;

namespace Ploofy.App.Services;

/// <summary>
/// Kategorilerin arayüzdeki karşılığı: adı ve kutunun simgesi.
/// </summary>
/// <remarks>
/// <para>
/// Motor kategorinin yalnızca üyelerini biliyor. Ad üç dilde değişiyor,
/// kutu simgesi ise arayüze ait bir tercih; ikisi de burada. Aynı ayrım
/// <c>DotContent</c> ve <c>HuntContent</c> içinde de var.
/// </para>
/// <para>
/// Kutu simgesi kategorinin <b>üyesi olmayan</b> bir emoji: kutuda duran
/// simge parçalardan biriyle aynı olsaydı çocuk onu eşleştirilecek bir
/// çift sanırdı. Hayvan kutusunda pati izi, araç kutusunda trafik ışığı.
/// </para>
/// </remarks>
public static class CategoryContent
{
    /// <summary>Kategori adının kaynak anahtarı.</summary>
    public static string NameKey(string categoryId) => categoryId switch
    {
        "animals" => "CategoryAnimals",
        "vehicles" => "CategoryVehicles",
        "food" => "CategoryFood",
        "clothes" => "CategoryClothes",
        "fruit" => "CategoryFruit",
        "vegetables" => "CategoryVegetables",
        _ => categoryId,
    };

    public static string Name(string categoryId) =>
        LocalizationService.Instance[NameKey(categoryId)];

    /// <summary>Kutunun simgesi.</summary>
    public static string Glyph(string categoryId) => categoryId switch
    {
        "animals" => "🐾",
        "vehicles" => "🚦",
        "food" => "🍽️",
        "clothes" => "🧺",
        "fruit" => "🧃",
        "vegetables" => "🥗",
        _ => "📦",
    };
}
