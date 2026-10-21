namespace Maintenance.Api.Models;

public class Intervention
{
    public int Id { get; set; }

    public string Description { get; set; } = "";

    public string Location { get; set; } = "";

    public string IssueType { get; set; } = "";

    public string Priority { get; set; } = "Medium";

    public string Status { get; set; } = "To Do";
}