using System.ComponentModel;
using Andes.Agents.Common.Constants;
using Microsoft.Extensions.AI;

namespace Andes.Agents.Service.Weather.Tools;

/// <summary>The function tools the weather agent calls: resolve a place, read its weather, read its forecast.</summary>
/// <remarks>
/// Expected failures come back as <see cref="ToolError"/> results rather than exceptions, so the model
/// can correct its arguments instead of the turn failing.
/// </remarks>
public sealed class WeatherToolProvider(IWeatherService weather)
{
    private readonly IWeatherService _weather = weather;

    /// <summary>Builds the tool list handed to the agent.</summary>
    public IList<AITool> CreateTools() =>
    [
        AIFunctionFactory.Create(
            SearchLocationAsync,
            name: "search_location",
            description: "Find the coordinates of a city or place by name. Call this before asking for weather."),
        AIFunctionFactory.Create(
            GetCurrentWeatherAsync,
            name: "get_current_weather",
            description: "Get the weather right now at a coordinate returned by search_location."),
        AIFunctionFactory.Create(
            GetDailyForecastAsync,
            name: "get_daily_forecast",
            description: "Get a day-by-day forecast, today included, for up to seven days at a coordinate returned by search_location."),
    ];

    [Description("Find places matching a name. Returns one or more candidates with coordinates.")]
    private async Task<object> SearchLocationAsync(
        [Description("City name, optionally followed by its country, such as 'Paris' or 'Paris, France'.")] string query,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new ToolError("Provide a city or place name to search for.");
        }

        IReadOnlyList<LocationResult> places = await _weather.SearchLocationsAsync(query.Trim(), cancellationToken).ConfigureAwait(false);

        return places.Count == 0
            ? new ToolError($"No place matched '{query}'. Ask the user for the nearest large city, then search for that.")
            : places;
    }

    [Description("Read the current weather at a coordinate.")]
    private async Task<object> GetCurrentWeatherAsync(
        [Description("Display name of the place, as returned by search_location.")] string locationName,
        [Description("Latitude in decimal degrees.")] double latitude,
        [Description("Longitude in decimal degrees.")] double longitude,
        CancellationToken cancellationToken)
    {
        if (ValidateCoordinate(latitude, longitude) is { } error)
        {
            return error;
        }

        return await _weather.GetCurrentConditionsAsync(locationName, latitude, longitude, cancellationToken).ConfigureAwait(false);
    }

    [Description("Read the daily forecast at a coordinate.")]
    private async Task<object> GetDailyForecastAsync(
        [Description("Display name of the place, as returned by search_location.")] string locationName,
        [Description("Latitude in decimal degrees.")] double latitude,
        [Description("Longitude in decimal degrees.")] double longitude,
        [Description("Number of days to return, from 1 to 7. Today is day 1.")] int days,
        CancellationToken cancellationToken)
    {
        if (ValidateCoordinate(latitude, longitude) is { } error)
        {
            return error;
        }

        if (days is < 1 or > WeatherLimits.MaxForecastDays)
        {
            return new ToolError($"days must be between 1 and {WeatherLimits.MaxForecastDays}.");
        }

        return await _weather.GetDailyForecastAsync(locationName, latitude, longitude, days, cancellationToken).ConfigureAwait(false);
    }

    private static ToolError? ValidateCoordinate(double latitude, double longitude) =>
        latitude is < -90 or > 90 || longitude is < -180 or > 180 || double.IsNaN(latitude) || double.IsNaN(longitude)
            ? new ToolError("latitude must be between -90 and 90 and longitude between -180 and 180; take them from search_location.")
            : null;
}
