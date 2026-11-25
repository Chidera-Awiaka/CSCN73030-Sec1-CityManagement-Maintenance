using Maintenance.Api.DTOs;
using Maintenance.Api.Integration;
using Maintenance.Api.Models;
using Maintenance.Api.Services;

namespace Maintenance.Api.Tests;

/// <summary>All Sprint 3 services wired together in memory with mock external services.</summary>
internal sealed class Sprint3TestWorld
{
    public InterventionService Interventions { get; } = new();
    public AuditService Audit { get; } = new();
    public ExternalServiceHub Hub { get; } = ExternalServiceHub.WithMocks();
    public ExchangeService Exchanges { get; }
    public WeatherAlertService Weather { get; }
    public BudgetService Budget { get; }
    public SecurityService Security { get; }
    public NoticeService Notices { get; }
    public TransitDiversionService Diversions { get; }

    public Sprint3TestWorld()
    {
        Exchanges = new ExchangeService(Hub);
        Weather = new WeatherAlertService(Interventions, Audit, Exchanges);
        Budget = new BudgetService(Interventions, Exchanges, Audit);
        Security = new SecurityService(Interventions, Exchanges, Audit);
        Notices = new NoticeService(Interventions, Exchanges);
        Diversions = new TransitDiversionService(Interventions, Exchanges);
    }

    public MockExternalService Mock(string service) => (MockExternalService)Hub.Get(service)!;

    public Intervention NewIntervention(string location = "King Street North") =>
        Interventions.Create(new CreateInterventionDto { Description = "Pothole", Location = location, IssueType = "Road" });
}
