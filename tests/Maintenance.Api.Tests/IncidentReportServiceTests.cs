using Maintenance.Api.DTOs;
using Maintenance.Api.Models;
using Maintenance.Api.Services;

namespace Maintenance.Api.Tests;

/// <summary>
/// Unit tests for receiving incident reports from E-services (ESV-01)
/// and accepting or rejecting them (ESV-02).
/// Sprint 2 - Chidera Awiaka.
/// </summary>
public sealed class IncidentReportServiceTests
{
    private static IncomingIncidentReportDto ValidReport(string externalId = "ES-1001") => new()
    {
        ExternalReportId = externalId,
        Description = "Large pothole in the right lane",
        Location = "King Street North",
        IssueType = "Road",
        ReportedAt = DateTime.UtcNow.AddHours(-2)
    };

    private static (IncidentReportService reports, InterventionService interventions) NewServices()
    {
        var interventions = new InterventionService();
        return (new IncidentReportService(interventions), interventions);
    }

    // ---------- ESV-01: Receive incident reports ----------

    [Fact]
    public void Receive_ValidReport_StoredAsPending()
    {
        var (service, _) = NewServices();

        var outcome = service.Receive(ValidReport());

        Assert.Equal(ReviewResult.Success, outcome.Result);
        Assert.Equal("Pending", outcome.Report!.Status);
        Assert.Equal("ES-1001", outcome.Report.ExternalReportId);
        Assert.Single(service.GetAll());
    }

    [Fact]
    public void Receive_TwoReports_GetUniqueIds()
    {
        var (service, _) = NewServices();

        var first = service.Receive(ValidReport("ES-1")).Report!;
        var second = service.Receive(ValidReport("ES-2")).Report!;

        Assert.NotEqual(first.Id, second.Id);
    }

    [Theory]
    [InlineData("", "Pothole", "King St", "Road", "External report ID is required.")]
    [InlineData("ES-1", "", "King St", "Road", "Description is required.")]
    [InlineData("ES-1", "Pothole", " ", "Road", "Location is required.")]
    [InlineData("ES-1", "Pothole", "King St", "", "Issue type is required.")]
    public void Receive_MissingRequiredField_IsInvalid(
        string externalId, string description, string location, string issueType, string expectedError)
    {
        var (service, _) = NewServices();

        var outcome = service.Receive(new IncomingIncidentReportDto
        {
            ExternalReportId = externalId,
            Description = description,
            Location = location,
            IssueType = issueType
        });

        Assert.Equal(ReviewResult.Invalid, outcome.Result);
        Assert.Contains(expectedError, outcome.Errors);
        Assert.Empty(service.GetAll());
    }

    [Fact]
    public void Receive_FutureReportedDate_IsInvalid()
    {
        var (service, _) = NewServices();
        var report = ValidReport();
        report.ReportedAt = DateTime.UtcNow.AddDays(1);

        var outcome = service.Receive(report);

        Assert.Equal(ReviewResult.Invalid, outcome.Result);
    }

    [Fact]
    public void Receive_SameExternalIdTwice_IsConflict()
    {
        var (service, _) = NewServices();
        service.Receive(ValidReport("ES-77"));

        var outcome = service.Receive(ValidReport("es-77"));

        Assert.Equal(ReviewResult.Conflict, outcome.Result);
        Assert.Single(service.GetAll());
    }

    [Fact]
    public void GetAll_FilterByStatus_ReturnsOnlyMatching()
    {
        var (service, _) = NewServices();
        service.Receive(ValidReport("ES-1"));
        var second = service.Receive(ValidReport("ES-2")).Report!;
        service.Reject(second.Id, new RejectIncidentReportDto { ReviewedBy = "Agent A", Reason = "Duplicate" });

        Assert.Single(service.GetAll("Pending"));
        Assert.Single(service.GetAll("rejected"));
        Assert.Equal(2, service.GetAll().Count);
    }

    [Fact]
    public void Receive_IsSavedAndReloadedFromFile()
    {
        string path = Path.Combine(Path.GetTempPath(), $"incident-reports-{Guid.NewGuid()}.json");
        try
        {
            var interventions = new InterventionService();
            var created = new IncidentReportService(interventions, path).Receive(ValidReport()).Report!;

            var reloaded = new IncidentReportService(interventions, path);

            Assert.NotNull(reloaded.GetById(created.Id));
            Assert.Equal("Pending", reloaded.GetById(created.Id)!.Status);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ---------- ESV-02: Accept or reject a report ----------

    [Fact]
    public void Accept_PendingReport_CreatesLinkedIntervention()
    {
        var (service, interventions) = NewServices();
        var report = service.Receive(ValidReport()).Report!;
        int before = interventions.GetAll().Count;

        var outcome = service.Accept(report.Id,
            new AcceptIncidentReportDto { ReviewedBy = "Agent A", Priority = "High" });

        Assert.Equal(ReviewResult.Success, outcome.Result);
        Assert.Equal("Accepted", outcome.Report!.Status);
        Assert.Equal("Agent A", outcome.Report.ReviewedBy);
        Assert.NotNull(outcome.Report.ReviewedAt);
        Assert.Equal(before + 1, interventions.GetAll().Count);

        var intervention = interventions.GetById(outcome.Report.InterventionId!.Value);
        Assert.NotNull(intervention);
        Assert.Equal("Large pothole in the right lane", intervention!.Description);
        Assert.Equal("King Street North", intervention.Location);
        Assert.Equal("High", intervention.Priority);
        Assert.Equal("To Do", intervention.Status);
    }

    [Fact]
    public void Accept_NoPriority_InterventionDefaultsToMedium()
    {
        var (service, interventions) = NewServices();
        var report = service.Receive(ValidReport()).Report!;

        var accepted = service.Accept(report.Id, new AcceptIncidentReportDto { ReviewedBy = "Agent A" }).Report!;

        Assert.Equal("Medium", interventions.GetById(accepted.InterventionId!.Value)!.Priority);
    }

    [Fact]
    public void Accept_MissingReviewerOrBadPriority_IsInvalidAndCreatesNothing()
    {
        var (service, interventions) = NewServices();
        var report = service.Receive(ValidReport()).Report!;
        int before = interventions.GetAll().Count;

        var noReviewer = service.Accept(report.Id, new AcceptIncidentReportDto { ReviewedBy = "" });
        var badPriority = service.Accept(report.Id,
            new AcceptIncidentReportDto { ReviewedBy = "Agent A", Priority = "Urgent" });

        Assert.Equal(ReviewResult.Invalid, noReviewer.Result);
        Assert.Equal(ReviewResult.Invalid, badPriority.Result);
        Assert.Equal(before, interventions.GetAll().Count);
        Assert.Equal("Pending", service.GetById(report.Id)!.Status);
    }

    [Fact]
    public void Reject_PendingReport_RecordsReasonAndCreatesNoIntervention()
    {
        var (service, interventions) = NewServices();
        var report = service.Receive(ValidReport()).Report!;
        int before = interventions.GetAll().Count;

        var outcome = service.Reject(report.Id,
            new RejectIncidentReportDto { ReviewedBy = "Agent B", Reason = "Private property, not a city asset" });

        Assert.Equal(ReviewResult.Success, outcome.Result);
        Assert.Equal("Rejected", outcome.Report!.Status);
        Assert.Equal("Private property, not a city asset", outcome.Report.RejectionReason);
        Assert.Null(outcome.Report.InterventionId);
        Assert.Equal(before, interventions.GetAll().Count);
    }

    [Fact]
    public void Reject_WithoutReason_IsInvalid()
    {
        var (service, _) = NewServices();
        var report = service.Receive(ValidReport()).Report!;

        var outcome = service.Reject(report.Id, new RejectIncidentReportDto { ReviewedBy = "Agent B", Reason = " " });

        Assert.Equal(ReviewResult.Invalid, outcome.Result);
        Assert.Contains("A rejection reason is required.", outcome.Errors);
    }

    [Fact]
    public void Review_UnknownReport_IsNotFound()
    {
        var (service, _) = NewServices();

        Assert.Equal(ReviewResult.NotFound,
            service.Accept(999, new AcceptIncidentReportDto { ReviewedBy = "Agent A" }).Result);
        Assert.Equal(ReviewResult.NotFound,
            service.Reject(999, new RejectIncidentReportDto { ReviewedBy = "Agent A", Reason = "x" }).Result);
    }

    [Fact]
    public void Review_AlreadyReviewedReport_IsConflict()
    {
        var (service, interventions) = NewServices();
        var report = service.Receive(ValidReport()).Report!;
        service.Accept(report.Id, new AcceptIncidentReportDto { ReviewedBy = "Agent A" });
        int afterFirstAccept = interventions.GetAll().Count;

        var acceptAgain = service.Accept(report.Id, new AcceptIncidentReportDto { ReviewedBy = "Agent A" });
        var rejectAfter = service.Reject(report.Id,
            new RejectIncidentReportDto { ReviewedBy = "Agent B", Reason = "Duplicate" });

        Assert.Equal(ReviewResult.Conflict, acceptAgain.Result);
        Assert.Equal(ReviewResult.Conflict, rejectAfter.Result);
        Assert.Equal(afterFirstAccept, interventions.GetAll().Count);
    }

    // ---------- MNT-02 fix (GitHub #2): filter by assigned technician ----------

    [Fact]
    public void Filter_ByTechnicianId_ReturnsOnlyThatTechniciansWork()
    {
        var interventions = new InterventionService();
        var mine = interventions.Create(new CreateInterventionDto
            { Description = "Leak", Location = "Erb St", IssueType = "Water" });
        var other = interventions.Create(new CreateInterventionDto
            { Description = "Sign down", Location = "Weber St", IssueType = "Road" });
        interventions.AssignTechnician(mine.Id, 7, "Alex Tech");
        interventions.AssignTechnician(other.Id, 8, "Sam Tech");

        var result = interventions.Filter(null, null, null, technicianId: 7);

        Assert.Single(result);
        Assert.Equal(mine.Id, result[0].Id);
    }

    [Fact]
    public void Filter_TechnicianIdCombinedWithStatus_AndUnknownTechnician()
    {
        var interventions = new InterventionService();
        var job = interventions.Create(new CreateInterventionDto
            { Description = "Streetlight out", Location = "Erb St", IssueType = "Electrical" });
        interventions.AssignTechnician(job.Id, 7, "Alex Tech");

        Assert.Single(interventions.Filter(null, null, "To Do", technicianId: 7));
        Assert.Empty(interventions.Filter(null, null, "Completed", technicianId: 7));
        Assert.Empty(interventions.Filter(null, null, null, technicianId: 12345));
    }
}
