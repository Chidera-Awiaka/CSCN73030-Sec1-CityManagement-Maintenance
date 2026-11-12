import { useEffect, useState } from "react";
import { API_BASE, type Intervention } from "./api";

/** Technicians available for assignment (same sample list as MNT-05). */
const TECHNICIANS = [
  { id: 101, name: "Alex Technician" },
  { id: 102, name: "Sam Technician" },
  { id: 103, name: "Jordan Technician" },
];

const formatDate = (value?: string | null) =>
  value ? new Date(value).toLocaleString() : "Not scheduled";

/**
 * MNT-06: a technician's personal list of assigned interventions.
 * Shows location, planned dates, instructions and priority.
 */
function MyAssignments({ refreshKey }: { refreshKey: number }) {
  const [technicianId, setTechnicianId] = useState(101);
  const [items, setItems] = useState<Intervention[]>([]);
  const [error, setError] = useState("");

  useEffect(() => {
    const load = async () => {
      const response = await fetch(`${API_BASE}/interventions/assigned/${technicianId}`);
      if (!response.ok) {
        setError(`Could not load assignments (HTTP ${response.status}).`);
        setItems([]);
        return;
      }
      setError("");
      setItems(await response.json());
    };
    load();
  }, [technicianId, refreshKey]);

  return (
    <div style={{ border: "1px solid #ccc", padding: "20px", marginBottom: "20px" }}>
      <h2>My Assigned Interventions</h2>
      <label>
        Technician{" "}
        <select value={technicianId} onChange={(e) => setTechnicianId(Number(e.target.value))}>
          {TECHNICIANS.map((t) => (
            <option key={t.id} value={t.id}>{t.name} ({t.id})</option>
          ))}
        </select>
      </label>

      {error && <p style={{ color: "red" }}>{error}</p>}
      {!error && items.length === 0 && <p>No open interventions are assigned to you.</p>}

      <ul>
        {items.map((i) => (
          <li key={i.id} style={{ marginBottom: "10px", textAlign: "left" }}>
            <strong>#{i.id} {i.description}</strong> — {i.priority} priority, {i.status}
            <br />
            Location: {i.location}
            <br />
            Planned: {formatDate(i.plannedStartDate)} → {formatDate(i.plannedEndDate)}
            <br />
            Instructions: {i.instructions || "None"}
          </li>
        ))}
      </ul>
    </div>
  );
}

export default MyAssignments;
