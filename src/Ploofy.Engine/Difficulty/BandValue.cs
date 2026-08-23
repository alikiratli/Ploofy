namespace Ploofy.Engine.Difficulty;

/// <summary>
/// Banda göre değişen tek bir ayarı taşır.
/// </summary>
/// <remarks>
/// Her mini oyunun kendi ayar sınıfını yazması yerine, oyundaki her knob bir
/// <see cref="BandValue{T}"/> olarak tanımlanır:
/// <c>new BandValue&lt;int&gt;(3, 6, 12)</c>. Zorluk tablosu böylece oyunun
/// kodunda tek satırda okunur kalır ve dengelemek için oyun mantığını okumak
/// gerekmez.
/// </remarks>
public sealed class BandValue<T>(T filiz, T fidan, T mese)
{
    public T Filiz { get; } = filiz;

    public T Fidan { get; } = fidan;

    public T Mese { get; } = mese;

    /// <summary>Üç bant için aynı değer — knob henüz ölçeklenmiyorsa.</summary>
    public static BandValue<T> Same(T value) => new(value, value, value);

    public T For(AgeBand band) => band switch
    {
        AgeBand.Filiz => Filiz,
        AgeBand.Fidan => Fidan,
        AgeBand.Mese => Mese,
        _ => throw new ArgumentOutOfRangeException(nameof(band), band, null),
    };
}
