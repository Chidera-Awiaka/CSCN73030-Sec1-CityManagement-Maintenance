using Maintenance.Api.Models;

namespace Maintenance.Api.Services;

public class InterventionService
{
    private readonly List<Intervention> _interventions = new()
    {
        new Intervention
        {
            Id = 1,
            Description = "Water main break",
            Location = "King Street",
            IssueType = "Water",
            Priority = "High",
            Status = "To Do"
        },

        new Intervention
        {
            Id = 2,
            Description = "Road damage",
            Location = "Victoria Street",
            IssueType = "Road",
            Priority = "Medium",
            Status = "Scheduled"
        }
    };

    public List<Intervention> GetAll()
    {
        return _interventions;
    }

    public Intervention? GetById(int id)
    {
        return _interventions.FirstOrDefault(x => x.Id == id);
    }

    public bool UpdatePriority(int id, string priority)
    {
        var intervention = GetById(id);

        if (intervention == null)
        {
            return false;
        }

        intervention.Priority = priority;
        return true;
    }

    public bool UpdateStatus(int id, string status)
    {
        var intervention = GetById(id);

        if (intervention == null)
        {
            return false;
        }

        intervention.Status = status;
        return true;
    }
}