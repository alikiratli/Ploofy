using Microsoft.Extensions.Logging;
using Ploofy.App.Localization;
using Ploofy.App.Services;
using Ploofy.App.ViewModels;
using Ploofy.App.Views;
using Ploofy.Data;
using Ploofy.Ui.Feedback;
using Ploofy.Ui.Parental;
using Plugin.Maui.Audio;
using SkiaSharp.Views.Maui.Controls.Hosting;

namespace Ploofy.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseSkiaSharp()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        RegisterServices(builder.Services);
        RegisterPages(builder.Services);

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }

    private static void RegisterServices(IServiceCollection services)
    {
        // Veritabanı uygulamanın kendi veri klasöründe. Yedeklenmiyor,
        // dışarı gönderilmiyor — çocuğa ait her şey bu dosyada ve cihazda kalıyor.
        services.AddSingleton(_ => new ProgressDatabase(
            Path.Combine(FileSystem.AppDataDirectory, "progress.db3")));
        services.AddSingleton<ProgressRepository>();

        services.AddSingleton(AudioManager.Current);
        services.AddSingleton<IFeedbackService, FeedbackService>();

        services.AddSingleton<ISubscriptionService, LocalSubscriptionService>();
        services.AddSingleton<AppState>();

        // Oyun akışının sayfalar arasında taşıdığı durum (seçili oyun, oturum,
        // biten turun özeti). Tek örnek, çünkü akış tek yönlü ve tek seferlik.
        services.AddSingleton<PlayFlow>();

        // Kilit metinleri uygulamanın kaynak dosyalarından geliyor; arayüz
        // katmanı üç dili tanımıyor.
        services.AddSingleton<IParentalGateService>(_ => new ParentalGateService(() =>
        {
            var l = LocalizationService.Instance;
            return new ParentalGateStrings(
                Title: l["ParentalGateTitle"],
                Hint: l["ParentalGateHint"],
                QuestionFormat: l["ParentalGateQuestion"],
                WrongAnswer: l["ParentalGateWrong"],
                Cancel: l["CommonCancel"],
                Ok: l["CommonOk"]);
        }));
    }

    private static void RegisterPages(IServiceCollection services)
    {
        services.AddSingleton<AppShell>();

        services.AddTransient<ProfilePickerPage>();
        services.AddTransient<ProfilePickerViewModel>();

        services.AddTransient<ProfileEditorPage>();
        services.AddTransient<ProfileEditorViewModel>();

        services.AddTransient<HomePage>();
        services.AddTransient<HomeViewModel>();

        services.AddTransient<PlaySetupPage>();
        services.AddTransient<PlaySetupViewModel>();

        services.AddTransient<MemoryMatchPage>();
        services.AddTransient<MemoryMatchViewModel>();

        services.AddTransient<BubblePopPage>();
        services.AddTransient<BubblePopViewModel>();

        services.AddTransient<ShapeSortPage>();
        services.AddTransient<ShapeSortViewModel>();

        services.AddTransient<HuntPage>();
        services.AddTransient<HuntViewModel>();

        services.AddTransient<CountMatchPage>();
        services.AddTransient<CountMatchViewModel>();

        services.AddTransient<SimonPage>();
        services.AddTransient<SimonViewModel>();

        services.AddTransient<BasketCatchPage>();
        services.AddTransient<BasketCatchViewModel>();

        services.AddTransient<MazeTracePage>();
        services.AddTransient<MazeTraceViewModel>();

        services.AddTransient<JigsawPage>();
        services.AddTransient<JigsawViewModel>();

        services.AddTransient<LetterTracePage>();
        services.AddTransient<LetterTraceViewModel>();

        services.AddTransient<PatternPage>();
        services.AddTransient<PatternViewModel>();

        services.AddTransient<RoundResultPage>();
        services.AddTransient<RoundResultViewModel>();

        services.AddTransient<SettingsPage>();
        services.AddTransient<SettingsViewModel>();

        services.AddTransient<PaywallPage>();
        services.AddTransient<PaywallViewModel>();

        services.AddTransient<SubscriptionPage>();
        services.AddTransient<SubscriptionViewModel>();

        services.AddTransient<ReportPage>();
        services.AddTransient<ReportViewModel>();
    }
}
