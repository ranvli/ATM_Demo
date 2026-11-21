using System.Text.Json;
using System.Text.Json.Serialization;
using AtmDemo.Core;
using AtmDemo.Core.Storage;
using AtmDemo.Web.Api.V1;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddSingleton<IAtmRepository, InMemoryAtmRepository>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<AtmService>();
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(
    new JsonStringEnumConverter<OperationKind>(JsonNamingPolicy.CamelCase, allowIntegerValues: false)));

var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();

app.MapGroup("/api/v1").MapAtmEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.Run();
