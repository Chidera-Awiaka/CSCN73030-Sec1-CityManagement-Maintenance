# Maintenance Service – Integration Contracts (TEC-02)

Version 1.0 (Sprint 3) · Owner: Maintenance team (Group 2) · Status: **proposed – to be confirmed with each team**

Each exchange below defines its **direction, required fields, identifiers, response format and error handling**.
Until each team publishes its API, Maintenance uses mock services that follow these same contracts (TEC-03).
Every exchange is recorded in the exchange log (`GET /api/exchanges`) and failed outbound messages can be
retried (`POST /api/exchanges/{id}/retry`) without creating duplicates (TEC-04).

## Common rules

| Rule | Value |
|---|---|
| Transport | REST over HTTP, JSON bodies, UTF-8 |
| Dates | ISO-8601 in UTC, e.g. `2026-11-26T08:00:00Z` |
| Inbound caller identification | Header `X-Source-Service: <service name>` (E-services, Budget, Security, Public Transport). Messages from any other caller are rejected with **403**. |
| Maintenance user identification | Header `X-User-Id` (recorded in the audit history, TEC-05) |
| Inbound idempotency | Each message carries the sender's own ID (`externalAlertId`, `decisionId`, `responseId`, `externalReference`). A repeated delivery returns **200 `{ "duplicate": true, ... }`** and changes nothing. |
| Outbound idempotency | Maintenance builds a key from the business data; a message that already succeeded is never sent again, even on retry. |
| Outbound success | Receiving service returns 2xx with `{ "reference": "<its ID>" }`. Maintenance stores the reference. |
| Outbound failure | Non-2xx or timeout → exchange recorded as **Failed** with the error; visible in `GET /api/exchanges?status=Failed`; retry with `POST /api/exchanges/{id}/retry`. |
| Error body (Maintenance responses) | `{ "errors": ["message", ...] }` with 400 (invalid), 403 (wrong sender), 404 (unknown reference), 409 (state conflict), 502 (external service failed), 503 (external data unavailable) |

---

## 1. E-services

### 1.1 Incident report → Maintenance (ESV-01) — *inbound*
`POST /api/incident-reports` · fields: `externalReportId`*, `description`*, `location`*, `issueType`*, `reportedAt` ·
identifier: `externalReportId` · response: 201 report / 409 already received / 400 errors.

### 1.2 Weather alert → Maintenance (ESV-03) — *inbound*
`POST /api/weather-alerts` · header `X-Source-Service: E-services`

| Field | Required | Notes |
|---|---|---|
| `externalAlertId` | ✔ | identifier, idempotency key |
| `title` | ✔ | headline |
| `content` | | full text (title used when empty) |
| `affectedArea` | | e.g. "Waterloo - Uptown" |
| `severity` | | Minor, Moderate, Severe, Extreme |
| `validFrom`, `validTo` | | `validTo` ≥ `validFrom` |

Response: **201** alert · **200** `{duplicate:true, alert}` · **400** errors. Receiving an alert never creates interventions or changes priorities.

### 1.3 Road closure / reopening Maintenance → E-services (ESV-06) — *outbound*
Operation `RoadClosureNotice`:
```json
{ "interventionId": 7, "closureStatus": "Closed|Updated|Reopened", "location": "King Street North",
  "plannedStart": "2026-11-30T08:00:00Z", "plannedEnd": "2026-11-30T17:00:00Z", "interventionStatus": "Scheduled" }
```
Idempotency key: intervention + closure status + location + dates. Result recorded per service.

---

## 2. Property Registration

### 2.1 Property search (REG-01) and lookup (REG-02) — *query*
`GET /properties?propertyId=&address=` → `[{ propertyId, address, propertyType, ward }]`; 404 none; 503 unavailable.

### 2.2 Utility information (REG-03) — *query*
`GET /properties/{propertyId}/utilities` →
`{ propertyId, water?, electricity?, gas? }` where each utility is `{ provider, shutoffLocation?, notes? }`.
A missing utility is `null`; Maintenance lists it in `missingUtilities` and tells the user it was not supplied.

### 2.3 Property-related service disruption Maintenance → Registry (REG-04) — *outbound*
Operation `PropertyDisruption`:
```json
{ "interventionId": 7, "propertyIds": ["PR-1001","PR-1002"], "disruptionType": "Water|Electricity|Gas|Access|Other",
  "updateType": "New|Update|Restored", "periodStart": "...", "periodEnd": "...", "notes": "..." }
```

---

## 3. Budget

### 3.1 Budget request Maintenance → Budget (BUD-02) — *outbound*
Operation `BudgetRequest`:
```json
{ "interventionId": 7, "description": "Pothole repair",
  "costBreakdown": { "repair": 1200.00, "material": 350.50, "equipment": 400.00 },
  "total": 1950.50, "requestedBy": "mgr-01" }
```
Budget returns `{ "reference": "BUD-1001" }`; Maintenance stores it and sets the budget status to **Pending**.

### 3.2 Budget decision Budget → Maintenance (BUD-03) — *inbound*
`POST /api/budget/decisions` · header `X-Source-Service: Budget`

| Field | Required | Notes |
|---|---|---|
| `decisionId` | ✔ | identifier, idempotency key |
| `requestReference` | ✔ | the reference Budget returned in 3.1 – links the decision to the request |
| `decision` | ✔ | Approved or Rejected |
| `approvedAmount` | | ≥ 0 |
| `comments`, `decidedBy` | | recorded and shown on the intervention |

Response: 200 budget summary · 200 `{duplicate:true}` · 403 not Budget · 404 unknown reference · 409 not pending.

---

## 4. Security

### 4.1 Assistance request Maintenance → Security (SEC-01) — *outbound*
Operation `AssistanceRequest`:
```json
{ "maintenanceRequestId": 3, "interventionId": 7, "reason": "Traffic control for lane closure",
  "location": "King Street North", "periodStart": "...", "periodEnd": "...", "requestedBy": "mgr-01" }
```
Security returns `{ "reference": "SEC-1001" }`. Transmission status (Succeeded / Failed) is shown on the request.

### 4.2 Response Security → Maintenance (SEC-02) — *inbound*
`POST /api/security/responses` · header `X-Source-Service: Security` ·
fields `responseId`*, `requestReference`*, `outcome`* (Approved, Declined, Partially Approved), `comments`, `respondedBy`.
Maintenance users cannot call this endpoint (403): they cannot approve requests on Security's behalf.

### 4.3 Road closures and work-status updates Maintenance → Security (SEC-03) — *outbound*
`RoadClosureNotice` (same body as 1.3) and `WorkStatusUpdate`:
`{ interventionId, interventionStatus, location, plannedStart, plannedEnd }`.

---

## 5. Public Transport

### 5.1 Routes and schedules (TRP-01) — *query*
`GET /routes?street=` → `[{ routeId, name, streets[], schedule, lastUpdated? }]`.
Maintenance shows the source and update time when supplied; when the service is down it shows "unavailable" (503).

### 5.2 Road closures Maintenance → Public Transport (TRP-02) — *outbound*
`RoadClosureNotice` (same body as 1.3); reopening is sent with `closureStatus: "Reopened"`.

### 5.3 Route changes / diversions Public Transport → Maintenance (TRP-03) — *inbound*
`POST /api/transit/diversions` · header `X-Source-Service: Public Transport` ·
fields `externalReference`*, `affectedRoutes`* (≥ 1), `description`*, `effectiveFrom`*, `effectiveTo`.
Linked to interventions with `POST /api/transit/diversions/{id}/links { interventionId }`.

---

## 6. Staff

### 6.1 Personnel and specializations (STF-01) — *query*
`GET /personnel?specialization=` → `[{ personnelId, name, specializations[] }]`. Read-only: employee creation and
editing stay in Staff. `personnelId` is used as the technician ID in MNT-05.
Overlap warnings (STF-02) are computed from Maintenance's own assignments only and are labelled as such.
