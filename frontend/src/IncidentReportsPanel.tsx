import { useCallback, useEffect, useState } from "react";
import { API_BASE, PRIORITIES } from "./api";

/** Incident report received from E-services (ESV-01). */
export type IncidentReport = {
  id: number;
  externalReportId: string;
  description: string;
  location: string;
  issueType: string;
  reportedAt: string;
  receivedAt: string;
  status: "Pending" | "Accepted" | "Rejected";
  reviewedBy?: string;
  reviewedAt?: string;
  rejectionReason?: string;
  interventionId?: number;
};

const REVIEW_STATUSES = ["Pending", "Accepted", "Rejected"];

/**
 * ESV-01 / ESV-02: lists incident reports received from E-services and
 * lets a maintenance agent accept (creates an intervention) or reject them.
 */
function IncidentReportsPanel({ onInterventionCreated }: { onInterventionCreated: () => void }) {
  const [reports, setReports] = useState<IncidentReport[]>([]);
  const [statusFilter, setStatusFilter] = useState("Pending");
  const [reviewer, setReviewer] = useState("");
  const [priorities, setPriorities] = useState<Record<number, string>>({});
  const [reasons, setReasons] = useState<Record<number, string>>({});
  const [message, setMessage] = useState("");
  const [errors, setErrors] = useState<string[]>([]);

  const loadReports = useCallback(async () => {
    const query = statusFilter ? `?status=${encodeURIComponent(statusFilter)}` : "";
    const response = await fetch(`${API_BASE}/incident-reports${query}`);
    setReports(await response.json());
  }, [statusFilter]);

  useEffect(() => {
    loadReports();
  }, [loadReports]);

  const review = async (id: number, action: "accept" | "reject") => {
    setErrors([]);
    setMessage("");

    const body =
      action === "accept"
        ? { reviewedBy: reviewer, priority: priorities[id] ?? "Medium" }
        : { reviewedBy: reviewer, reason: reasons[id] ?? "" };

    const response = await fetch(`${API_BASE}/incident-reports/${id}/${action}`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });

    const data = await response.json();

    if (!response.ok) {
      setErrors(data.errors ?? [`Could not ${action} the report.`]);
      return;
    }

    if (action === "accept") {
      setMessage(`Report ${data.externalReportId} accepted. Intervention #${data.interventionId} created.`);
      onInterventionCreated();
    } else {
      setMessage(`Report ${data.externalReportId} rejected.`);
    }

    loadReports();
  };

  return (
    <div style={{ padding: "30px", paddingBottom: 0 }}>
      <h1>Incident Reports from E-services</h1>

      <div style={{ display: "flex", gap: "10px", flexWrap: "wrap", marginBottom: "15px" }}>
        <input
          placeholder="Your name (reviewer)*"
          value={reviewer}
          onChange={(e) => setReviewer(e.target.value)}
        />

        <select value={statusFilter} onChange={(e) => setStatusFilter(e.target.value)}>
          <option value="">All reports</option>
          {REVIEW_STATUSES.map((s) => (
            <option key={s} value={s}>{s}</option>
          ))}
        </select>

        <button onClick={loadReports}>Refresh</button>
      </div>

      {errors.length > 0 && (
        <ul style={{ color: "red" }}>
          {errors.map((error) => (
            <li key={error}>{error}</li>
          ))}
        </ul>
      )}
      {message && <p style={{ color: "green" }}>{message}</p>}

      <p>{reports.length} report(s)</p>

      {reports.map((report) => (
        <div
          key={report.id}
          style={{ border: "1px solid #ccc", padding: "15px", marginBottom: "15px" }}
        >
          <h3>
            {report.externalReportId}: {report.description}
          </h3>
          <p>
            <strong>Location:</strong> {report.location} &nbsp;
            <strong>Type:</strong> {report.issueType} &nbsp;
            <strong>Reported:</strong> {new Date(report.reportedAt).toLocaleString()}
          </p>
          <p>
            <strong>Status:</strong> {report.status}
            {report.reviewedBy && ` by ${report.reviewedBy}`}
            {report.interventionId && ` → Intervention #${report.interventionId}`}
            {report.rejectionReason && ` (Reason: ${report.rejectionReason})`}
          </p>

          {report.status === "Pending" && (
            <div style={{ display: "flex", gap: "10px", flexWrap: "wrap" }}>
              <select
                value={priorities[report.id] ?? "Medium"}
                onChange={(e) => setPriorities({ ...priorities, [report.id]: e.target.value })}
              >
                {PRIORITIES.map((p) => (
                  <option key={p} value={p}>{p}</option>
                ))}
              </select>
              <button onClick={() => review(report.id, "accept")}>Accept</button>

              <input
                placeholder="Rejection reason"
                value={reasons[report.id] ?? ""}
                onChange={(e) => setReasons({ ...reasons, [report.id]: e.target.value })}
              />
              <button onClick={() => review(report.id, "reject")}>Reject</button>
            </div>
          )}
        </div>
      ))}
    </div>
  );
}

export default IncidentReportsPanel;
