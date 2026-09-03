using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ploofy.App.Localization;
using Ploofy.App.Services;
using Ploofy.Data;
using Ploofy.Engine;
using Ploofy.Engine.Progress;

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

/// <summary>Seçilebilir tek avatar.</summary>
/// <remarks>
/// Kilit durumu değişebiliyor: ızgara ekran açılırken kuruluyor, çocuğun
/// yıldızı ise profil okunduktan sonra biliniyor.
/// </remarks>
public sealed partial class AvatarChoice(string emoji, int requiredStars) : ObservableObject
{
    public string Emoji { get; } = emoji;

    /// <summary>Açılması için gereken toplam yıldız; başlangıçtan açıksa sıfır.</summary>
    public int RequiredStars { get; } = requiredStars;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLocked))]
    [NotifyPropertyChangedFor(nameof(Opacity))]
    public partial bool IsUnlocked { get; set; }

    public bool IsLocked => !IsUnlocked;

    /// <summary>Kilitli avatarın altındaki "5 yıldız" etiketi.</summary>
    public string RequirementText => $"{RequiredStars} ★";

    public double Opacity => IsUnlocked ? 1.0 : 0.4;
}

/// <summary>
/// Günlük oyun süresi seçeneği: bir dakika değeri ya da "sınırsız".
/// </summary>
public sealed partial class ScreenTimeOption(int minutes) : ObservableObject
{
    /// <summary>Dakika; sınırsızsa <see cref="ScreenTimeBudget.Unlimited"/>.</summary>
    public int Minutes { get; } = minutes;

    public bool IsUnlimited => Minutes <= ScreenTimeBudget.Unlimited;

    public string Label => IsUnlimited
        ? LocalizationService.Instance["ScreenTimeUnlimited"]
        : LocalizationService.Instance.Format("ScreenTimeMinutes", Minutes);

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

/// <summary>Ekrandaki tek avatar grubu — başlığı ve seçenekleriyle.</summary>
public sealed class AvatarGroupVm(string nameKey, IReadOnlyList<AvatarChoice> choices)
{
    public string Name => LocalizationService.Instance[nameKey];

    public IReadOnlyList<AvatarChoice> Choices { get; } = choices;
}

/// <summary>
/// Çocuk profili oluşturma ve düzenleme.
/// </summary>
/// <remarks>
/// <para>
/// Ebeveyn kilidinin arkasından açılıyor. İstenen tek şey takma ad, yaş bandı
/// ve avatar; gerçek ad, doğum tarihi ya da başka bir kişisel veri sorulmuyor —
/// toplanmayan veri korunması gereken veri değil.
/// </para>
/// <para>
/// Aynı ekran iki iş görüyor: <c>profileeditor</c> yolu boş gelirse yeni
/// profil, <c>profileeditor?profileId=3</c> gelirse o profilin düzenlenmesi.
/// İkisi için ayrı ekran yazmanın anlamı yok — sorulan üç şey aynı ve
/// düzenleme ekranı ayrı yazılsaydı avatar ızgarası iki yerde durup birinde
/// güncellenirdi.
/// </para>
/// <para>
/// Yaş bandı da düzenlenebiliyor: çocuk büyüyor ve profili silip yeniden
/// kurmak bütün yıldızlarını siler. Bant değiştiğinde eski yıldızlar
/// duruyor; ilerleme oyun <b>ve</b> bant başına tutuluyor.
/// </para>
/// </remarks>
public sealed partial class ProfileEditorViewModel : ObservableObject, IQueryAttributable
{
    /// <summary>Düzenlemeye açan gezinme parametresi.</summary>
    public const string ProfileIdParameter = "profileId";

    private readonly ProgressRepository _repository;
    private readonly AppState _state;

    /// <summary>Düzenlenen profil; yeni profil oluşturuluyorsa boş.</summary>
    private ChildProfileRow? _editing;

    private int? _requestedId;
    private bool _loaded;

    /// <summary>
    /// Varsayılanlar kurucuda: ekran açılır açılmaz orta bant ve ilk avatar
    /// seçili geliyor, yani ebeveyn yalnızca adı yazıp kaydedebiliyor.
    /// </summary>
    public ProfileEditorViewModel(ProgressRepository repository, AppState state)
    {
        _repository = repository;
        _state = state;

        DisplayName = string.Empty;

        // Listedeki örneğin kendisi seçiliyor, eşdeğeri değil: CollectionView
        // seçili öğeyi referansla buluyor, yeni bir BandOption vermek
        // varsayılan bandı ekranda seçili göstermiyordu.
        SelectedBand = Bands.First(b => b.Band == AgeBand.Fidan);
        SelectedAvatar = AvatarCatalog.Default;
        HeaderText = LocalizationService.Instance["AddProfile"];

        ScreenTimeMinutes = ScreenTimeBudget.Unlimited;
        ScreenTimeHint = LocalizationService.Instance["ScreenTimeHint"];

        // Izgara kilitli hâliyle kuruluyor: yeni profilin yıldızı yok, yani
        // yalnızca başlangıçtan açık olanlar seçilebilir. Düzenleme açıldığında
        // LoadAsync o çocuğun yıldızına göre kilitleri kaldırıyor.
        AvatarGroups =
        [
            .. AvatarCatalog.Groups.Select(group => new AvatarGroupVm(
                group.NameKey,
                [.. group.Avatars.Select(emoji => new AvatarChoice(
                    emoji,
                    AvatarCatalog.RequiredStars(emoji))
                {
                    IsSelected = emoji == AvatarCatalog.Default,
                    IsUnlocked = AvatarCatalog.IsUnlocked(emoji, 0),
                })])),
        ];
    }

    [ObservableProperty]
    public partial string DisplayName { get; set; }

    [ObservableProperty]
    public partial BandOption SelectedBand { get; set; }

    [ObservableProperty]
    public partial string SelectedAvatar { get; set; }

    [ObservableProperty]
    public partial bool CanSave { get; set; }

    /// <summary>Seçili günlük sınır, dakika. Sıfır = sınırsız.</summary>
    [ObservableProperty]
    public partial int ScreenTimeMinutes { get; set; }

    /// <summary>
    /// Sınırın altındaki açıklama — neyin sayıldığını söylüyor.
    /// </summary>
    /// <remarks>
    /// Ebeveynin "ekran süresi" beklediği yerde "oyun süresi" ölçülüyor:
    /// ana ekranda ya da koleksiyonda geçen süre sayılmıyor. Bunu yazmamak,
    /// raporla sınırın neden aynı sayıyı verdiğini de açıklamamak olurdu.
    /// </remarks>
    [ObservableProperty]
    public partial string ScreenTimeHint { get; set; } = string.Empty;

    /// <summary>Ekranın başlığı — "Çocuk ekle" ya da "Çocuğu düzenle".</summary>
    [ObservableProperty]
    public partial string HeaderText { get; set; }

    /// <summary>
    /// Günlük oyun süresi seçenekleri; ilki "sınırsız".
    /// </summary>
    /// <remarks>
    /// Sınırsız <b>başta</b> ve varsayılan olarak seçili. Sıralamanın sonuna
    /// konsaydı sınır koymak varsayılan gibi görünürdü; oysa uygulamanın
    /// duruşu, sınırın ebeveynin bilinçli bir kararı olması.
    /// </remarks>
    public IReadOnlyList<ScreenTimeOption> ScreenTimeOptions { get; } =
    [
        new(ScreenTimeBudget.Unlimited) { IsSelected = true },
        .. ScreenTimeBudget.Choices.Select(m => new ScreenTimeOption(m)),
    ];

    public ObservableCollection<BandOption> Bands { get; } =
        [.. Enum.GetValues<AgeBand>().Select(b => new BandOption(b))];

    /// <summary>Avatarlar temalarına göre gruplu duruyor.</summary>
    /// <remarks>
    /// Otuz iki simgeyi tek bir ızgaraya dökmek, ebeveyni de çocuğu da
    /// aradığını bulamaz hâle getiriyor. Başlıklar aramayı "hangi grupta
    /// olurdu" sorusuna indirgiyor.
    /// </remarks>
    public IReadOnlyList<AvatarGroupVm> AvatarGroups { get; }

    partial void OnDisplayNameChanged(string value) =>
        CanSave = !string.IsNullOrWhiteSpace(value);

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _requestedId = query.TryGetValue(ProfileIdParameter, out var value)
            && int.TryParse(value?.ToString(), out var id)
                ? id
                : null;
    }

    /// <summary>
    /// Düzenleme açıldıysa profili okur ve alanları doldurur.
    /// </summary>
    /// <remarks>
    /// Bir kez çalışıyor: sayfa uygulama arka plandan döndüğünde de
    /// görünüyor ve ikinci bir yükleme ebeveynin yazdığı adı geri alırdı.
    /// </remarks>
    public async Task LoadAsync()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;

        if (_requestedId is not { } id)
        {
            return;
        }

        var existing = await _repository.ProfileByIdAsync(id);
        _editing = existing;
        if (existing is null)
        {
            // Profil arada silinmiş; boş bir düzenleme ekranı göstermek yerine
            // geri dönülüyor.
            await Shell.Current.GoToAsync("..");
            return;
        }

        HeaderText = LocalizationService.Instance["EditProfile"];
        DisplayName = existing.DisplayName;

        // Kazanılmış avatarların kilidi açılıyor. Çocuğun o an takındığı
        // avatar her koşulda açık: katalog sırası ileride değişirse bile
        // ebeveyn, çocuğun simgesini "kilitli" görüp değiştirmek zorunda
        // kalmamalı.
        var totalStars = await _repository.TotalStarsAsync(existing.Id);
        foreach (var option in AvatarGroups.SelectMany(g => g.Choices))
        {
            option.IsUnlocked = AvatarCatalog.IsUnlocked(option.Emoji, totalStars)
                || option.Emoji == existing.AvatarId;
        }

        var band = AgeBandExtensions.FromId(existing.AgeBandId);
        SelectedBand = Bands.First(b => b.Band == band);

        var limit = await _repository.ScreenTimeLimitAsync(existing.Id);
        SelectScreenTime(ScreenTimeOptions.FirstOrDefault(o => o.Minutes == limit));

        // Avatar katalogdan çıkarılmışsa (eski bir profil) seçim varsayılanda
        // kalıyor; ekranın boş bir seçimle açılması daha kötü.
        var avatar = AvatarGroups
            .SelectMany(g => g.Choices)
            .FirstOrDefault(c => c.Emoji == existing.AvatarId);

        if (avatar is not null)
        {
            SelectAvatar(avatar);
        }
    }

    /// <summary>
    /// Avatarı seçer.
    /// </summary>
    /// <remarks>
    /// Seçim <c>CollectionView.SelectedItem</c> ile değil elle yürütülüyor:
    /// seçenekler artık gruplara bölündü ve her grubun kendi seçimi olsaydı
    /// ekranda aynı anda üç seçili avatar görünürdü.
    /// </remarks>
    [RelayCommand]
    private void SelectAvatar(AvatarChoice? choice)
    {
        // Kilitli avatara dokunmak sessizce yok sayılıyor. Gereken yıldız
        // zaten simgenin altında yazılı; ayrıca bir uyarı çıkarmak ebeveyni
        // kapatması gereken bir kutuyla karşılamak olurdu.
        if (choice is null || choice.IsLocked)
        {
            return;
        }

        SelectedAvatar = choice.Emoji;

        foreach (var option in AvatarGroups.SelectMany(g => g.Choices))
        {
            option.IsSelected = ReferenceEquals(option, choice);
        }
    }

    /// <summary>
    /// Günlük oyun süresi sınırını seçer.
    /// </summary>
    /// <remarks>
    /// Kaydedilmiş bir sınır listede yoksa (seçenekler ileride değişirse)
    /// seçim sınırsıza düşmüyor — ekranda hiçbir şey seçili görünmemesi,
    /// yanlış bir sınırı doğru göstermekten dürüst.
    /// </remarks>
    [RelayCommand]
    private void SelectScreenTime(ScreenTimeOption? choice)
    {
        if (choice is null)
        {
            return;
        }

        ScreenTimeMinutes = choice.Minutes;

        foreach (var option in ScreenTimeOptions)
        {
            option.IsSelected = ReferenceEquals(option, choice);
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!CanSave)
        {
            return;
        }

        if (_editing is { } row)
        {
            row.DisplayName = DisplayName.Trim();
            row.AgeBandId = SelectedBand.Band.ToId();
            row.AvatarId = SelectedAvatar;
            await _repository.UpdateProfileAsync(row);
            await _repository.SetScreenTimeLimitAsync(row.Id, ScreenTimeMinutes);

            // Düzenlenen çocuk o an oynayan çocuksa ana ekrandaki ad, avatar
            // ve bant hemen tazelenmeli: bant zorluğu belirliyor ve eski
            // değerle açılan bir oyun yanlış bantta oynanırdı.
            if (_state.ActiveProfile?.Id == row.Id)
            {
                await _state.RefreshActiveProfileAsync();
            }

            await Shell.Current.GoToAsync("..");
            return;
        }

        var profile = await _repository.CreateProfileAsync(
            DisplayName.Trim(),
            SelectedBand.Band,
            SelectedAvatar);

        await _repository.SetScreenTimeLimitAsync(profile.Id, ScreenTimeMinutes);

        // Yeni eklenen çocuk doğrudan oynamaya başlasın: ebeveyn profili
        // oluşturduktan sonra bir de listeden seçmek zorunda kalmasın.
        await _state.SetActiveProfileAsync(profile);
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private static async Task CancelAsync() => await Shell.Current.GoToAsync("..");
}
