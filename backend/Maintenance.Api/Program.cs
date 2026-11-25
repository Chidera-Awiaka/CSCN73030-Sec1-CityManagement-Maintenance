using Maintenance.Api.Integration;
using Maintenance.Api.Models;
using Maintenance.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Interventions are saved to Data/interventions.json (MNT-01 persistent storage)
builder.Services.AddSingleton(_ => new InterventionService(
    Path.Combine(builder.Environment.ContentRootPath, "Data", "interventions.json")));

// REG-01 / REG-02: Property Registry integration. The mock is replaced by a REST
// client once the Property Registration service publishes its API.
// A factory is used so DI does not pick the test constructor (which takes a property list).
builder.Services.AddSingleton<IPropertyRegistry>(_ => new MockPropertyRegistry());

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


// ESV-01 / ESV-02: incident reports received from E-services.
// Accepting a report creates an intervention through InterventionService.
builder.Services.AddSingleton(sp => new IncidentReportService(
    sp.GetRequiredService<InterventionService>(),
    Path.Combine(builder.Environment.ContentRootPath, "Data", "incident-reports.json")));

// ---------------- Sprint 3 ----------------
string DataFile(string name) => Path.Combine(builder.Environment.ContentRootPath, "Data", name);

// TEC-05 audit history.
builder.Services.AddSingleton(_ => new AuditService(DataFile("audit.json")));

// TEC-03 mock external services (replaced by real REST clients that follow docs/integration-contracts.md).
builder.Services.AddSingleton(_ => ExternalServiceHub.WithMocks());
builder.Services.AddSingleton<IStaffDirectory>(_ => new MockStaffDirectory());
builder.Services.AddSingleton<ITransitService>(_ => new MockTransitService());

// TEC-04 exchange log with retries.
builder.Services.AddSingleton(sp => new ExchangeService(sp.GetRequiredService<ExternalServiceHub>(), DataFile("exchanges.json")));

// BUD-04 rule (pending confirmation): off unless Budget:RequireApprovalToStart = true.
builder.Services.AddSingleton(_ => new BudgetPolicy
{
    RequireApprovalToStart = builder.Configuration.GetValue("Budget:RequireApprovalToStart", false)
});

// ESV-03 / ESV-04 / ESV-05 weather alerts.
builder.Services.AddSingleton(sp => new WeatherAlertService(
    sp.GetRequiredService<InterventionService>(), sp.GetRequiredService<AuditService>(),
    sp.GetRequiredService<ExchangeService>(), DataFile("weather-alerts.json")));

// BUD-01 .. BUD-05 budget.
builder.Services.AddSingleton(sp => new BudgetService(
    sp.GetRequiredService<InterventionService>(), sp.GetRequiredService<ExchangeService>(), sp.GetRequiredService<AuditService>()));

// SEC-01 / SEC-02 security requests.
builder.Services.AddSingleton(sp => new SecurityService(
    sp.GetRequiredService<InterventionService>(), sp.GetRequiredService<ExchangeService>(),
    sp.GetRequiredService<AuditService>(), DataFile("security-requests.json")));

// ESV-06 / SEC-03 / TRP-02 / REG-04 notices.
builder.Services.AddSingleton(sp => new NoticeService(
    sp.GetRequiredService<InterventionService>(), sp.GetRequiredService<ExchangeService>(), DataFile("notices.json")));

// TRP-03 route changes.
builder.Services.AddSingleton(sp => new TransitDiversionService(
    sp.GetRequiredService<InterventionService>(), sp.GetRequiredService<ExchangeService>(), DataFile("route-diversions.json")));

var app = builder.Build();

// Create the services that subscribe to exchange retries at start-up (TEC-04).
app.Services.GetRequiredService<BudgetService>();
app.Services.GetRequiredService<SecurityService>();
app.UseCors("AllowFrontend");

// Configure HTTP pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();