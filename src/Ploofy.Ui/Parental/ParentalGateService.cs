using Ploofy.Engine.Access;

namespace Ploofy.Ui.Parental;

/// <summary>
/// Kilidi sayı klavyeli bir istem olarak gösteren uygulama.
/// </summary>
/// <remarks>
/// <para>
/// Platformun kendi istem penceresi kullanılıyor: özel bir açılır pencere
/// paketine bağımlılık eklemeden, her platformda tanıdık ve erişilebilir bir
/// diyalog çıkıyor. Klavye sayısal, yani çocuk yanlışlıkla harf yazamıyor.
/// </para>
/// <para>
/// Yanlış cevapta soru <b>değişiyor</b>: aynı soruyu deneme yanılmayla geçmek,
/// engeli anlamsız kılardı.
/// </para>
/// </remarks>
public sealed class ParentalGateService(Func<ParentalGateStrings> stringsProvider)
    : IParentalGateService
{
    /// <summary>Üst üste kaç deneme yapılabileceği.</summary>
    private const int MaxAttempts = 3;

    private readonly ParentalGateState _state = new();

    public async Task<bool> RequestAsync(ParentalGateReason reason)
    {
        // Ebeveyn az önce geçtiyse tekrar sorma: ayarlarda gezinirken her
        // ekranda soru çözmek gereksiz.
        if (_state.IsUnlocked)
        {
            return true;
        }

        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page is null)
        {
            return false;
        }

        var strings = stringsProvider();

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var challenge = ParentalGateChallenge.Generate();
            var question = string.Format(
                System.Globalization.CultureInfo.CurrentCulture,
                strings.QuestionFormat,
                challenge.Left,
                challenge.Right,
                challenge.Addend);

            var answer = await page.DisplayPromptAsync(
                title: strings.Title,
                message: $"{strings.Hint}\n\n{question}",
                accept: strings.Ok,
                cancel: strings.Cancel,
                keyboard: Keyboard.Numeric,
                maxLength: 4);

            // İptal edildi — vazgeçmek yanlış cevaptan farklı, tekrar sorma.
            if (answer is null)
            {
                return false;
            }

            if (challenge.Accepts(answer))
            {
                _state.MarkUnlocked();
                return true;
            }

            if (attempt < MaxAttempts - 1)
            {
                await page.DisplayAlert(strings.Title, strings.WrongAnswer, strings.Ok);
            }
        }

        return false;
    }

    public void Lock() => _state.Lock();
}
