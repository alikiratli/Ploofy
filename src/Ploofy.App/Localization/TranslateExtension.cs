using System.Globalization;

namespace Ploofy.App.Localization;

/// <summary>
/// XAML'de metin bağlar: <c>Text="{loc:Translate HomeTitle}"</c>.
/// </summary>
/// <remarks>
/// Sabit bir metin döndürmek yerine bir bağ (binding) döndürüyor. Sebebi:
/// ayarlardan dil değiştirildiğinde açık duran ekran yeniden kurulmadan
/// güncelleniyor.
/// </remarks>
[ContentProperty(nameof(Key))]
// Hizmet sağlayıcıya ihtiyaç duymuyor; XAML derleyicisi bunu bilmeden
// uzantıyı derleyemiyor.
[AcceptEmptyServiceProvider]
public sealed class TranslateExtension : IMarkupExtension<BindingBase>
{
    public string Key { get; set; } = string.Empty;

    public BindingBase ProvideValue(IServiceProvider serviceProvider) => new Binding
    {
        Mode = BindingMode.OneWay,
        Path = $"[{Key}]",
        Source = LocalizationService.Instance,
    };

    object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider) =>
        ProvideValue(serviceProvider);
}

/// <summary>
/// Yer tutuculu metinleri biçimlendirmek için dönüştürücü:
/// <c>{Binding Name, Converter={StaticResource FormatWith}, ConverterParameter=HandoffTitle}</c>.
/// </summary>
public sealed class FormatWithConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = parameter as string ?? string.Empty;
        return LocalizationService.Instance.Format(key, value ?? string.Empty);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
