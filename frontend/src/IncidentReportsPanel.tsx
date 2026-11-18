import { useCallback, useEffect, useRef, useState } from "react";
import { API_BASE, PRIORITIES, errorMessage } from "./api";
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
  reviewedBy?: string | null;
  reviewedAt?: string | null;
  rejectionReason?: string | null;
  interventionId?: number | null;
};

type IncidentReportsPanelProps = {
  onInterventionCreated: () => void;
};

const REVIEW_STATUSES = ["Pending", "Accepted", "Rejected"];

/**
 * ESV-01: list and filter incident reports received from E-services.
 * ESV-02: accept a report and create an intervention, or reject with a reason.
 */
function IncidentReportsPanel({
  onInterventionCreated,
}: IncidentReportsPanelProps) {
  const [reports, setReports] = useState<IncidentReport[]>([]);
  const [statusFilter, setStatusFilter] = useState("Pending");
  const [reviewer, setReviewer] = useState("");

  const [priorities, setPriorities] = useState<Record<number, string>>({});
  const [reasons, setReasons] = useState<Record<number, string>>({});

  const [message, setMessage] = useState("");
  const [errors, setErrors] = useState<string[]>([]);

  const [loading, setLoading] = useState(false);
  const [reviewing, setReviewing] = useState(false);

  const reviewLock = useRef(false);

  const busy = loading || reviewing;

  const loadReports = useCallback(
    async (signal?: AbortSignal) => {
      setLoading(true);
      setErrors([]);

      try {
        const query = statusFilter
          ? `?status=${encodeURIComponent(statusFilter)}`
          : "";

        const response = await fetch(
          `${API_BASE}/incident-reports${query}`,
          { signal },
        );

        if (!response.ok) {
          throw new Error(await errorMessage(response));
        }

        const data: unknown = await response.json();

        if (!Array.isArray(data)) {
          throw new Error("The API returned an invalid report list.");
        }

        if (!signal?.aborted) {
          setReports(data as IncidentReport[]);
        }
      } catch (error) {
        if (!signal?.aborted) {
          setErrors([
            error instanceof Error
              ? error.message
              : "Unable to load incident reports.",
          ]);
        }
      } finally {
        if (!signal?.aborted) {
          setLoading(false);
        }
      }
    },
    [statusFilter],
  );

  useEffect(() => {
    const controller = new AbortController();

    void loadReports(controller.signal);

    return () => controller.abort();
  }, [loadReports]);

  const review = async (id: number, action: "accept" | "reject") => {
    if (reviewLock.current) return;

    setErrors([]);
    setMessage("");

    const reviewedBy = reviewer.trim();
    const reason = (reasons[id] ?? "").trim();

    if (!reviewedBy) {
      setErrors(["Reviewer name is required."]);
      return;
    }

    if (action === "reject" && !reason) {
      setErrors(["A rejection reason is required."]);
      return;
    }

    reviewLock.current = true;
    setReviewing(true);

    try {
      const body =
        action === "accept"
          ? {
              reviewedBy,
              priority: priorities[id] ?? "Medium",
            }
          : {
              reviewedBy,
              reason,
            };

      const response = await fetch(
        `${API_BASE}/incident-reports/${id}/${action}`,
        {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
          },
          body: JSON.stringify(body),
        },
      );

      if (!response.ok) {
        throw new Error(await errorMessage(response));
      }

      const data: IncidentReport = await response.json();

      if (action === "accept") {
        setMessage(
          `Report ${data.externalReportId} accepted. Intervention #${data.interventionId} created.`,
        );

        onInterventionCreated();
      } else {
        setMessage(`Report ${data.externalReportId} rejected.`);
      }

      await loadReports();
    } catch (error) {
      setErrors([
        error instanceof Error
          ? `${error.message} Refresh the reports before trying again.`
          : "Unable to review the report. Refresh before trying again.",
      ]);
    } finally {
      reviewLock.current = false;
      setReviewing(false);
    }
  };

  return (
    <section
      aria-labelledby="incident-reports-heading"
      style={{ padding: "30px", paddingBottom: 0 }}
    >
      <h1 id="incident-reports-heading">
        Incident Reports from E-services
      </h1>

      <div
        style={{
          display: "flex",
          gap: "10px",
          flexWrap: "wrap",
          alignItems: "end",
          marginBottom: "15px",
        }}
      >
        <label>
          Reviewer name *
          <br />
          <input
            placeholder="Your name (reviewer)*"
            value={reviewer}
            onChange={(event) => setReviewer(event.target.value)}
            disabled={reviewing}
          />
        </label>

        <label>
          Review status
          <br />
          <select
            value={statusFilter}
            onChange={(event) => setStatusFilter(event.target.value)}
            disabled={busy}
          >
            <option value="">All reports</option>

            {REVIEW_STATUSES.map((status) => (
              <option key={status} value={status}>
                {status}
              </option>
            ))}
          </select>
        </label>

        <button
          type="button"
          onClick={() => void loadReports()}
          disabled={busy}
        >
          {loading ? "Loading..." : "Refresh"}
        </button>
      </div>

      {errors.length > 0 && (
        <div role="alert" style={{ color: "#b00020" }}>
          <ul>
            {errors.map((error, index) => (
              <li key={`${index}-${error}`}>{error}</li>
            ))}
          </ul>
        </div>
      )}

      {message && (
        <p role="status" style={{ color: "#16713b" }}>
          {message}
        </p>
      )}

      {reviewing && <p role="status">Processing decision...</p>}

      <p>
        {loading ? "Loading reports..." : `${reports.length} report(s)`}
      </p>

      {!loading && errors.length === 0 && reports.length === 0 && (
        <p>No incident reports match this filter.</p>
      )}

      <div aria-busy={busy}>
        {reports.map((report) => (
          <article
            key={report.id}
            style={{
              border: "1px solid #ccc",
              padding: "15px",
              marginBottom: "15px",
              borderRadius: "6px",
            }}
          >
            <h2>
              {report.externalReportId}: {report.description}
            </h2>

            <p>
              Location: {report.location}
              {" | "}
              Type: {report.issueType}
            </p>

            <p>
              Reported: {new Date(report.reportedAt).toLocaleString()}
              {" | "}
              Received: {new Date(report.receivedAt).toLocaleString()}
            </p>

            <p>
              Status: {report.status}
              {report.reviewedBy && ` by ${report.reviewedBy}`}
              {report.interventionId != null &&
                ` - Intervention #${report.interventionId}`}
            </p>

            {report.reviewedAt && (
              <p>
                Reviewed: {new Date(report.reviewedAt).toLocaleString()}
              </p>
            )}

            {report.rejectionReason && (
              <p>Rejection reason: {report.rejectionReason}</p>
            )}

            {report.status === "Pending" && (
              <div
                style={{
                  display: "flex",
                  gap: "10px",
                  flexWrap: "wrap",
                  alignItems: "end",
                }}
              >
                <label>
                  Intervention priority
                  <br />
                  <select
                    value={priorities[report.id] ?? "Medium"}
                    onChange={(event) =>
                      setPriorities((current) => ({
                        ...current,
                        [report.id]: event.target.value,
                      }))
                    }
                    disabled={busy}
                  >
                    {PRIORITIES.map((priority) => (
                      <option key={priority} value={priority}>
                        {priority}
                      </option>
                    ))}
                  </select>
                </label>

                <button
                  type="button"
                  onClick={() => void review(report.id, "accept")}
                  disabled={busy}
                >
                  Accept
                </button>

                <label>
                  Rejection reason *
                  <br />
                  <input
                    placeholder="Rejection reason"
                    value={reasons[report.id] ?? ""}
                    onChange={(event) =>
                      setReasons((current) => ({
                        ...current,
                        [report.id]: event.target.value,
                      }))
                    }
                    disabled={busy}
                  />
                </label>

                <button
                  type="button"
                  onClick={() => void review(report.id, "reject")}
                  disabled={busy}
                >
                  Reject
                </button>
              </div>
            )}
          </article>
        ))}
      </div>
    </section>
  );
}

export default IncidentReportsPanel;