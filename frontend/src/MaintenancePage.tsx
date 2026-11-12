import { useCallback, useEffect, useState } from "react";
import CreateInterventionForm from "./CreateInterventionForm";
import PropertyPanel from "./PropertyPanel";
import MyAssignments from "./MyAssignments";
import CloseInterventionPanel from "./CloseInterventionPanel";
import { API_BASE, PRIORITIES, STATUSES, type Intervention } from "./api";

function MaintenancePage() {
  const [interventions, setInterventions] = useState<Intervention[]>([]);

  // MNT-06: refreshes "My Assigned Interventions" after any change
  const [refreshKey, setRefreshKey] = useState(0);

  // MNT-02 filters
  const [priorityFilter, setPriorityFilter] = useState("");
  const [locationFilter, setLocationFilter] = useState("");
  const [statusFilter, setStatusFilter] = useState("");

  // MNT-04 schedule values
  const [startDates, setStartDates] = useState<Record<number, string>>({});
  const [endDates, setEndDates] = useState<Record<number, string>>({});

  // MNT-05 technician selection
  const [technicians, setTechnicians] = useState<Record<number, string>>({});

  const loadInterventions = useCallback(async () => {
    const params = new URLSearchParams();

    if (priorityFilter) {
      params.append("priority", priorityFilter);
    }

    if (locationFilter) {
      params.append("location", locationFilter);
    }

    if (statusFilter) {
      params.append("status", statusFilter);
    }

    const response = await fetch(`${API_BASE}/interventions?${params}`);

    const data = await response.json();

    setInterventions(data);
    setRefreshKey((k) => k + 1);
  }, [priorityFilter, locationFilter, statusFilter]);

  useEffect(() => {
    loadInterventions();
  }, [loadInterventions]);

  // MNT-03
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

  // MNT-07
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

  // MNT-04
  const scheduleIntervention = async (id: number) => {
    const plannedStartDate = startDates[id];
    const plannedEndDate = endDates[id];

    if (!plannedStartDate || !plannedEndDate) {
      alert("Please select both start and end dates.");
      return;
    }

    if (new Date(plannedEndDate) < new Date(plannedStartDate)) {
      alert("End date cannot be before start date.");
      return;
    }

    const response = await fetch(`${API_BASE}/interventions/${id}/schedule`, {
      method: "PUT",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify({
        plannedStartDate,
        plannedEndDate,
      }),
    });

    if (!response.ok) {
      const message = await response.text();
      alert(message || "Unable to schedule intervention.");
      return;
    }

    alert("Intervention scheduled successfully.");

    loadInterventions();
  };

  // MNT-05
  const assignTechnician = async (id: number) => {
    const technicianValue = technicians[id];

    if (!technicianValue) {
      alert("Please select a technician.");
      return;
    }

    const [technicianId, technicianName] = technicianValue.split("|");

    const response = await fetch(`${API_BASE}/interventions/${id}/assign`, {
      method: "PUT",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify({
        technicianId: Number(technicianId),
        technicianName: technicianName,
      }),
    });

    if (!response.ok) {
      const message = await response.text();
      alert(message || "Unable to assign technician.");
      return;
    }

    alert("Technician assigned successfully.");

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

      {/* MNT-06 */}
      <MyAssignments refreshKey={refreshKey} />

      {/* MNT-02: filter */}
      <div
        style={{
          marginBottom: "20px",
          display: "flex",
          gap: "10px",
          flexWrap: "wrap",
        }}
      >
        <select
          value={priorityFilter}
          onChange={(e) => setPriorityFilter(e.target.value)}
        >
          <option value="">All priorities</option>

          {PRIORITIES.map((p) => (
            <option key={p} value={p}>
              {p}
            </option>
          ))}
        </select>

        <input
          placeholder="Filter by location"
          value={locationFilter}
          onChange={(e) => setLocationFilter(e.target.value)}
        />

        <select
          value={statusFilter}
          onChange={(e) => setStatusFilter(e.target.value)}
        >
          <option value="">All statuses</option>

          {STATUSES.map((s) => (
            <option key={s} value={s}>
              {s}
            </option>
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

          {/* MNT-03 */}
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
              <option key={p} value={p}>
                {p}
              </option>
            ))}
          </select>

          {/* MNT-07 */}
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
              <option key={s} value={s}>
                {s}
              </option>
            ))}
          </select>

          <hr
            style={{
              marginTop: "20px",
              marginBottom: "20px",
            }}
          />

          {/* MNT-04 */}
          <h3>Schedule Intervention</h3>

          <div
            style={{
              display: "flex",
              gap: "10px",
              flexWrap: "wrap",
              alignItems: "center",
            }}
          >
            <div>
              <label>Planned Start:</label>

              <br />

              <input
                type="datetime-local"
                value={startDates[intervention.id] || ""}
                onChange={(e) =>
                  setStartDates({
                    ...startDates,
                    [intervention.id]: e.target.value,
                  })
                }
              />
            </div>

            <div>
              <label>Planned End:</label>

              <br />

              <input
                type="datetime-local"
                value={endDates[intervention.id] || ""}
                onChange={(e) =>
                  setEndDates({
                    ...endDates,
                    [intervention.id]: e.target.value,
                  })
                }
              />
            </div>

            <button onClick={() => scheduleIntervention(intervention.id)}>
              Schedule
            </button>
          </div>

          <p>
            <strong>Current Planned Start:</strong>{" "}
            {intervention.plannedStartDate
              ? new Date(intervention.plannedStartDate).toLocaleString()
              : "Not scheduled"}
          </p>

          <p>
            <strong>Current Planned End:</strong>{" "}
            {intervention.plannedEndDate
              ? new Date(intervention.plannedEndDate).toLocaleString()
              : "Not scheduled"}
          </p>

          <hr
            style={{
              marginTop: "20px",
              marginBottom: "20px",
            }}
          />

          {/* MNT-05 */}
          <h3>Assign Technician</h3>

          <div
            style={{
              display: "flex",
              gap: "10px",
              flexWrap: "wrap",
              alignItems: "center",
            }}
          >
            <select
              value={technicians[intervention.id] || ""}
              onChange={(e) =>
                setTechnicians({
                  ...technicians,
                  [intervention.id]: e.target.value,
                })
              }
            >
              <option value="">Select technician</option>

              <option value="101|Alex Technician">Alex Technician</option>

              <option value="102|Sam Technician">Sam Technician</option>

              <option value="103|Jordan Technician">Jordan Technician</option>
            </select>

            <button onClick={() => assignTechnician(intervention.id)}>
              Assign Technician
            </button>
          </div>

          <p>
            <strong>Assigned Technician:</strong>{" "}
            {intervention.assignedTechnicianName || "Not assigned"}
          </p>

          {intervention.assignedTechnicianId && (
            <p>
              <strong>Technician ID:</strong>{" "}
              {intervention.assignedTechnicianId}
            </p>
          )}

          {intervention.instructions && (
            <p>
              <strong>Instructions:</strong> {intervention.instructions}
            </p>
          )}

          {/* MNT-08: close the intervention */}
          <CloseInterventionPanel intervention={intervention} onClosed={loadInterventions} />

          {/* REG-01 / REG-02: search the Property Registry and link properties */}
          <PropertyPanel interventionId={intervention.id} onChanged={loadInterventions} />
        </div>
      ))}
    </div>
  );
}

export default MaintenancePage;
