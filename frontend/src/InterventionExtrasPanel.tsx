import { useCallback, useState } from "react";
import { call, formatDate, toIso, type Intervention } from "./api";

type Delivery = { service: string; exchangeId: number; status: string; reference?: string | null; error?: string | null };
type Notice = { id: number; kind: string; summary: string; sentBy: string; createdAt: string; deliveries: Delivery[] };
type SecurityRequest = {
  id: number;
  reason: string;
  location: string;
  periodStart: string;
  periodEnd: string;
  transmissionStatus: string;
  transmissionError?: string | null;
  exchangeId: number;
  securityReference?: string | null;
  response?: { outcome: string; comments?: string | null; respondedBy?: string | null; receivedAt: string } | null;
};
type Comparison = {
  estimatedTotal?: number | null;
  actualCost?: number | null;
  difference?: number | null;
  differencePercent?: number | null;
  available: boolean;
  message?: string | null;
};
type Conflicts = { conflicts: { interventionId: number; description: string; plannedStartDate: string; plannedEndDate: string }[]; scope: string };
type Utilities = {
  propertyId: string;
  water?: Utility | null;
  electricity?: Utility | null;
  gas?: Utility | null;
  missingUtilities: string[];
};
type Utility = { provider: string; shutoffLocation?: string | null; notes?: string | null };
type Diversion = { id: number; externalReference: string; affectedRoutes: string[]; description: string; effectiveFrom: string; effectiveTo?: string | null };

const money = (v?: number | null) => (v == null ? "—" : `$${v.toFixed(2)}`);
const SERVICES = ["E-services", "Security", "Public Transport"];
const box = { borderTop: "1px dashed #aaa", marginTop: "10px", paddingTop: "10px", textAlign: "left" } as const;

/**
 * Sprint 3 tools for one intervention:
 * BUD-01..05 budget, STF-02 overlaps, REG-03 utilities, ESV-06 / SEC-03 / TRP-02 road closures and
 * work-status updates, SEC-01 / SEC-02 security requests, REG-04 property disruptions, TRP-03 linked
 * route changes, TEC-04 delivery status and retry.
 */
function InterventionExtrasPanel({ intervention, onChanged }: { intervention: Intervention; onChanged: () => void }) {
  const [open, setOpen] = useState(false);
  const [message, setMessage] = useState("");
  const [error, setError] = useState("");

  const [repair, setRepair] = useState(String(intervention.costEstimate?.repairCost ?? ""));
  const [material, setMaterial] = useState(String(intervention.costEstimate?.materialCost ?? ""));
  const [equipment, setEquipment] = useState(String(intervention.costEstimate?.equipmentCost ?? ""));
  const [comparison, setComparison] = useState<Comparison | null>(null);

  const [conflicts, setConflicts] = useState<Conflicts | null>(null);
  const [utilities, setUtilities] = useState<Utilities[]>([]);
  const [notices, setNotices] = useState<Notice[]>([]);
  const [requests, setRequests] = useState<SecurityRequest[]>([]);
  const [diversions, setDiversions] = useState<Diversion[]>([]);

  const [closureStatus, setClosureStatus] = useState("Closed");
  const [closureStart, setClosureStart] = useState("");
  const [closureEnd, setClosureEnd] = useState("");
  const [services, setServices] = useState<string[]>(SERVICES);

  const [reason, setReason] = useState("");
  const [secStart, setSecStart] = useState("");
  const [secEnd, setSecEnd] = useState("");

  const [disruptionType, setDisruptionType] = useState("Water");
  const [updateType, setUpdateType] = useState("New");
  const [disStart, setDisStart] = useState("");
  const [disEnd, setDisEnd] = useState("");

  const id = intervention.id;
  const total = (Number(repair) || 0) + (Number(material) || 0) + (Number(equipment) || 0);

  const load = useCallback(async () => {
    const [c, conf, n, r, d] = await Promise.all([
      call<Comparison>("GET", `/interventions/${id}/budget/comparison`),
      call<Conflicts>("GET", `/interventions/${id}/conflicts`),
      call<Notice[]>("GET", `/interventions/${id}/notices`),
      call<SecurityRequest[]>("GET", `/interventions/${id}/security-requests`),
      call<Diversion[]>("GET", `/transit/diversions?interventionId=${id}`),
    ]);
    setComparison(c.data ?? null);
    setConflicts(conf.data ?? null);
    setNotices(n.data ?? []);
    setRequests(r.data ?? []);
    setDiversions(d.data ?? []);
    const util = await Promise.all(
      (intervention.linkedPropertyIds ?? []).map((p) => call<Utilities>("GET", `/properties/${encodeURIComponent(p)}/utilities`)),
    );
    setUtilities(util.filter((u) => u.ok && u.data).map((u) => u.data as Utilities));
  }, [id, intervention.linkedPropertyIds]);

  const done = async (ok: boolean, okText: string, err?: string) => {
    setMessage(ok ? okText : "");
    setError(ok ? "" : err ?? "Request failed.");
    await load();
    onChanged();
  };

  const saveEstimate = async () => {
    const r = await call("PUT", `/interventions/${id}/budget/estimate`, {
      repairCost: Number(repair) || 0,
      materialCost: Number(material) || 0,
      equipmentCost: Number(equipment) || 0,
    });
    await done(r.ok, "Estimate saved.", r.error);
  };

  const submitBudget = async () => {
    const r = await call<{ alreadySubmitted: boolean }>("POST", `/interventions/${id}/budget/submit`);
    await done(r.ok, r.data?.alreadySubmitted ? "Already submitted - no duplicate request sent." : "Submitted to Budget (status Pending).", r.error);
  };

  const sendClosure = async () => {
    const r = await call<{ duplicate: boolean; allDelivered: boolean }>("POST", `/interventions/${id}/road-closures`, {
      closureStatus,
      plannedStart: toIso(closureStart),
      plannedEnd: toIso(closureEnd),
      services,
    });
    await done(r.ok, r.data?.duplicate ? "Already sent - no duplicate." : r.data?.allDelivered ? "Road closure sent." : "Sent - some deliveries failed (retry below).", r.error);
  };

  const sendWorkStatus = async () => {
    const r = await call("POST", `/interventions/${id}/work-status-updates`, {});
    await done(r.ok, "Work status sent to Security.", r.error);
  };

  const requestSecurity = async () => {
    const r = await call("POST", `/interventions/${id}/security-requests`, { reason, periodStart: toIso(secStart), periodEnd: toIso(secEnd) });
    await done(r.ok, "Assistance request created.", r.error);
    if (r.ok) setReason("");
  };

  const sendDisruption = async () => {
    const r = await call("POST", `/interventions/${id}/property-disruptions`, {
      disruptionType,
      updateType,
      periodStart: toIso(disStart),
      periodEnd: toIso(disEnd),
    });
    await done(r.ok, "Disruption sent to the Property Registry.", r.error);
  };

  const retry = async (exchangeId: number) => {
    const r = await call<{ succeeded: boolean }>("POST", `/exchanges/${exchangeId}/retry`);
    await done(r.ok && !!r.data?.succeeded, "Retry succeeded.", r.error ?? "Retry failed - the service is still unavailable.");
  };

  const toggleService = (s: string) =>
    setServices((current) => (current.includes(s) ? current.filter((x) => x !== s) : [...current, s]));

  return (
    <details
      style={box}
      onToggle={(e) => {
        const isOpen = (e.target as HTMLDetailsElement).open;
        setOpen(isOpen);
        if (isOpen) load();
      }}
    >
      <summary>
        <strong>Budget, closures & coordination</strong> — budget status: {intervention.budgetStatus ?? "Not Submitted"}
      </summary>
      {open && (
        <>
          {message && <p style={{ color: "green" }}>{message}</p>}
          {error && <p style={{ color: "red" }}>{error}</p>}

          <h4>Budget (BUD-01 – BUD-05)</h4>
          <p>
            Repair $<input type="number" min="0" value={repair} onChange={(e) => setRepair(e.target.value)} style={{ width: 90 }} /> Material $
            <input type="number" min="0" value={material} onChange={(e) => setMaterial(e.target.value)} style={{ width: 90 }} /> Equipment $
            <input type="number" min="0" value={equipment} onChange={(e) => setEquipment(e.target.value)} style={{ width: 90 }} />{" "}
            <strong>Total {money(total)}</strong> <button onClick={saveEstimate}>Save estimate</button>{" "}
            <button onClick={submitBudget} disabled={!intervention.costEstimate}>
              Submit to Budget
            </button>
          </p>
          <p>
            Status: <strong>{intervention.budgetStatus ?? "Not Submitted"}</strong>
            {intervention.budgetRequestReference && ` · Budget reference ${intervention.budgetRequestReference}`}
            {intervention.budgetDecision &&
              ` · ${intervention.budgetDecision.decision}${intervention.budgetDecision.approvedAmount != null ? ` (${money(intervention.budgetDecision.approvedAmount)})` : ""}` +
                `${intervention.budgetDecision.decidedBy ? ` by ${intervention.budgetDecision.decidedBy}` : ""}` +
                `${intervention.budgetDecision.comments ? `: ${intervention.budgetDecision.comments}` : ""}`}
          </p>
          {comparison && (
            <p>
              Estimated vs actual:{" "}
              {comparison.available
                ? `${money(comparison.estimatedTotal)} vs ${money(comparison.actualCost)} → difference ${money(comparison.difference)} (${comparison.differencePercent ?? "—"}%)`
                : comparison.message}
            </p>
          )}

          <h4>Technician overlaps (STF-02)</h4>
          {conflicts && conflicts.conflicts.length > 0 ? (
            <ul style={{ color: "#c60" }}>
              {conflicts.conflicts.map((c) => (
                <li key={c.interventionId}>
                  ⚠ Overlaps #{c.interventionId} {c.description} ({formatDate(c.plannedStartDate)} – {formatDate(c.plannedEndDate)})
                </li>
              ))}
            </ul>
          ) : (
            <p>No overlapping assignments.</p>
          )}
          {conflicts && <p><small>{conflicts.scope}</small></p>}

          <h4>Utilities of linked properties (REG-03)</h4>
          {utilities.length === 0 && <p>Link a property to see its utilities.</p>}
          {utilities.map((u) => (
            <p key={u.propertyId}>
              <strong>{u.propertyId}</strong> — Water: {u.water ? `${u.water.provider}${u.water.shutoffLocation ? `, shut-off ${u.water.shutoffLocation}` : ""}` : "not supplied"} ·
              Electricity: {u.electricity ? `${u.electricity.provider}${u.electricity.shutoffLocation ? `, shut-off ${u.electricity.shutoffLocation}` : ""}` : "not supplied"} ·
              Gas: {u.gas ? `${u.gas.provider}${u.gas.shutoffLocation ? `, shut-off ${u.gas.shutoffLocation}` : ""}` : "not supplied"}
              {u.missingUtilities.length > 0 && <em> (missing: {u.missingUtilities.join(", ")})</em>}
            </p>
          ))}

          <h4>Road closure (ESV-06, SEC-03, TRP-02)</h4>
          <p>
            <select value={closureStatus} onChange={(e) => setClosureStatus(e.target.value)}>
              <option>Closed</option>
              <option>Updated</option>
              <option>Reopened</option>
            </select>{" "}
            <input type="datetime-local" value={closureStart} onChange={(e) => setClosureStart(e.target.value)} title="Planned start (defaults to schedule)" />{" "}
            <input type="datetime-local" value={closureEnd} onChange={(e) => setClosureEnd(e.target.value)} title="Planned end (defaults to schedule)" />{" "}
            {SERVICES.map((s) => (
              <label key={s}>
                <input type="checkbox" checked={services.includes(s)} onChange={() => toggleService(s)} /> {s}{" "}
              </label>
            ))}
            <button onClick={sendClosure}>Send</button> <button onClick={sendWorkStatus}>Send work status to Security</button>
          </p>

          <h4>Security assistance (SEC-01, SEC-02)</h4>
          <p>
            <input placeholder="Reason" value={reason} onChange={(e) => setReason(e.target.value)} />{" "}
            <input type="datetime-local" value={secStart} onChange={(e) => setSecStart(e.target.value)} />{" "}
            <input type="datetime-local" value={secEnd} onChange={(e) => setSecEnd(e.target.value)} />{" "}
            <button onClick={requestSecurity}>Request assistance</button>
          </p>
          <ul>
            {requests.map((r) => (
              <li key={r.id}>
                #{r.id} {r.reason} ({formatDate(r.periodStart)} – {formatDate(r.periodEnd)}) — transmission{" "}
                <strong>{r.transmissionStatus}</strong>
                {r.securityReference && ` (${r.securityReference})`}
                {r.transmissionStatus === "Failed" && (
                  <>
                    {" "}
                    {r.transmissionError} <button onClick={() => retry(r.exchangeId)}>Retry</button>
                  </>
                )}{" "}
                — response:{" "}
                {r.response ? `${r.response.outcome}${r.response.respondedBy ? ` by ${r.response.respondedBy}` : ""}${r.response.comments ? `: ${r.response.comments}` : ""}` : "waiting for Security"}
              </li>
            ))}
          </ul>

          <h4>Property service disruption (REG-04)</h4>
          <p>
            <select value={disruptionType} onChange={(e) => setDisruptionType(e.target.value)}>
              {["Water", "Electricity", "Gas", "Access", "Other"].map((t) => (
                <option key={t}>{t}</option>
              ))}
            </select>{" "}
            <select value={updateType} onChange={(e) => setUpdateType(e.target.value)}>
              {["New", "Update", "Restored"].map((t) => (
                <option key={t}>{t}</option>
              ))}
            </select>{" "}
            <input type="datetime-local" value={disStart} onChange={(e) => setDisStart(e.target.value)} />{" "}
            <input type="datetime-local" value={disEnd} onChange={(e) => setDisEnd(e.target.value)} />{" "}
            <button onClick={sendDisruption}>Send to Property Registry</button>
          </p>

          <h4>Messages sent (TEC-04)</h4>
          {notices.length === 0 && <p>None yet.</p>}
          <ul>
            {notices.map((n) => (
              <li key={n.id}>
                {formatDate(n.createdAt)} — {n.kind}: {n.summary} —{" "}
                {n.deliveries.map((d) => (
                  <span key={d.service}>
                    {d.service}: <strong style={{ color: d.status === "Succeeded" ? "green" : "red" }}>{d.status}</strong>
                    {d.status === "Failed" && <button onClick={() => retry(d.exchangeId)}>Retry</button>}{" "}
                  </span>
                ))}
              </li>
            ))}
          </ul>

          <h4>Route changes linked (TRP-03)</h4>
          {diversions.length === 0 ? (
            <p>None linked.</p>
          ) : (
            <ul>
              {diversions.map((d) => (
                <li key={d.id}>
                  {d.externalReference}: routes {d.affectedRoutes.join(", ")} — {d.description} ({formatDate(d.effectiveFrom)} – {formatDate(d.effectiveTo, "open-ended")})
                </li>
              ))}
            </ul>
          )}
        </>
      )}
    </details>
  );
}

export default InterventionExtrasPanel;
