using Ploofy.Engine.Progress;

namespace Ploofy.Engine.Difficulty;

/// <summary>
/// Bandın içindeki kademe.
/// </summary>
/// <remarks>
/// Bant ebeveynin seçtiği şey ve uygulama onu kendiliğinden değiştirmiyor;
/// değişen yalnızca o bandın içindeki kademe.
/// </remarks>
public enum DifficultyStep
{
    /// <summary>Bandın kendi ayarları.</summary>
    Base = 0,

    /// <summary>Bir üst bandın ayarları — bant, profil ve kutlama aynı kalır.</summary>
    Stretch = 1,
}

/// <summary>
/// Üç bant kaba: çocuk bir oyunu üst üste kusursuz bitiriyorsa o oyun ona
/// artık bir şey öğretmiyor demektir. Bu kural o durumu yakalayıp <b>tek bir
/// oyunun</b> zorluğunu bir kademe yukarı alıyor.
/// </summary>
/// <remarks>
/// <para>
/// <b>Kademe saklanmıyor, geçmişten türetiliyor.</b> Yeni bir sütun, yeni bir
/// ayar ve şema göçü olmamasının dışında asıl sebep şu: saklanan bir kademe
/// yanlış kaldığında kendi kendine düzelmez. Türetilen kademe her turda
/// yeniden hesaplanıyor, yani çocuk zorlanmaya başladığı anda kendiliğinden
/// geri iniyor ve kimsenin bir şeyi sıfırlaması gerekmiyor.
/// </para>
/// <para>
/// <b>Ölçüt yıldız değil, kusursuz tur.</b> Yıldız üç bantta üç ayrı şey
/// ölçüyor — Filiz'de bitirmek zaten üç yıldız, yani yıldıza bakan bir kural
/// her Filiz çocuğunu üç turda yukarı iterdi. Kusursuz tur (üç yıldız <b>ve</b>
/// sıfır hata) üç bantta da aynı şeyi söylüyor: bu tur çocuğa zor gelmedi.
/// </para>
/// <para>
/// <b>Kademe oyun başına.</b> Yapbozda ustalaşmış çocuk Yolu Bul'da da usta
/// değil; tek bir "seviye" bütün kütüphaneyi birden zorlaştırırdı.
/// </para>
/// <para>
/// <b>Değişen yalnızca knob'lar.</b> Kademe <see cref="BandValue{T}"/>
/// aramasını bir üst banda kaydırıyor; <see cref="DifficultyProfile"/> çocuğun
/// gerçek bandında kalıyor. Yani yukarı çıkan Filiz çocuğu daha çok parça
/// görüyor ama zamanlayıcı, yazı ve kaybetme yine gelmiyor: onlar zorluk
/// değil, yaşa uygunluk kuralları. Yıldız da gerçek bantla veriliyor, yani
/// zorlaşmış tur çocuğun ödülünü kısmıyor.
/// </para>
/// <para>
/// <b>Görünür olmak zorunda.</b> Sessizce zorlaşan bir oyun ebeveyne bozulmuş
/// gibi görünür ("dün yapıyordu, bugün yapamıyor"). Ana ekranda kutucuğun
/// üstünde bir işaret, tur sonunda bir satır ve ebeveyn ekranında bir liste
/// var; ebeveyn ayarlardan bütünüyle kapatabiliyor.
/// </para>
/// </remarks>
public static class AdaptiveDifficulty
{
    /// <summary>
    /// Yukarı çıkmak için gereken üst üste kusursuz tur sayısı.
    /// </summary>
    /// <remarks>
    /// İki az: bu yaşta iki kolay tur şansa gelebiliyor. Dört fazla: bir
    /// oyunu dört kez üst üste kusursuz bitiren çocuk çoktan sıkılmış olur.
    /// </remarks>
    public const int PerfectRoundsToStretch = 3;

    /// <summary>
    /// Bu bandın bir üstü var mı?
    /// </summary>
    /// <remarks>
    /// Meşe en üst bant, yani <see cref="BandValue{T}"/> içinde gidilecek bir
    /// değer yok ve orada kademe hep <see cref="DifficultyStep.Base"/> kalıyor.
    /// Bilinen sınır: 8-9 yaşındaki bir çocuk kütüphanenin tavanını görebilir.
    /// Karşılığı, üç değerlik tabloya dördüncü bir sütun uydurmak olurdu —
    /// uydurulan sayı, olmayan bir kademeden kötü.
    /// </remarks>
    public static bool CanStretch(AgeBand band) => band != AgeBand.Mese;

    /// <summary>Kademenin gerçekte hangi bandın ayarlarını okuduğu.</summary>
    public static AgeBand KnobBand(AgeBand band, DifficultyStep step) =>
        step == DifficultyStep.Stretch && CanStretch(band)
            ? band + 1
            : band;

    /// <summary>Tur çocuğa zor gelmemiş mi?</summary>
    /// <remarks>
    /// Üç yıldız tek başına yetmiyor: Fidan'da bir hata da üç yıldız veriyor
    /// ve Filiz'de bitiren herkes üç alıyor. Sıfır hata koşulu, ölçütü üç
    /// bantta da aynı şeye bağlıyor.
    /// </remarks>
    public static bool IsPerfect(PlayedRound round) =>
        round.Stars == StarRating.MaxStars && round.Mistakes == 0;

    /// <summary>
    /// Bir oyunun bu profildeki güncel kademesi.
    /// </summary>
    /// <param name="band">Çocuğun bandı. Kademe bandı değiştirmiyor.</param>
    /// <param name="newestFirst">
    /// <b>Yalnızca o oyunun</b> turları, yeniden eskiye. Başka bandın turları
    /// eleniyor: bant değiştiren çocuk yeni bandına eski bandındaki
    /// ustalığıyla girmemeli.
    /// </param>
    public static DifficultyStep Evaluate(AgeBand band, IEnumerable<PlayedRound> newestFirst)
    {
        if (!CanStretch(band))
        {
            return DifficultyStep.Base;
        }

        var seen = 0;

        foreach (var round in newestFirst)
        {
            if (round.Band != band)
            {
                continue;
            }

            if (!IsPerfect(round))
            {
                return DifficultyStep.Base;
            }

            if (++seen == PerfectRoundsToStretch)
            {
                return DifficultyStep.Stretch;
            }
        }

        // Yeterince tur yok. Yeni bir oyun her zaman bandın kendi ayarlarıyla
        // başlıyor — ilk turdan zorlaşan bir oyun kimseye bir şey anlatmaz.
        return DifficultyStep.Base;
    }
}
