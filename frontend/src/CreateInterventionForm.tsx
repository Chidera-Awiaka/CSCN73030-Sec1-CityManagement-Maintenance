import { useState, type FormEvent } from "react";
import { API_BASE, PRIORITIES } from "./api";

/** MNT-01: form for creating a new intervention. */
function CreateInterventionForm({ onCreated }: { onCreated: () => void }) {
  const [description, setDescription] = useState("");
  const [location, setLocation] = useState("");
  const [issueType, setIssueType] = useState("");
  const [priority, setPriority] = useState("Medium");
  const [errors, setErrors] = useState<string[]>([]);
  const [message, setMessage] = useState("");

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    setErrors([]);
    setMessage("");

    const response = await fetch(`${API_BASE}/interventions`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ description, location, issueType, priority }),
    });

    if (!response.ok) {
      const data = await response.json();
      setErrors(data.errors ?? ["Could not create the intervention."]);
      return;
    }

    const created = await response.json();
    setMessage(`Intervention #${created.id} created with status "${created.status}".`);
    setDescription("");
    setLocation("");
    setIssueType("");
    setPriority("Medium");
    onCreated();
  };

  return (
    <form
      onSubmit={submit}
      style={{ border: "1px solid #ccc", padding: "20px", marginBottom: "20px" }}
    >
      <h2>Create Intervention</h2>

      <p>
        <label>
          Description*{" "}
          <input value={description} onChange={(e) => setDescription(e.target.value)} />
        </label>
      </p>
      <p>
        <label>
          Location*{" "}
          <input value={location} onChange={(e) => setLocation(e.target.value)} />
        </label>
      </p>
      <p>
        <label>
          Issue type*{" "}
          <input
            value={issueType}
            placeholder="Road, Water, Electrical..."
            onChange={(e) => setIssueType(e.target.value)}
          />
        </label>
      </p>
      <p>
        <label>
          Priority{" "}
          <select value={priority} onChange={(e) => setPriority(e.target.value)}>
            {PRIORITIES.map((p) => (
              <option key={p} value={p}>{p}</option>
            ))}
          </select>
        </label>
      </p>

      <button type="submit">Create</button>

      {errors.length > 0 && (
        <ul style={{ color: "red" }}>
          {errors.map((error) => (
            <li key={error}>{error}</li>
          ))}
        </ul>
      )}
      {message && <p style={{ color: "green" }}>{message}</p>}
    </form>
  );
}

export default CreateInterventionForm;
