import { useCallback, useEffect, useState } from "react";
import { call, formatDate, panelStyle } from "./api";

type Staff = { source: string; retrievedAt: string; specializations: string[]; personnel: { personnelId: number; name: string; specializations: string[] }[] };
type Routes = {
  available: boolean;
  source: string;
  retrievedAt?: string;
  message?: string | null;
  routes?: { routeId: string; name: string; streets: string[]; schedule: string; lastUpdated?: string | null }[];
};
type Diversion = { id: number; externalReference: string; affectedRoutes: string[]; description: string; effectiveFrom: string; effectiveTo?: string | null; linkedInterventionIds: number[] };
type Exchange = { id: number; service: string; direction: string; operation: string; status: string; attempts: number; lastError?: string | null; externalReference?: string | null; interventionId?: number | null; createdAt: string };
type Audit = { id: number; timestamp: string; entityType: string; entityId: string; action: string; actor?: string | null; source: string; details: string };
type Mocks = { outbound: { service: string; available: boolean }[]; staffAvailable?: boolean; transitAvailable?: boolean; propertyRegistryAvailable?: boolean };

const TABS = ["Staff", "Transit", "Exchanges", "Audit"] as const;

/**
 * Operations: STF-01 personnel, TRP-01 routes, TRP-03 route changes, TEC-03 mock switches,
 * TEC-04 exchange log and retries, TEC-05 audit history.
 */
function OperationsPanel({ refreshKey }: { refreshKey: number }) {
  const [tab, setTab] = useState<(typeof TABS)[number]>("Staff");
  const [error, setError] = useState("");

  const [specialization, setSpecialization] = useState("");
  const [staff, setStaff] = useState<Staff | null>(null);

  const [street, setStreet] = useState("");
  const [routes, setRoutes] = useState<Routes | null>(null);
  const [diversions, setDiversions] = useState<Diversion[]>([]);
  const [linkTo, setLinkTo] = useState<Record<number, string>>({});

  const [failedOnly, setFailedOnly] = useState(false);
  const [exchanges, setExchanges] = useState<Exchange[]>([]);
  const [mocks, setMocks] = useState<Mocks | null>(null);

  const [audit, setAudit] = useState<Audit[]>([]);
  const [auditType, setAuditType] = useState("");
  const [auditId, setAuditId] = useState("");

  const load = useCallback(async () => {
    setError("");
    if (tab === "Staff") {
      const r = await call<Staff>("GET", `/staff${specialization ? `?specialization=${encodeURIComponent(specialization)}` : ""}`);
      setStaff(r.ok ? r.data ?? null : null);
      if (!r.ok) setError(`Staff data unavailable: ${r.error}`);
    } else if (tab === "Transit") {
      const r = await call<Routes>("GET", `/transit/routes${street ? `?street=${encodeURIComponent(street)}` : ""}`);
      setRoutes(r.ok ? r.data ?? null : { available: false, source: "Public Transport", message: r.error });
      const d = await call<Diversion[]>("GET", "/transit/diversions");
      setDiversions(d.data ?? []);
    } else if (tab === "Exchanges") {
      const [x, m] = await Promise.all([
        call<Exchange[]>("GET", `/exchanges${failedOnly ? "?status=Failed" : ""}`),
        call<Mocks>("GET", "/mock-services"),
      ]);
      setExchanges(x.data ?? []);
      setMocks(m.data ?? null);
    } else {
      const params = new URLSearchParams();
      if (auditType) params.append("entityType", auditType);
      if (auditId) params.append("entityId", auditId);
      const r = await call<Audit[]>("GET", `/audit?${params}`);
      setAudit(r.data ?? []);
    }
  }, [tab, specialization, street, failedOnly, auditType, auditId]);

  useEffect(() => {
    load();
  }, [load, refreshKey]);

  const retry = async (id: number) => {
    const r = await call<{ succeeded: boolean }>("POST", `/exchanges/${id}/retry`);
    setError(r.ok && r.data?.succeeded ? "" : r.error ?? "Retry failed - the service is still unavailable.");
    await load();
  };

  const setAvailable = async (service: string, available: boolean) => {
    await call("PUT", `/mock-services/${encodeURIComponent(service)}/availability`, { available });
    await load();
  };

  const link = async (diversionId: number) => {
    const interventionId = Number(linkTo[diversionId]);
    const r = await call("POST", `/transit/diversions/${diversionId}/links`, { interventionId });
    setError(r.ok ? "" : r.error ?? "");
    await load();
  };

  return (
    <div style={panelStyle}>
      <h2>Operations</h2>
      <p>
        {TABS.map((t) => (
          <button key={t} onClick={() => setTab(t)} style={{ fontWeight: tab === t ? "bold" : "normal", marginRight: 6 }}>
            {t}
          </button>
        ))}
      </p>
      {error && <p style={{ color: "red" }}>{error}</p>}

      {tab === "Staff" && (
        <div style={{ textAlign: "left" }}>
          <p>
            Specialization{" "}
            <select value={specialization} onChange={(e) => setSpecialization(e.target.value)}>
              <option value="">All</option>
              {(staff?.specializations ?? []).map((s) => (
                <option key={s}>{s}</option>
              ))}
            </select>{" "}
            {staff && <small>Source: {staff.source}, retrieved {formatDate(staff.retrievedAt)}</small>}
          </p>
          <ul>
            {staff?.personnel.map((p) => (
              <li key={p.personnelId}>
                {p.personnelId} — {p.name} ({p.specializations.join(", ")})
              </li>
            ))}
          </ul>
        </div>
      )}

      {tab === "Transit" && (
        <div style={{ textAlign: "left" }}>
          <p>
            Street <input value={street} placeholder="e.g. King Street" onChange={(e) => setStreet(e.target.value)} />
          </p>
          {routes && !routes.available && <p style={{ color: "red" }}>Route data unavailable: {routes.message}</p>}
          {routes?.available && (
            <>
              <p>
                <small>
                  Source: {routes.source}, retrieved {formatDate(routes.retrievedAt)}
                </small>
              </p>
              {routes.message && <p>{routes.message}</p>}
              <ul>
                {routes.routes?.map((r) => (
                  <li key={r.routeId}>
                    Route {r.routeId} {r.name} — {r.schedule} — {r.streets.join(", ")} —{" "}
                    <small>updated {formatDate(r.lastUpdated, "time not supplied")}</small>
                  </li>
                ))}
              </ul>
            </>
          )}
          <h4>Route changes received (TRP-03)</h4>
          {diversions.length === 0 && <p>None received.</p>}
          <ul>
            {diversions.map((d) => (
              <li key={d.id}>
                {d.externalReference}: routes {d.affectedRoutes.join(", ")} — {d.description} ({formatDate(d.effectiveFrom)} –{" "}
                {formatDate(d.effectiveTo, "open-ended")}) — linked: {d.linkedInterventionIds.map((i) => `#${i}`).join(", ") || "none"}{" "}
                <input
                  style={{ width: 60 }}
                  placeholder="ID"
                  value={linkTo[d.id] ?? ""}
                  onChange={(e) => setLinkTo({ ...linkTo, [d.id]: e.target.value })}
                />{" "}
                <button onClick={() => link(d.id)}>Link</button>
              </li>
            ))}
          </ul>
        </div>
      )}

      {tab === "Exchanges" && (
        <div style={{ textAlign: "left" }}>
          <p>
            <label>
              <input type="checkbox" checked={failedOnly} onChange={(e) => setFailedOnly(e.target.checked)} /> Failed only
            </label>
          </p>
          {mocks && (
            <p>
              <small>Mock services (testing): </small>
              {[
                ...mocks.outbound.map((m) => ({ service: m.service, available: m.available })),
                { service: "Staff", available: mocks.staffAvailable ?? true },
              ].map((m) => (
                <button key={m.service} onClick={() => setAvailable(m.service, !m.available)} style={{ marginRight: 4 }}>
                  {m.service}: {m.available ? "up" : "DOWN"}
                </button>
              ))}
            </p>
          )}
          <table>
            <thead>
              <tr>
                <th>#</th>
                <th>When</th>
                <th>Service</th>
                <th>Dir.</th>
                <th>Operation</th>
                <th>Int.</th>
                <th>Status</th>
                <th>Tries</th>
                <th>Reference / error</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {exchanges.map((x) => (
                <tr key={x.id}>
                  <td>{x.id}</td>
                  <td>{formatDate(x.createdAt)}</td>
                  <td>{x.service}</td>
                  <td>{x.direction}</td>
                  <td>{x.operation}</td>
                  <td>{x.interventionId ?? ""}</td>
                  <td style={{ color: x.status === "Failed" ? "red" : "green" }}>{x.status}</td>
                  <td>{x.attempts}</td>
                  <td>{x.externalReference ?? x.lastError ?? ""}</td>
                  <td>{x.status === "Failed" && <button onClick={() => retry(x.id)}>Retry</button>}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {tab === "Audit" && (
        <div style={{ textAlign: "left" }}>
          <p>
            <select value={auditType} onChange={(e) => setAuditType(e.target.value)}>
              <option value="">All records</option>
              <option>Intervention</option>
              <option>WeatherAlert</option>
              <option>SecurityRequest</option>
            </select>{" "}
            <input style={{ width: 60 }} placeholder="ID" value={auditId} onChange={(e) => setAuditId(e.target.value)} />
          </p>
          <ul>
            {audit.map((a) => (
              <li key={a.id}>
                {formatDate(a.timestamp)} — {a.entityType} #{a.entityId} — <strong>{a.action}</strong> — {a.details} —{" "}
                <em>{a.actor ? `by ${a.actor}` : `source: ${a.source}`}</em>
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}

export default OperationsPanel;
