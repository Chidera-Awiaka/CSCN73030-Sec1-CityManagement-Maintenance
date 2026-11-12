/** Base URL of the Maintenance API (matches the "http" profile in launchSettings.json). */
export const API_BASE = "http://localhost:5000/api";

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
