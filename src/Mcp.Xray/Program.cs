using G4.Converters;

using Mcp.Xray.Domain;
using Mcp.Xray.Domain.Extensions;
using Mcp.Xray.Domain.Formatters;
using Mcp.Xray.Settings;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;

using System.Text.Json;
using System.Text.Json.Serialization;

// Write the ASCII logo for the Hub Controller with the specified version.
ControllerUtilities.WriteAsciiLogo(version: "0000.00.00.0000");

// Create a new instance of the WebApplicationBuilder with the provided command-line arguments.
var builder = WebApplication.CreateBuilder(args);

#region *** Url & Kestrel ***
// Configure the URLs that the Kestrel web server should listen on.
// If no URLs are specified, it uses the default settings.
builder.WebHost.UseUrls();
#endregion

#region *** Service       ***
// Add logging services to the service collection for application logging.
builder.Services.AddLogging(builder =>
{
    builder.AddConsole();
});

// Add response compression services to reduce the size of HTTP responses.
// This is enabled for HTTPS requests to improve performance.
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});

// Add routing services with configuration to use lowercase URLs for consistency and SEO benefits.
builder.Services.AddRouting(i => i.LowercaseUrls = true);

// Enable directory browsing, allowing users to see the list of files in a directory.
builder.Services.AddDirectoryBrowser();

// Add controller services with custom input formatters and JSON serialization options.
builder.Services
    .AddControllers(i =>
        // Add a custom input formatter to handle plain text inputs.
        i.InputFormatters.Add(new PlainTextInputFormatter()))
    .AddJsonOptions(i =>
    {
        // Configure JSON serializer to format JSON with indentation for readability.
        i.JsonSerializerOptions.WriteIndented = false;

        // Ignore properties with null values during serialization to reduce payload size.
        i.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;

        // Use camelCase naming for JSON properties to follow JavaScript conventions.
        i.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;

        // Enable case-insensitive property name matching during deserialization.
        i.JsonSerializerOptions.PropertyNameCaseInsensitive = true;

        // Add a custom type converter for handling specific types during serialization/deserialization.
        i.JsonSerializerOptions.Converters.Add(new TypeConverter());

        // Add a custom exception converter to handle exception serialization.
        i.JsonSerializerOptions.Converters.Add(new ExceptionConverter());

        // Add a custom DateTime converter to handle ISO 8601 date/time format.
        i.JsonSerializerOptions.Converters.Add(new DateTimeIso8601Converter());

        // Add a custom method base converter to handle method base serialization.
        i.JsonSerializerOptions.Converters.Add(new MethodBaseConverter());

        // Add a custom dictionary converter to handle serialization of dictionaries with string keys and object values.
        i.JsonSerializerOptions.Converters.Add(new DictionaryStringObjectJsonConverter());
    });

// Add and configure Swagger for API documentation and testing.
builder.Services.AddSwaggerGen(i =>
{
    // Define a Swagger document named "v4" with title and version information.
    i.SwaggerDoc(
        name: $"v{AppSettings.ApiVersion}",
        info: new OpenApiInfo { Title = "G4™ XRay MCP", Version = $"v{AppSettings.ApiVersion}" });

    // Order API actions in the Swagger UI by HTTP method for better organization.
    i.OrderActionsBy(a => a.HttpMethod);

    // Enable annotations to allow for additional metadata in Swagger documentation.
    i.EnableAnnotations();
});

// Add IHttpClientFactory to the service collection for making HTTP requests.
builder.Services.AddHttpClient();
#endregion

#region *** Dependencies  ***
IDomain.SetDependencies(builder);
#endregion

#region *** Configuration ***
// Initialize the application builder
var app = builder.Build();

// Configure the application to use the response caching middleware
app.UseResponseCaching();

// Add the cookie policy
app.UseCookiePolicy();

// Add the routing and controller mapping to the application
app.UseRouting();

// Add the Swagger documentation and UI page to the application
app.UseSwagger();
app.UseSwaggerUI(i =>
{
    i.SwaggerEndpoint($"/swagger/v{AppSettings.ApiVersion}/swagger.json", $"G{AppSettings.ApiVersion}");
    i.DisplayRequestDuration();
    i.EnableFilter();
    i.EnableTryItOutByDefault();
});

app.MapDefaultControllerRoute();
app.MapControllers();
#endregion

// Start the application and wait for it to finish.
await app.RunAsync();
