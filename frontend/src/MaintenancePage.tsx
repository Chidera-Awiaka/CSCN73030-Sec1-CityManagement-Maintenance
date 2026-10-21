import { useEffect, useState } from "react";

type Intervention = {
  id: number;
  description: string;
  location: string;
  issueType: string;
  priority: string;
  status: string;
};

function MaintenancePage() {
  const [interventions, setInterventions] = useState<Intervention[]>([]);

  const loadInterventions = async () => {
    const response = await fetch("http://localhost:5000/api/interventions");
    const data = await response.json();

    setInterventions(data);
  };

  useEffect(() => {
    loadInterventions();
  }, []);

  const updatePriority = async (id: number, priority: string) => {
    await fetch(`http://localhost:5000/api/interventions/${id}/priority`, {
      method: "PUT",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify({
        priority: priority,
      }),
    });

    loadInterventions();
  };

  const updateStatus = async (id: number, status: string) => {
    await fetch(`http://localhost:5000/api/interventions/${id}/status`, {
      method: "PUT",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify({
        status: status,
      }),
    });

    loadInterventions();
  };

  return (
    <div style={{ padding: "30px" }}>
      <h1>Maintenance Interventions</h1>

      {interventions.map((intervention) => (
        <div
          key={intervention.id}
          style={{
            border: "1px solid #ccc",
            padding: "20px",
            marginBottom: "20px",
          }}
        >
          <h2>{intervention.description}</h2>

          <p>
            <strong>ID:</strong> {intervention.id}
          </p>

          <p>
            <strong>Location:</strong> {intervention.location}
          </p>

          <p>
            <strong>Issue Type:</strong> {intervention.issueType}
          </p>

          <p>
            <strong>Priority:</strong> {intervention.priority}
          </p>

          <select
            value={intervention.priority}
            onChange={(event) =>
              updatePriority(intervention.id, event.target.value)
            }
          >
            <option value="Low">Low</option>
            <option value="Medium">Medium</option>
            <option value="High">High</option>
            <option value="Critical">Critical</option>
          </select>

          <p>
            <strong>Status:</strong> {intervention.status}
          </p>

          <select
            value={intervention.status}
            onChange={(event) =>
              updateStatus(intervention.id, event.target.value)
            }
          >
            <option value="To Do">To Do</option>
            <option value="Scheduled">Scheduled</option>
            <option value="In Progress">In Progress</option>
            <option value="On Hold">On Hold</option>
            <option value="Completed">Completed</option>
            <option value="Cancelled">Cancelled</option>
          </select>
        </div>
      ))}
    </div>
  );
}

export default MaintenancePage;
