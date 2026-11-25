/** API URL: local override or the default backend launch profile. */
export const API_BASE =
  import.meta.env.VITE_API_BASE_URL || "http://localhost:5164/api";

export type Intervention = {
  id: number;
  description: string;
  location: string;
  issueType: string;
  priority: string;
  status: string;
  createdAt: string;

  // MNT-04
  plannedStartDate?: string;
  plannedEndDate?: string;

  // MNT-05
  assignedTechnicianId?: number;
  assignedTechnicianName?: string;

  // REG-02: external Property IDs linked to the intervention
  linkedPropertyIds?: string[];

  // MNT-06
  instructions?: string | null;

  // MNT-08
  completionReport?: string | null;
  actualCompletionDate?: string | null;
  actualCost?: number | null;

  // BUD-01 .. BUD-03
  costEstimate?: {
    repairCost: number;
    materialCost: number;
    equipmentCost: number;
    total: number;
    estimatedBy: string;
    estimatedAt: string;
  } | null;
  budgetStatus?: string;
  budgetRequestReference?: string | null;
  budgetDecision?: {
    decision: string;
    approvedAmount?: number | null;
    comments?: string | null;
    decidedBy?: string | null;
    receivedAt: string;
  } | null;
};

/** REG-01: a property returned by the Property Registry. */
export type Property = {
  propertyId: string;
  address: string;
  propertyType: string;
  ward: string;
};

/** Reads the API's error message from a failed response (REG-01 / REG-02). */
export async function errorMessage(response: Response): Promise<string> {
  try {
    const data = await response.json();
    if (Array.isArray(data.errors)) return data.errors.join(" ");
    if (data.message) return data.message;
  } catch {
    // not JSON
  }
  return `Request failed (HTTP ${response.status}).`;
}

export const PRIORITIES = ["Low", "Medium", "High", "Critical"];

export const STATUSES = [
  "To Do",
  "Scheduled",
  "In Progress",
  "On Hold",
  "Completed",
  "Cancelled",
];

/** Maintenance user sent with Sprint 3 requests so changes appear in the audit history (TEC-05). */
export const CURRENT_USER = "mgr-01";

/** Result of an API call made with {@link call}. */
export type CallResult<T> = { ok: boolean; status: number; data?: T; error?: string };

/**
 * Calls the Maintenance API with the user header and JSON body (Sprint 3 helper).
 * Returns the parsed body on success or the API's error message on failure.
 */
export async function call<T>(method: string, path: string, body?: unknown): Promise<CallResult<T>> {
  try {
    const response = await fetch(`${API_BASE}${path}`, {
      method,
      headers: {
        "X-User-Id": CURRENT_USER,
        ...(body === undefined ? {} : { "Content-Type": "application/json" }),
      },
      body: body === undefined ? undefined : JSON.stringify(body),
    });
    if (!response.ok) {
      return { ok: false, status: response.status, error: await errorMessage(response) };
    }
    const text = await response.text();
    return { ok: true, status: response.status, data: text ? (JSON.parse(text) as T) : undefined };
  } catch {
    return { ok: false, status: 0, error: "Cannot reach the Maintenance API." };
  }
}

/** Formats an ISO date for display, or a fallback. */
export const formatDate = (value?: string | null, fallback = "—") =>
  value ? new Date(value).toLocaleString() : fallback;

/** Converts a datetime-local input value to ISO, or null. */
export const toIso = (value: string) => (value ? new Date(value).toISOString() : null);

/** Shared box style for Sprint 3 panels. */
export const panelStyle = { border: "1px solid #ccc", padding: "20px", marginBottom: "20px" } as const;
