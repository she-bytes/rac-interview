using Api.Configuration;
using Asp.Versioning;

namespace Api
{
    // structural notes
    // made the API HTTPS where it was previously HTTP (encrypting the traffic improves security)
    // updated the service to modern .NET 10 (latest LTS version of dotnet) to keep up to date with latest security patches, library support and gain access to modern C# and .NET features
    // added API versioning so future enhancements can be done without breaking changes

    // things I haven't done that could be argued for:
    // any sort of authorisation
    // included OpenAPI UI (Swagger or similar) [though it does produce a consumable JSON file that can be used by clients]
    // any sort of telemetry (this could be hooked into appinsights)
    public class Program
    {
        public static void Main(string[] args)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

            builder.Configuration
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

            builder.Services
                .AddOptions<CachingOptions>()
                .Bind(builder.Configuration.GetSection("Caching"))
                .ValidateOnStart();

            builder.Services.AddControllers();

            builder.Services
                // configure URL based API versioning
                .AddApiVersioning(options =>
                {
                    options.ApiVersionReader = new UrlSegmentApiVersionReader();
                })
                // sets up string subsitution in the version URL (in this case v[x])
                .AddApiExplorer(options =>
                {
                    options.GroupNameFormat = "'v'VVV";
                    options.SubstituteApiVersionInUrl = true;
                });

            builder.Services.AddOpenApi();

            builder.Services.AddMemoryCache();

            WebApplication app = builder.Build();

            app.MapControllers();
            
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
                app.UseDeveloperExceptionPage();
            }

            app.UseHttpsRedirection();

            app.Run();
        }
    }
}