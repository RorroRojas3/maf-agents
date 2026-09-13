using Andes.Agents.Service.Weather;
using Andes.Agents.Service.Weather.Tools;

namespace Andes.Agents.Api.Configuration;

internal static class WeatherConfiguration
{
    public static IServiceCollection AddWeather(this IServiceCollection services)
    {
        services.AddSingleton<IWeatherService, WeatherService>();
        services.AddSingleton<WeatherToolProvider>();

        return services;
    }
}
