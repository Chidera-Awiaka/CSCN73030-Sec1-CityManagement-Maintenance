import { useState } from "react";
import { API_BASE, errorMessage, type Intervention } from "./api";

/** MNT-08: close an intervention with a completion report, date and actual cost. */
function CloseInterventionPanel({
  intervention,
  onClosed,
}: {
  intervention: Intervention;
  onClosed: () => void;
}) {
  const [report, setReport] = useState("");
  const [completedOn, setCompletedOn] = useState("");
  const [cost, setCost] = useState("");
  const [message, setMessage] = useState("");

  if (intervention.status === "Completed") {
    return (
      <div style={{ borderTop: "1px dashed #aaa", marginTop: "12px", paddingTop: "12px" }}>
        <strong>Closed:</strong>{" "}
        {intervention.actualCompletionDate
          ? new Date(intervention.actualCompletionDate).toLocaleString()
          : ""}
        {intervention.actualCost != null && <> — cost ${intervention.actualCost}</>}
        <br />
        <strong>Completion report:</strong> {intervention.completionReport}
      </div>
    );
  }

  const close = async () => {
    setMessage("");
    const response = await fetch(`${API_BASE}/interventions/${intervention.id}/close`, {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        completionReport: report,
        actualCompletionDate: completedOn ? new Date(completedOn).toISOString() : null,
        actualCost: cost === "" ? null : Number(cost),
      }),
    });

    if (!response.ok) {
      setMessage(await errorMessage(response));
      return;
    }

    setReport("");
    setCompletedOn("");
    setCost("");
    onClosed();
  };

  return (
    <div style={{ borderTop: "1px dashed #aaa", marginTop: "12px", paddingTop: "12px" }}>
      <strong>Close intervention</strong>
      <div style={{ display: "flex", gap: "8px", flexWrap: "wrap", justifyContent: "center" }}>
        <input
          placeholder="Completion report (required)"
          value={report}
          onChange={(e) => setReport(e.target.value)}
        />
        <input type="datetime-local" value={completedOn} onChange={(e) => setCompletedOn(e.target.value)} />
        <input
          type="number"
          min="0"
          step="0.01"
          placeholder="Actual cost ($)"
          value={cost}
          onChange={(e) => setCost(e.target.value)}
        />
        <button onClick={close}>Close intervention</button>
      </div>
      {message && <p style={{ color: "red" }}>{message}</p>}
    </div>
  );
}

export default CloseInterventionPanel;
