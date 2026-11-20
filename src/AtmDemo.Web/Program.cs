using AtmDemo.Web.Api.V1;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();

app.MapGroup("/api/v1").MapAccountEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.Run();
