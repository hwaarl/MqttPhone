using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using MqttPhone.Services;

namespace MqttPhone
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
                })
                ;


#if DEBUG
            builder.Logging.AddDebug();
#endif

#if ANDROID
            // register Config service for Android
            builder.Services.AddSingleton<Services.IAppSettingsService, MqttPhone.Platforms.Android.AppSettingsService>();
            builder.Services.AddSingleton<MqttConfig>(sp => new MqttConfig(sp.GetRequiredService<Services.IAppSettingsService>()));
#endif
            // register MainPage so DI can inject config and service
            builder.Services.AddSingleton<MainPage>();

            return builder.Build();
        }
    }
}
