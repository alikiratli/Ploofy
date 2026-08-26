using Ploofy.App.Localization;
using Ploofy.Engine.Catalog;

namespace Ploofy.App.Services;

/// <summary>
/// Katalogdaki oyunun arayüzdeki karşılığı: adı, kart rengi, gideceği sayfa.
/// </summary>
/// <remarks>
/// Motor oyunun adını ve rengini bilmiyor — bilmemeli de, çünkü bunlar dile ve
/// temaya göre değişiyor. Eşleme burada, tek yerde: yeni oyun eklerken
/// katalogdaki satırın karşılığı olarak buraya da bir satır giriyor.
/// </remarks>
public static class GamePresentation
{
    /// <summary>Oyun adının kaynak anahtarı.</summary>
    public static string NameKey(string gameId) => gameId switch
    {
        GameCatalog.MemoryMatch => "GameMemoryMatch",
        GameCatalog.BubblePop => "GameBubblePop",
        GameCatalog.ShapeSort => "GameShapeSort",
        GameCatalog.MazeTrace => "GameMazeTrace",
        GameCatalog.Jigsaw => "GameJigsaw",
        GameCatalog.SimonSequence => "GameSimonSequence",
        GameCatalog.BasketCatch => "GameBasketCatch",
        GameCatalog.LetterHunt => "GameLetterHunt",
        GameCatalog.NumberHunt => "GameNumberHunt",
        GameCatalog.CountMatch => "GameCountMatch",
        _ => gameId,
    };

    public static string Name(string gameId) => LocalizationService.Instance[NameKey(gameId)];

    /// <summary>
    /// Oyun kartının simgesi. Okuma bilmeyen çocuk oyunu adından değil
    /// simgesinden tanıyor, bu yüzden simge ada eşlik eden süs değil asıl işaret.
    /// </summary>
    public static string Glyph(string gameId) => gameId switch
    {
        GameCatalog.MemoryMatch => "🃏",
        GameCatalog.BubblePop => "🫧",
        GameCatalog.ShapeSort => "🔺",
        GameCatalog.MazeTrace => "🧭",
        GameCatalog.Jigsaw => "🧩",
        GameCatalog.SimonSequence => "🥁",
        GameCatalog.BasketCatch => "🧺",
        GameCatalog.LetterHunt => "🔤",
        GameCatalog.NumberHunt => "🔢",
        GameCatalog.CountMatch => "🍎",
        _ => "🎈",
    };

    /// <summary>
    /// Kutucuğun degrade zemininin kaynak anahtarı. Katalogdaki sıraya göre
    /// dönüşümlü: aynı ekranda yan yana gelen iki kutucuk aynı renkte olmuyor.
    /// </summary>
    public static string BackgroundKey(string gameId)
    {
        var index = 0;
        for (var i = 0; i < GameCatalog.Games.Count; i++)
        {
            if (GameCatalog.Games[i].Id == gameId)
            {
                index = i;
                break;
            }
        }

        return $"Card{(index % 6) + 1}Brush";
    }

    /// <summary>
    /// Oyunun sayfa yolu. Henüz yazılmamış oyunlar null döner; ana ekran
    /// onları "yakında" olarak gösterir.
    /// </summary>
    public static string? Route(string gameId) => gameId switch
    {
        GameCatalog.MemoryMatch => "memorymatch",
        GameCatalog.BubblePop => "bubblepop",
        GameCatalog.ShapeSort => "shapesort",
        GameCatalog.LetterHunt => "hunt",
        GameCatalog.NumberHunt => "hunt",
        GameCatalog.CountMatch => "countmatch",
        GameCatalog.SimonSequence => "simon",
        GameCatalog.BasketCatch => "basketcatch",
        _ => null,
    };

    public static bool IsPlayable(string gameId) => Route(gameId) is not null;
}
