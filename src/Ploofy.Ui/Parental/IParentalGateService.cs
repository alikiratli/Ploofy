using Ploofy.Engine.Access;

namespace Ploofy.Ui.Parental;

/// <summary>
/// Ebeveyn kilidi kapısı.
/// </summary>
/// <remarks>
/// Kilidin arkasına konması gereken her yer bunu çağırır ve dönen değere
/// bakar. Kilidin nasıl göründüğü, ne kadar açık kaldığı ve sorunun zorluğu
/// tek yerde; ekranlar bunu bilmez.
/// </remarks>
public interface IParentalGateService
{
    /// <summary>
    /// Ebeveynden onay ister. Kilit hâlâ açıksa soru sorulmaz ve doğrudan
    /// true döner.
    /// </summary>
    Task<bool> RequestAsync(ParentalGateReason reason);

    /// <summary>
    /// Kilidi kapatır. Uygulama arka plana atıldığında çağrılıyor — cihaz
    /// çocuğa geri döndüğünde ayarlar açık kalmasın.
    /// </summary>
    void Lock();
}

/// <summary>
/// Kilit sorusunu ekranda gösteren metinler.
/// </summary>
/// <remarks>
/// Arayüz katmanı üç dili tanımıyor (uygulamanın kendi kaynak dosyaları var),
/// bu yüzden metinler dışarıdan veriliyor. Sorunun kendisi sayı olduğu için
/// çevrilmesi gereken tek şey çerçeve metinleri.
/// </remarks>
public sealed record ParentalGateStrings(
    string Title,
    string Hint,
    string QuestionFormat,
    string WrongAnswer,
    string Cancel,
    string Ok);
