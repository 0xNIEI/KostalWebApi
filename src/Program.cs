using KostalModbusClient;
using System.Text.Json;

namespace KostalWebApi
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            var MyAllowSpecificOrigins = "_myAllowSpecificOrigins";

            // Add services to the container.
            builder.Services.AddAuthorization();

            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            builder.Services.AddCors(options =>
            {
                options.AddPolicy(name: MyAllowSpecificOrigins, policy =>
                {
                    policy.WithMethods("GET").AllowAnyOrigin().AllowAnyHeader();
                });
            });

            var plentiCoreIpAddress = builder.Configuration.GetSection("KostalPlenticoreIpAddress").Value;

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();
            app.UseCors();
            app.UseAuthorization();

            app.MapGet("/data", async (HttpContext httpContext) =>
            {
                if (string.IsNullOrWhiteSpace(plentiCoreIpAddress))
                {
                    return JsonSerializer.Serialize("Plenticore IP Address not set in Appsettings");
                }

                var client = new ModbusClient(plentiCoreIpAddress);

                var production = string.Empty;
                var consumption = string.Empty;
                var grid = string.Empty;
                var feedIn = false;

                var totalDcPowerProduced = (await client.GetTotalDcPower()).Value;

                production = totalDcPowerProduced >= 1000.00
                    ? Math.Round(totalDcPowerProduced / 1000, 1).ToString() + " kW"
                    : Math.Round(totalDcPowerProduced).ToString() + " W";

                var consumptionFromPv = (await client.GetHomeOwnConsumptionFromPv()).Value;
                var consumptionFromGrid = (await client.GetHomeOwnConsumptionFromGrid()).Value;

                var inverterOutput = (await client.GetTotalAcActivePower()).Value;

                if (Math.Round(consumptionFromGrid) == 0)
                {
                    feedIn = true;

                    consumption = consumptionFromPv >= 1000.00
                    ? Math.Round(consumptionFromPv / 1000, 1).ToString() + " kW"
                    : Math.Round(consumptionFromPv).ToString() + " W";

                    var gridTemp = inverterOutput - consumptionFromPv;

                    grid = gridTemp >= 1000.00
                    ? Math.Round(gridTemp / 1000, 1).ToString() + " kW"
                    : Math.Round(gridTemp).ToString() + " W";
                }
                else
                {
                    feedIn = false;

                    var consumptionTemp = consumptionFromPv + consumptionFromGrid;

                    consumption = consumptionTemp >= 1000.00
                    ? Math.Round(consumptionTemp / 1000, 1).ToString() + " kW"
                    : Math.Round(consumptionTemp).ToString() + " W";

                    grid = consumptionFromGrid >= 1000.00
                    ? Math.Round(consumptionFromGrid / 1000, 1).ToString() + " kW"
                    : Math.Round(consumptionFromGrid).ToString() + " W";
                }

                var result = new Data
                {
                    consumption = consumption,
                    production = production,
                    grid = grid,
                    feedIn = feedIn
                };

                return JsonSerializer.Serialize(result);
            })
            .WithName("GetData").RequireCors(MyAllowSpecificOrigins);

            app.Use(async (context, next) =>
            {
                try
                {
                    context.Response.Headers.Append("Content-Security-Policy", "default-src 'none'; font-src 'none'; img-src 'none'; object-src 'none'; script-src 'none'; style-src 'none'; connect-src 'self'; base-uri 'none'; form-action 'none'; frame-ancestors 'none';");
                    context.Response.Headers.Append("X-Xss-Protection", "1; mode=block");
                    context.Response.Headers.Append("X-Frame-Options", "DENY");
                    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
                    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
                }
                catch
                {
                    // dont do anything because the headers are already added so we don't care
                }

                await next();
            });

            app.Run();
        }
    }
}