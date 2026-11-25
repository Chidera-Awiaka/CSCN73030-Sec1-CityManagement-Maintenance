import { useCallback, useEffect, useState } from "react";
import { call, formatDate, panelStyle, PRIORITIES, toIso } from "./api";

/** Weather alert from E-services with its assessments and actions (ESV-03, ESV-04, ESV-05). */
type WeatherAlert = {
  id: number;
  externalAlertId: string;
  title: string;
  content: string;
  affectedArea?: string | null;
  severity?: string | null;
  validFrom?: string | null;
  validTo?: string | null;
  receivedAt: string;
  reviewStatus: string;
  assessments: { reviewer: string; reviewedAt: string; decision: string; rationale: string }[];
  actions: { type: string; interventionIds: number[]; notes?: string | null; actor: string; takenAt: string }[];
  linkedInterventionIds: number[];
};

const DECISIONS = ["Action Required", "Monitor", "No Impact"];
const ACTIONS = [
  { value: "CreateIntervention", label: "Create intervention" },
  { value: "LinkInterventions", label: "Link existing interventions" },
  { value: "NoActionRequired", label: "No action required" },
  { value: "Reschedule", label: "Reschedule linked intervention" },
  { value: "PutOnHold", label: "Put linked intervention on hold" },
];

/**
 * ESV-03: list of weather alerts received from E-services.
 * ESV-04: record an assessment (reviewer, decision, rationale) and set the review state.
 * ESV-05: act on an alert; every decision stays attached to the alert.
 */
function WeatherAlertsPanel({ onInterventionsChanged }: { onInterventionsChanged: () => void }) {
  const [alerts, setAlerts] = useState<WeatherAlert[]>([]);
  const [filter, setFilter] = useState("");
  const [message, setMessage] = useState("");
  const [error, setError] = useState("");

  const load = useCallback(async () => {
    const query = filter ? `?reviewStatus=${encodeURIComponent(filter)}` : "";
    const result = await call<WeatherAlert[]>("GET", `/weather-alerts${query}`);
    if (result.ok) {
      setAlerts(result.data ?? []);
    } else {
      setError(result.error ?? "");
    }
  }, [filter]);

  useEffect(() => {
    load();
  }, [load]);

  const report = async (ok: boolean, okText: string, errorText?: string, interventionsChanged = false) => {
    setMessage(ok ? okText : "");
    setError(ok ? "" : errorText ?? "Request failed.");
    if (ok) {
      await load();
      if (interventionsChanged) onInterventionsChanged();
    }
  };

  return (
    <div style={panelStyle}>
      <h2>Weather Alerts (E-services)</h2>
      <p>
        <label>
          Show{" "}
          <select value={filter} onChange={(e) => setFilter(e.target.value)}>
            <option value="">All alerts</option>
            <option value="Awaiting Review">Awaiting Review</option>
            <option value="Reviewed">Reviewed</option>
          </select>
        </label>{" "}
        <button onClick={load}>Refresh</button>
      </p>
      {message && <p style={{ color: "green" }}>{message}</p>}
      {error && <p style={{ color: "red" }}>{error}</p>}
      {alerts.length === 0 && <p>No weather alerts.</p>}

      {alerts.map((alert) => (
        <AlertCard key={alert.id} alert={alert} report={report} />
      ))}
    </div>
  );
}

type Reporter = (ok: boolean, okText: string, errorText?: string, interventionsChanged?: boolean) => Promise<void>;

function AlertCard({ alert, report }: { alert: WeatherAlert; report: Reporter }) {
  const [decision, setDecision] = useState("Action Required");
  const [rationale, setRationale] = useState("");
  const [actionType, setActionType] = useState("CreateIntervention");
  const [ids, setIds] = useState("");
  const [priority, setPriority] = useState("High");
  const [notes, setNotes] = useState("");
  const [start, setStart] = useState("");
  const [end, setEnd] = useState("");

  const assess = async () => {
    const r = await call("POST", `/weather-alerts/${alert.id}/assessments`, { decision, rationale });
    await report(r.ok, `Assessment recorded for alert #${alert.id}.`, r.error);
    if (r.ok) setRationale("");
  };

  const reopen = async () => {
    const r = await call("PUT", `/weather-alerts/${alert.id}/review-status`, { reviewStatus: "Awaiting Review" });
    await report(r.ok, `Alert #${alert.id} is awaiting review again.`, r.error);
  };

  const act = async () => {
    const interventionIds = ids.split(",").map((x) => Number(x.trim())).filter((x) => x > 0);
    const body = {
      type: actionType,
      notes: notes || null,
      priority,
      interventionIds,
      interventionId: interventionIds[0] ?? null,
      plannedStartDate: toIso(start),
      plannedEndDate: toIso(end),
    };
    const r = await call("POST", `/weather-alerts/${alert.id}/actions`, body);
    await report(r.ok, `Action recorded on alert #${alert.id}.`, r.error, actionType !== "NoActionRequired");
    if (r.ok) {
      setNotes("");
      setIds("");
    }
  };

  const needsIds = actionType === "LinkInterventions";
  const needsOne = actionType === "Reschedule" || actionType === "PutOnHold";

  return (
    <div style={{ borderTop: "1px solid #888", padding: "12px 0", textAlign: "left" }}>
      <h3>
        #{alert.id} {alert.title}{" "}
        <small>
          ({alert.severity ?? "severity not provided"}) — {alert.reviewStatus}
        </small>
      </h3>
      <p>{alert.content}</p>
      <p>
        <strong>Area:</strong> {alert.affectedArea ?? "not provided"} · <strong>Valid:</strong>{" "}
        {formatDate(alert.validFrom, "not provided")} → {formatDate(alert.validTo, "not provided")} ·{" "}
        <strong>E-services ID:</strong> {alert.externalAlertId} · <strong>Received:</strong> {formatDate(alert.receivedAt)}
      </p>
      <p>
        <strong>Linked interventions:</strong>{" "}
        {alert.linkedInterventionIds.length ? alert.linkedInterventionIds.map((i) => `#${i}`).join(", ") : "none"}
      </p>

      <details>
        <summary>History ({alert.assessments.length} assessment(s), {alert.actions.length} action(s))</summary>
        <ul>
          {alert.assessments.map((a, i) => (
            <li key={`a${i}`}>
              {formatDate(a.reviewedAt)} — <strong>{a.decision}</strong> by {a.reviewer}: {a.rationale}
            </li>
          ))}
          {alert.actions.map((a, i) => (
            <li key={`x${i}`}>
              {formatDate(a.takenAt)} — <strong>{a.type}</strong>
              {a.interventionIds.length ? ` [${a.interventionIds.map((x) => `#${x}`).join(", ")}]` : ""} by {a.actor}
              {a.notes ? `: ${a.notes}` : ""}
            </li>
          ))}
        </ul>
      </details>

      <p>
        <strong>Assess:</strong>{" "}
        <select value={decision} onChange={(e) => setDecision(e.target.value)}>
          {DECISIONS.map((d) => (
            <option key={d}>{d}</option>
          ))}
        </select>{" "}
        <input placeholder="Rationale (required)" value={rationale} onChange={(e) => setRationale(e.target.value)} />{" "}
        <button onClick={assess}>Record assessment</button>{" "}
        {alert.reviewStatus === "Reviewed" && <button onClick={reopen}>Mark awaiting review</button>}
      </p>

      <p>
        <strong>Act:</strong>{" "}
        <select value={actionType} onChange={(e) => setActionType(e.target.value)}>
          {ACTIONS.map((a) => (
            <option key={a.value} value={a.value}>
              {a.label}
            </option>
          ))}
        </select>{" "}
        {actionType === "CreateIntervention" && (
          <select value={priority} onChange={(e) => setPriority(e.target.value)}>
            {PRIORITIES.map((p) => (
              <option key={p}>{p}</option>
            ))}
          </select>
        )}{" "}
        {(needsIds || needsOne) && (
          <input
            placeholder={needsIds ? "Intervention IDs, e.g. 1, 2" : "Linked intervention ID"}
            value={ids}
            onChange={(e) => setIds(e.target.value)}
          />
        )}{" "}
        {actionType === "Reschedule" && (
          <>
            <input type="datetime-local" value={start} onChange={(e) => setStart(e.target.value)} />{" "}
            <input type="datetime-local" value={end} onChange={(e) => setEnd(e.target.value)} />
          </>
        )}{" "}
        <input
          placeholder={actionType === "NoActionRequired" ? "Reason (required)" : "Notes (optional)"}
          value={notes}
          onChange={(e) => setNotes(e.target.value)}
        />{" "}
        <button onClick={act}>Record action</button>
      </p>
    </div>
  );
}

export default WeatherAlertsPanel;
