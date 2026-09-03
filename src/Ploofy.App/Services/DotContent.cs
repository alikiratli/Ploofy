using Ploofy.App.Localization;

namespace Ploofy.App.Services;

/// <summary>
/// Noktaları Birleştir'in resimlerinin arayüzdeki karşılığı: adı ve simgesi.
/// </summary>
/// <remarks>
/// <para>
/// Motor resmin yalnızca geometrisini biliyor — bilmesi gereken de o. Ad üç
/// dilde değişiyor, simge ise arayüze ait bir tercih; ikisi de burada, tek
/// yerde. Yeni resim eklerken <c>DotPictures</c> içindeki satırın karşılığı
/// olarak buraya da bir satır giriyor.
/// </para>
/// <para>
/// Simge tanınmayan resim için ✏️ dönüyor: eksik bir eşleme oyunu
/// çöktürmemeli, yalnızca süssüz bırakmalı.
/// </para>
/// </remarks>
public static class DotContent
{
    /// <summary>Resim adının kaynak anahtarı.</summary>
    public static string NameKey(string pictureId) => pictureId switch
    {
        "fish" => "DotFish",
        "duck" => "DotDuck",
        "star" => "DotStar",
        "cat" => "DotCat",
        "turtle" => "DotTurtle",
        "bird" => "DotBird",
        "butterfly" => "DotButterfly",
        "rabbit" => "DotRabbit",
        "dog" => "DotDog",
        "whale" => "DotWhale",
        "elephant" => "DotElephant",
        _ => pictureId,
    };

    public static string Name(string pictureId) =>
        LocalizationService.Instance[NameKey(pictureId)];

    /// <summary>
    /// Resmin simgesi.
    /// </summary>
    /// <remarks>
    /// Okuma bilmeyen çocuk için adın yerine geçiyor: ne çizdiğini bilmek,
    /// bu oyunda sırayı takip etme isteğinin kaynağı.
    /// </remarks>
    public static string Glyph(string pictureId) => pictureId switch
    {
        "fish" => "🐟",
        "duck" => "🦆",
        "star" => "⭐",
        "cat" => "🐱",
        "turtle" => "🐢",
        "bird" => "🐦",
        "butterfly" => "🦋",
        "rabbit" => "🐰",
        "dog" => "🐶",
        "whale" => "🐋",
        "elephant" => "🐘",
        _ => "✏️",
    };
}
