using Maintenance.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Interventions are saved to Data/interventions.json (MNT-01 persistent storage)
builder.Services.AddSingleton(_ => new InterventionService(
    Path.Combine(builder.Environment.ContentRootPath, "Data", "interventions.json")));

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy
            .WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});


var app = builder.Build();
app.UseCors("AllowFrontend");

// Configure HTTP pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();