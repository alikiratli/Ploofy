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
}
