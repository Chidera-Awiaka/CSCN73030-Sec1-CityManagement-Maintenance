/** Base URL of the Maintenance API (matches the "http" profile in launchSettings.json). */
export const API_BASE = "http://localhost:5164/api";

export type Intervention = {
  id: number;
  description: string;
  location: string;
  issueType: string;
  priority: string;
  status: string;
  createdAt: string;
};

export const PRIORITIES = ["Low", "Medium", "High", "Critical"];

export const STATUSES = [
  "To Do",
  "Scheduled",
  "In Progress",
  "On Hold",
  "Completed",
  "Cancelled",
];
