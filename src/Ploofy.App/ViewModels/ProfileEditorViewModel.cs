using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ploofy.App.Localization;
using Ploofy.App.Services;
using Ploofy.Data;
using Ploofy.Engine;

namespace Ploofy.App.ViewModels;

/// <summary>Yaş bandı seçeneği — adı ve açıklamasıyla.</summary>
public sealed class BandOption(AgeBand band)
{
    public AgeBand Band { get; } = band;

    public string Name => LocalizationService.Instance[Band switch
    {
        AgeBand.Filiz => "BandFiliz",
        AgeBand.Fidan => "BandFidan",
        _ => "BandMese",
    }];

    public string AgeRange
    {
        get
        {
            var (min, max) = Band.AgeRange();
            return LocalizationService.Instance.Format("BandAgeRange", min, max);
        }
    }

    public string Hint => LocalizationService.Instance[Band switch
    {
        AgeBand.Filiz => "BandFilizHint",
        AgeBand.Fidan => "BandFidanHint",
        _ => "BandMeseHint",
    }];
}

/// <summary>
/// Yeni çocuk profili oluşturma.
/// </summary>
/// <remarks>
/// Ebeveyn kilidinin arkasından açılıyor. İstenen tek şey takma ad, yaş bandı
/// ve avatar; gerçek ad, doğum tarihi ya da başka bir kişisel veri sorulmuyor —
/// toplanmayan veri korunması gereken veri değil.
/// </remarks>
public sealed partial class ProfileEditorViewModel : ObservableObject
{
    private readonly ProgressRepository _repository;
    private readonly AppState _state;

    /// <summary>
    /// Varsayılanlar kurucuda: ekran açılır açılmaz orta bant ve ilk avatar
    /// seçili geliyor, yani ebeveyn yalnızca adı yazıp kaydedebiliyor.
    /// </summary>
    public ProfileEditorViewModel(ProgressRepository repository, AppState state)
    {
        _repository = repository;
        _state = state;

        DisplayName = string.Empty;
        SelectedBand = new BandOption(AgeBand.Fidan);
        SelectedAvatar = Avatars[0];
    }

    /// <summary>
    /// Avatar seçenekleri. Görsel dosya değil emoji: üç dilde de aynı, ek
    /// varlık gerektirmiyor ve her platformda renkli görünüyor.
    /// </summary>
    public static readonly IReadOnlyList<string> Avatars =
        ["🦊", "🐻", "🐼", "🐸", "🦁", "🐧", "🐢", "🐙", "🦉", "🐝", "🦕", "🐰"];

    [ObservableProperty]
    public partial string DisplayName { get; set; }

    [ObservableProperty]
    public partial BandOption SelectedBand { get; set; }

    [ObservableProperty]
    public partial string SelectedAvatar { get; set; }

    [ObservableProperty]
    public partial bool CanSave { get; set; }

    public ObservableCollection<BandOption> Bands { get; } =
        [.. Enum.GetValues<AgeBand>().Select(b => new BandOption(b))];

    public ObservableCollection<string> AvatarChoices { get; } = [.. Avatars];

    partial void OnDisplayNameChanged(string value) =>
        CanSave = !string.IsNullOrWhiteSpace(value);

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!CanSave)
        {
            return;
        }

        var profile = await _repository.CreateProfileAsync(
            DisplayName.Trim(),
            SelectedBand.Band,
            SelectedAvatar);

        // Yeni eklenen çocuk doğrudan oynamaya başlasın: ebeveyn profili
        // oluşturduktan sonra bir de listeden seçmek zorunda kalmasın.
        await _state.SetActiveProfileAsync(profile);
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private static async Task CancelAsync() => await Shell.Current.GoToAsync("..");
}
