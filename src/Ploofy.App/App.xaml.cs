using Ploofy.App.Services;
using Ploofy.Ui.Parental;

namespace Ploofy.App;

public partial class App : Application
{
    private readonly AppState _state;
    private readonly IParentalGateService _parentalGate;

    public App(AppState state, IParentalGateService parentalGate)
    {
        InitializeComponent();

        _state = state;
        _parentalGate = parentalGate;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new AppShell());

        // Açılışta ayarlar, dil, abonelik durumu ve son seçili profil yükleniyor.
        window.Created += async (_, _) => await _state.InitializeAsync();

        // Uygulama arka plana atıldığında ebeveyn kilidi yeniden kapanır:
        // cihaz çocuğa geri döndüğünde ayarlar açık kalmasın.
        window.Deactivated += (_, _) => _parentalGate.Lock();

        return window;
    }
}
