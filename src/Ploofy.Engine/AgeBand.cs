namespace Ploofy.Engine;

/// <summary>
/// Yaş bandı — uygulamanın tek zorluk ekseni.
/// </summary>
/// <remarks>
/// Her mini oyun ayrı "kolay/orta/zor" ayarı tutmaz; tek bir bant seçilir ve
/// bütün oyunlar parametrelerini o banda göre ölçekler. Böylece kardeşler aynı
/// oyunu farklı bantlarda oynayabilir ve yeni oyun eklerken zorluk tasarımı
/// sıfırdan düşünülmez.
/// </remarks>
public enum AgeBand
{
    /// <summary>2-4 yaş. Kaybetme yok, okuma yok, tek dokunuş / basit sürükle.</summary>
    Filiz = 0,

    /// <summary>4-6 yaş. Hafif hedefler, harf/sayı tanıma başlar, başarısızlık baskısı düşük.</summary>
    Fidan = 1,

    /// <summary>6-9 yaş. Zamanlayıcı, puan, çeldirici, kendi rekorunu kırma.</summary>
    Mese = 2,
}

public static class AgeBandExtensions
{
    /// <summary>
    /// Veritabanında ve kayıtlı dosyalarda saklanan sabit anahtar.
    /// Enum değerleri değişse bile bu metin değişmemeli — yıldız kayıtları buna bağlı.
    /// </summary>
    public static string ToId(this AgeBand band) => band switch
    {
        AgeBand.Filiz => "filiz",
        AgeBand.Fidan => "fidan",
        AgeBand.Mese => "mese",
        _ => throw new ArgumentOutOfRangeException(nameof(band), band, null),
    };

    /// <summary>
    /// Bilinmeyen bir anahtar gelirse orta bant döner: kayıt bozulmuşsa çocuğu
    /// hatayla karşılamak yerine oynanabilir bir zorlukla karşılamak daha doğru.
    /// </summary>
    public static AgeBand FromId(string id) => id switch
    {
        "filiz" => AgeBand.Filiz,
        "mese" => AgeBand.Mese,
        _ => AgeBand.Fidan,
    };

    public static (int Min, int Max) AgeRange(this AgeBand band) => band switch
    {
        AgeBand.Filiz => (2, 4),
        AgeBand.Fidan => (4, 6),
        AgeBand.Mese => (6, 9),
        _ => throw new ArgumentOutOfRangeException(nameof(band), band, null),
    };

    /// <summary>
    /// Ebeveynin girdiği yaşa en uygun bant. Bantlar uçlarda örtüştüğü için
    /// (4 yaş hem Filiz hem Fidan) sınır yaş büyük banda verilir.
    /// </summary>
    public static AgeBand ForAge(int age) => age switch
    {
        <= 3 => AgeBand.Filiz,
        <= 5 => AgeBand.Fidan,
        _ => AgeBand.Mese,
    };
}
