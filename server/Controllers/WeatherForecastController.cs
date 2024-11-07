using Microsoft.AspNetCore.Mvc;

namespace server.Controllers
{
    [ApiController]
    public class WeatherForecastController : ControllerBase
    {
        /// <summary>
        /// Retrieves the weather forecast for the next few days.
        /// </summary>
        /// <returns>A list of weather forecasts.</returns>
        [HttpGet]
        [Route("GetWeatherForecast")]
        public async Task<IActionResult> Get()
        {
            var summaries = new[]
            {
                "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
            };

            var forecast = Enumerable.Range(1, 5).Select(index =>
            new WeatherForecast
            (
                DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                Random.Shared.Next(-20, 55),
                summaries[Random.Shared.Next(summaries.Length)]
            ))
            .ToArray();
            return Ok(forecast);
        }

        public record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
        {
            public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
        }

    }
}