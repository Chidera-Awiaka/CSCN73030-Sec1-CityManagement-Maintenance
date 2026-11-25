
## Sprint 2 additions: REG-01, REG-02, MNT-06, MNT-08

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/properties?propertyId=&address=` | REG-01 search by Property ID (exact) or address (partial). 404 when nothing matches, 400 when empty, 503 when the registry is unavailable |
| GET | `/api/interventions/{id}/properties` | REG-02 linked property details |
| POST | `/api/interventions/{id}/properties` | REG-02 link `{ "propertyId": "PR-1001" }` — 404 unknown property, 409 already linked |
| DELETE | `/api/interventions/{id}/properties/{propertyId}` | REG-02 remove a link |

Sprint 2 uses `MockPropertyRegistry` (PR-1001 … PR-1005) behind `IPropertyRegistry`, as allowed by REG-01, until the
Property Registration service publishes its API.

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/interventions/assigned/{technicianId}?includeClosed=false` | MNT-06 a technician's own interventions (location, planned dates, instructions, priority), earliest start first |
| PUT | `/api/interventions/{id}/close` | MNT-08 close: `{ "completionReport": "...", "actualCompletionDate": "...", "actualCost": 245.50 }` — report required; date defaults to now, cannot be in the future or before creation; cost cannot be negative; 409 if already Completed/Cancelled |

`instructions` can be supplied when creating an intervention (MNT-06). Test evidence: `docs/test-results/sprint2-nikhil-regression.md`.

## Sprint 3 additions

All remaining Maintenance requirements. External services are mocked (TEC-03) using the contracts in
[`docs/integration-contracts.md`](docs/integration-contracts.md) (TEC-02). Test evidence:
`docs/test-results/sprint3-regression.md`. Ready-made requests: `backend/Maintenance.Api/Maintenance.Api.http`.

| Area | Requirements | Main endpoints |
|---|---|---|
| Weather alerts | ESV-03 receive, ESV-04 assess, ESV-05 act | `POST/GET /api/weather-alerts`, `POST /api/weather-alerts/{id}/assessments`, `PUT .../review-status`, `POST .../actions` |
| Road closures | ESV-06, SEC-03, TRP-02 | `POST /api/interventions/{id}/road-closures`, `POST .../work-status-updates`, `GET .../notices` |
| Property Registry | REG-03 utilities, REG-04 disruptions | `GET /api/properties/{id}/utilities`, `POST /api/interventions/{id}/property-disruptions` |
| Budget | BUD-01 … BUD-05 | `PUT /api/interventions/{id}/budget/estimate`, `POST .../budget/submit`, `POST /api/budget/decisions`, `GET .../budget/comparison` |
| Security | SEC-01, SEC-02 | `POST/GET /api/interventions/{id}/security-requests`, `POST /api/security/responses` |
| Public Transport | TRP-01, TRP-03 | `GET /api/transit/routes`, `POST/GET /api/transit/diversions`, `POST .../diversions/{id}/links` |
| Staff | STF-01, STF-02 | `GET /api/staff?specialization=`, `GET /api/interventions/{id}/conflicts` (+ warnings on assign/schedule) |
| Platform | TEC-03 mocks, TEC-04 exchanges & retries, TEC-05 audit | `PUT /api/mock-services/{name}/availability`, `GET /api/exchanges`, `POST /api/exchanges/{id}/retry`, `GET /api/audit` |

**Headers:** user actions send `X-User-Id` (recorded in the audit history). Messages from other services must send
`X-Source-Service` (E-services, Budget, Security, Public Transport); anything else is refused with 403 — so Maintenance users
cannot record Budget or Security decisions on those teams' behalf.

**BUD-04** (pending confirmation) is off by default. To enforce "no In Progress without an approved budget", set
`"Budget": { "RequireApprovalToStart": true }` in `backend/Maintenance.Api/appsettings.json`.
