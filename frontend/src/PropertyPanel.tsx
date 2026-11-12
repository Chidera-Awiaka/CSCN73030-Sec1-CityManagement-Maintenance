import { useEffect, useState } from "react";
import { API_BASE, errorMessage, type Property } from "./api";

/**
 * REG-01: search the Property Registry by Property ID or address.
 * REG-02: link / unlink properties on one intervention.
 */
function PropertyPanel({ interventionId, onChanged }: { interventionId: number; onChanged: () => void }) {
  const [propertyId, setPropertyId] = useState("");
  const [address, setAddress] = useState("");
  const [results, setResults] = useState<Property[]>([]);
  const [linked, setLinked] = useState<Property[]>([]);
  const [message, setMessage] = useState("");

  const loadLinked = async () => {
    const response = await fetch(`${API_BASE}/interventions/${interventionId}/properties`);
    if (response.ok) setLinked(await response.json());
  };

  useEffect(() => {
    loadLinked();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [interventionId]);

  const search = async () => {
    setMessage("");
    setResults([]);
    const params = new URLSearchParams();
    if (propertyId) params.append("propertyId", propertyId);
    if (address) params.append("address", address);

    const response = await fetch(`${API_BASE}/properties?${params}`);
    if (!response.ok) {
      setMessage(await errorMessage(response));
      return;
    }
    setResults(await response.json());
  };

  const link = async (id: string) => {
    const response = await fetch(`${API_BASE}/interventions/${interventionId}/properties`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ propertyId: id }),
    });
    setMessage(response.ok ? `Linked ${id}.` : await errorMessage(response));
    await loadLinked();
    onChanged();
  };

  const unlink = async (id: string) => {
    const response = await fetch(`${API_BASE}/interventions/${interventionId}/properties/${encodeURIComponent(id)}`, {
      method: "DELETE",
    });
    setMessage(response.ok ? `Unlinked ${id}.` : await errorMessage(response));
    await loadLinked();
    onChanged();
  };

  return (
    <div style={{ borderTop: "1px dashed #aaa", marginTop: "12px", paddingTop: "12px" }}>
      <strong>Linked properties:</strong>{" "}
      {linked.length === 0 && <em>none</em>}
      <ul>
        {linked.map((p) => (
          <li key={p.propertyId}>
            {p.propertyId} — {p.address}{" "}
            <button onClick={() => unlink(p.propertyId)}>Remove</button>
          </li>
        ))}
      </ul>

      <div style={{ display: "flex", gap: "8px", flexWrap: "wrap" }}>
        <input placeholder="Property ID (e.g. PR-1001)" value={propertyId} onChange={(e) => setPropertyId(e.target.value)} />
        <input placeholder="Address (e.g. King Street)" value={address} onChange={(e) => setAddress(e.target.value)} />
        <button onClick={search}>Search registry</button>
      </div>

      <ul>
        {results.map((p) => (
          <li key={p.propertyId}>
            {p.propertyId} — {p.address} ({p.propertyType}, {p.ward}){" "}
            <button onClick={() => link(p.propertyId)}>Link</button>
          </li>
        ))}
      </ul>
      {message && <p style={{ color: message.startsWith("Linked") || message.startsWith("Unlinked") ? "green" : "red" }}>{message}</p>}
    </div>
  );
}

export default PropertyPanel;
