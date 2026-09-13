namespace Andes.Agents.Service.Weather;

/// <summary>A place the weather tools can report on.</summary>
/// <param name="Name">Place name as commonly written.</param>
/// <param name="Country">Country the place is in.</param>
/// <param name="Latitude">Decimal degrees, north positive.</param>
/// <param name="Longitude">Decimal degrees, east positive.</param>
public sealed record LocationResult(string Name, string Country, double Latitude, double Longitude);

/// <summary>Weather at a place right now.</summary>
/// <param name="Location">Place the reading is for.</param>
/// <param name="ObservedAt">UTC time of the reading.</param>
/// <param name="LocalDate">Calendar date at the place.</param>
/// <param name="Conditions">Short description such as "Partly cloudy".</param>
/// <param name="TemperatureC">Air temperature in Celsius.</param>
/// <param name="TemperatureF">Air temperature in Fahrenheit.</param>
/// <param name="FeelsLikeC">Apparent temperature in Celsius.</param>
/// <param name="HumidityPercent">Relative humidity.</param>
/// <param name="WindSpeedKph">Wind speed in kilometres per hour.</param>
/// <param name="WindDirection">Compass direction the wind blows from.</param>
/// <param name="PrecipitationChancePercent">Chance of precipitation in the coming hours.</param>
public sealed record CurrentConditionsResult(
    string Location,
    DateTimeOffset ObservedAt,
    DateOnly LocalDate,
    string Conditions,
    double TemperatureC,
    double TemperatureF,
    double FeelsLikeC,
    int HumidityPercent,
    double WindSpeedKph,
    string WindDirection,
    int PrecipitationChancePercent);

/// <summary>Forecast for one calendar day.</summary>
/// <param name="Date">The day.</param>
/// <param name="Conditions">Short description of the day's weather.</param>
/// <param name="HighC">Daytime high in Celsius.</param>
/// <param name="LowC">Overnight low in Celsius.</param>
/// <param name="HighF">Daytime high in Fahrenheit.</param>
/// <param name="LowF">Overnight low in Fahrenheit.</param>
/// <param name="PrecipitationChancePercent">Chance of precipitation.</param>
/// <param name="WindSpeedKph">Typical wind speed in kilometres per hour.</param>
public sealed record DailyForecastDay(
    DateOnly Date,
    string Conditions,
    double HighC,
    double LowC,
    double HighF,
    double LowF,
    int PrecipitationChancePercent,
    double WindSpeedKph);

/// <summary>A multi-day forecast for a place.</summary>
/// <param name="Location">Place the forecast is for.</param>
/// <param name="Days">One entry per day, starting today at the place.</param>
public sealed record DailyForecastResult(string Location, IReadOnlyList<DailyForecastDay> Days);

/// <summary>What a tool returns when it cannot answer; the model reads it and recovers.</summary>
/// <param name="Error">What went wrong and what would fix it.</param>
public sealed record ToolError(string Error);
