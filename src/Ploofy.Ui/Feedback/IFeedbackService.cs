namespace Ploofy.Ui.Feedback;

/// <summary>
/// Çocuğun bir şey yaptığında aldığı anlık geri bildirim.
/// </summary>
/// <remarks>
/// Bu yaş grubunda "başarı hissi"nin büyük kısmı buradan geliyor: doğru
/// dokunuşta kısa neşeli bir ses ve hafif titreşim, ekrandaki animasyondan
/// daha güçlü çalışıyor. Tek arayüz arkasında toplanmasının sebebi, ses ve
/// titreşimin ayarlardan birlikte kapatılabilmesi ve her oyunun aynı
/// sözlüğü kullanması.
/// </remarks>
public interface IFeedbackService
{
    bool SoundEnabled { get; set; }

    bool HapticsEnabled { get; set; }

    ValueTask PlayAsync(FeedbackCue cue);
}

/// <summary>
/// Oyunların kullanabileceği geri bildirim sözlüğü.
/// </summary>
/// <remarks>
/// Oyunlar dosya adı ya da titreşim süresi seçmez, yalnızca "ne oldu"yu
/// söyler. Sesler ve titreşim şiddeti tek yerde ayarlandığı için uygulama
/// baştan sona aynı dili konuşuyor.
/// </remarks>
public enum FeedbackCue
{
    /// <summary>Kart çevrildi, obje tutuldu — nötr bir dokunuş.</summary>
    Tap,

    /// <summary>Doğru eşleşme, doğru kutu.</summary>
    Correct,

    /// <summary>
    /// Yanlış. Filiz ve Fidan bantlarında ceza yok; bu ipucu "kaybettin"
    /// değil "tekrar dene" tonunda olmalı.
    /// </summary>
    Retry,

    /// <summary>Tur tamamlandı.</summary>
    RoundComplete,

    /// <summary>Yıldız kazanıldı.</summary>
    StarEarned,

    /// <summary>Sıra devrediliyor — cihaz kardeşe uzatılıyor.</summary>
    Handoff,

    /// <summary>Kilitli bir şeye dokunuldu.</summary>
    Locked,

    /// <summary>
    /// Sırayı Tekrarla'nın birinci tuşu. Altı tuşun altısı ayrı nota taşıyor:
    /// klasik oyunda dizi kulakla da hatırlanıyor ve gösterim bir ezgiye
    /// dönüşüyor — özellikle Filiz bandında, henüz "üçüncü sıradaki" diye
    /// düşünemeyen çocuk için görsel sıradan daha güçlü bir tutamak.
    /// Notalar pentatonik seçildi; dizi hangi sırayla çıkarsa çıksın
    /// uyumsuz bir aralık duyulmuyor.
    /// </summary>
    Pad1,

    /// <summary>Sırayı Tekrarla'nın ikinci tuşu. Bkz. <see cref="Pad1"/>.</summary>
    Pad2,

    /// <summary>Sırayı Tekrarla'nın üçüncü tuşu. Bkz. <see cref="Pad1"/>.</summary>
    Pad3,

    /// <summary>Sırayı Tekrarla'nın dördüncü tuşu. Bkz. <see cref="Pad1"/>.</summary>
    Pad4,

    /// <summary>Sırayı Tekrarla'nın beşinci tuşu. Bkz. <see cref="Pad1"/>.</summary>
    Pad5,

    /// <summary>Sırayı Tekrarla'nın altıncı tuşu. Bkz. <see cref="Pad1"/>.</summary>
    Pad6,
}

/// <summary>Sözlüğün sıra ile adres arasındaki köprüsü.</summary>
public static class FeedbackCues
{
    private static readonly FeedbackCue[] Pads =
    [
        FeedbackCue.Pad1, FeedbackCue.Pad2, FeedbackCue.Pad3,
        FeedbackCue.Pad4, FeedbackCue.Pad5, FeedbackCue.Pad6,
    ];

    /// <summary>Tuş sırasının sesi. Sıra havuzdan taşarsa başa dönüyor.</summary>
    public static FeedbackCue Pad(int index) => Pads[((index % Pads.Length) + Pads.Length) % Pads.Length];
}
