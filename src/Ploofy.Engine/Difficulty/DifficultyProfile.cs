namespace Ploofy.Engine.Difficulty;

public enum CelebrationIntensity
{
    Low,
    Medium,
    High,
}

/// <summary>
/// Bütün mini oyunların uymak zorunda olduğu ortak davranış kuralları.
/// </summary>
/// <remarks>
/// Oyuna özel knob'lar (<see cref="BandValue{T}"/> ile) oyunun kendi
/// dosyasında durur; burada yalnızca her oyunun uyması gereken ortak sözleşme
/// var. Arayüz katmanı bu bayrakları okuyup zamanlayıcıyı, "yanlış" geri
/// bildirimini ve yazılı metni buna göre gösterir ya da hiç göstermez.
/// </remarks>
public sealed record DifficultyProfile
{
    private DifficultyProfile(AgeBand band)
    {
        Band = band;
    }

    public AgeBand Band { get; private init; }

    /// <summary>Oyun kaybedilebilir mi? false ise tur yalnızca tamamlanabilir.</summary>
    public bool CanFail { get; private init; }

    public bool ShowsTimer { get; private init; }

    /// <summary>
    /// Ekranda okumaya bağımlı metin gösterilebilir mi? false ise yönerge
    /// ikon + sesle verilmek zorunda.
    /// </summary>
    public bool UsesWrittenText { get; private init; }

    /// <summary>Çocuk bir süre hareketsiz kalırsa sesli yönerge tekrarlansın mı?</summary>
    public bool RepeatsVoicePrompt { get; private init; }

    public CelebrationIntensity Celebration { get; private init; }

    /// <summary>
    /// Banda karşılık gelen standart profil. Oyunlar bunu doğrudan kullanır;
    /// istisna gerekirse <c>with</c> ile tek alan değiştirilir.
    /// </summary>
    public static DifficultyProfile For(AgeBand band) => band switch
    {
        // Filiz'de kaybetme yok: yanlış dokunuş sessizce yok sayılır, obje
        // yerine geri döner. Ekranda tek bir yazı yok, yönerge sadece sesle.
        AgeBand.Filiz => new DifficultyProfile(AgeBand.Filiz)
        {
            CanFail = false,
            ShowsTimer = false,
            UsesWrittenText = false,
            RepeatsVoicePrompt = true,
            Celebration = CelebrationIntensity.High,
        },

        // Fidan'da hedef var ama ceza yok; harf/sayı görsel olarak da yazılır.
        AgeBand.Fidan => new DifficultyProfile(AgeBand.Fidan)
        {
            CanFail = false,
            ShowsTimer = false,
            UsesWrittenText = true,
            RepeatsVoicePrompt = true,
            Celebration = CelebrationIntensity.High,
        },

        // Meşe'de zamanlayıcı ve gerçek başarısızlık var; kutlama daha ölçülü,
        // çünkü bu yaşta abartılı kutlama "bebek işi" hissi veriyor.
        AgeBand.Mese => new DifficultyProfile(AgeBand.Mese)
        {
            CanFail = true,
            ShowsTimer = true,
            UsesWrittenText = true,
            RepeatsVoicePrompt = false,
            Celebration = CelebrationIntensity.Medium,
        },

        _ => throw new ArgumentOutOfRangeException(nameof(band), band, null),
    };
}
