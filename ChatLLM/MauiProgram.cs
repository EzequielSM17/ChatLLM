using Views;
using Microsoft.Extensions.Logging;
using Services;
using ViewModels;

namespace ChatLLM
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
    		builder.Logging.AddDebug();
#endif
            // 1. Registrar el servicio como Singleton
            builder.Services.AddSingleton<ChatService>();

            // 2. Registrar el ViewModel
            builder.Services.AddSingleton<ChatViewModel>();

            // 3. Registrar la Vista
            builder.Services.AddTransient<MainPage>();
            builder.Services.AddTransient<SettingsPage>();
            return builder.Build();
        }
    }
}
