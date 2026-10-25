import { useCallback, useEffect, useState } from "react";
import CreateInterventionForm from "./CreateInterventionForm";
import { API_BASE, PRIORITIES, STATUSES, type Intervention } from "./api";

function MaintenancePage() {
  const [interventions, setInterventions] = useState<Intervention[]>([]);

  // MNT-02 filters
  const [priorityFilter, setPriorityFilter] = useState("");
  const [locationFilter, setLocationFilter] = useState("");
  const [statusFilter, setStatusFilter] = useState("");

  const loadInterventions = useCallback(async () => {
    const params = new URLSearchParams();
    if (priorityFilter) params.append("priority", priorityFilter);
    if (locationFilter) params.append("location", locationFilter);
    if (statusFilter) params.append("status", statusFilter);

    const response = await fetch(`${API_BASE}/interventions?${params}`);
    const data = await response.json();

    setInterventions(data);
  }, [priorityFilter, locationFilter, statusFilter]);

  useEffect(() => {
    loadInterventions();
  }, [loadInterventions]);

  const updatePriority = async (id: number, priority: string) => {
    await fetch(`${API_BASE}/interventions/${id}/priority`, {
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
    await fetch(`${API_BASE}/interventions/${id}/status`, {
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

  const clearFilters = () => {
    setPriorityFilter("");
    setLocationFilter("");
    setStatusFilter("");
  };

  return (
    <div style={{ padding: "30px" }}>
      <h1>Maintenance Interventions</h1>

      {/* MNT-01: create */}
      <CreateInterventionForm onCreated={loadInterventions} />

      {/* MNT-02: filter */}
      <div style={{ marginBottom: "20px", display: "flex", gap: "10px", flexWrap: "wrap" }}>
        <select value={priorityFilter} onChange={(e) => setPriorityFilter(e.target.value)}>
          <option value="">All priorities</option>
          {PRIORITIES.map((p) => (
            <option key={p} value={p}>{p}</option>
          ))}
        </select>

        <input
          placeholder="Filter by location"
          value={locationFilter}
          onChange={(e) => setLocationFilter(e.target.value)}
        />

        <select value={statusFilter} onChange={(e) => setStatusFilter(e.target.value)}>
          <option value="">All statuses</option>
          {STATUSES.map((s) => (
            <option key={s} value={s}>{s}</option>
          ))}
        </select>

        <button onClick={clearFilters}>Clear filters</button>
      </div>

      <p>{interventions.length} intervention(s) found</p>

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
            {PRIORITIES.map((p) => (
              <option key={p} value={p}>{p}</option>
            ))}
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
            {STATUSES.map((s) => (
              <option key={s} value={s}>{s}</option>
            ))}
          </select>
        </div>
      ))}
    </div>
  );
}

export default MaintenancePage;
