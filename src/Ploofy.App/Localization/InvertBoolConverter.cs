using System.Globalization;

namespace Ploofy.App.Localization;

/// <summary>
/// <c>IsVisible="{Binding IsMultiplayer, Converter={StaticResource Not}}"</c>.
/// </summary>
/// <remarks>
/// Aynı ekranın iki hâli (tek kişilik / sıralı) tek bir bayrakla yönetiliyor;
/// ikinci bir "IsSolo" özelliği tutmak, ikisinin birbiriyle tutarsız kalma
/// ihtimalini getirirdi.
/// </remarks>
public sealed class InvertBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not bool flag || !flag;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not bool flag || !flag;
}
