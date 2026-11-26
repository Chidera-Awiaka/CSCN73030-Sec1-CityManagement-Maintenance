## Maintenance Service – Design Sprint 2

**Tag:** `v2.0.0-sprint2` (main @ 9fd4848, merged 12 Nov 2026)

### Issues fixed
| Issue | Requirement | Owner | PR |
|---|---|---|---|
| #18 | [MNT-04] Schedule an intervention – `PUT /api/interventions/{id}/schedule` | Yaksh | #17, #21 |
| #19 | [MNT-05] Assign technicians or teams – `PUT /api/interventions/{id}/assign` | Yaksh | #17, #21 |
| #9 | [REG-01] Search the property registry by ID or address – `GET /api/properties` | Nikhil | #23 |
| #10 | [REG-02] Link properties to an intervention – `GET/POST/DELETE /api/interventions/{id}/properties` | Nikhil | #23 |
| – | MNT-06 Technician's assigned work – `GET /api/interventions/assigned/{technicianId}` | Nikhil | #23 |
| – | MNT-08 Close an intervention with report, date and cost – `PUT /api/interventions/{id}/close` | Nikhil | #23 |
| #25 | [ESV-01] Receive incident reports from E-services – `POST /api/incident-reports` | Chidera | #24 |
| #26 | [ESV-02] Accept or reject a report (accept creates a linked intervention) | Chidera | #24 |
| #2 | [MNT-02] fix: filter interventions by technician ID | Chidera | #24 |

Also: Sprint 2 Doxygen docs (`docs/sprint2/html`), UI panels for incident reports and properties.

### Known issues
- External services not live yet: E-services posts to our endpoint (#25); property registry (#9, #10) uses local sample data until the Registration API is confirmed.
- TEC-01 roles (#5) not yet enforced on endpoints (no authentication yet).
- MNT-04 (#18) and MNT-05 (#19) verified manually; no dedicated unit tests yet.
- Incident review UI hardening and configurable API URL – fixed after release in PR #27.
- Data stored in local JSON files (no database).

### Quick start
```
git checkout v2.0.0-sprint2
dotnet run --project backend/Maintenance.Api --launch-profile http   # http://localhost:5164
cd frontend && npm install && npm run dev                             # http://localhost:5173
```
E-services example: `POST /api/incident-reports` with `{ "externalReportId":"ESV-1001", "description":"Pothole", "location":"King St", "issueType":"Road" }`

### Testing
`dotnet test CityManagement.Maintenance.slnx` → **91 executed, 91 passed, 0 failed** (57 new + 34 Sprint 1 regression). Record: `docs/test-results/sprint2-nikhil-regression.md`
- IncidentReportServiceTests 19/19 (#25, #26, #2) · PropertyRegistryTests 7/7 (#9) · PropertyEndpointTests 11/11 (#9, #10) · PropertyLinkTests 7/7 (#10) · AssignmentAndCloseTests 13/13 (MNT-06, MNT-08)
- Regression: InterventionServiceTests 16/16 (#1, #2) · MaintenanceAuthorizationTests 18/18 (#5)
- Manual: schedule (#18), assign (#19), report → accept → intervention (#25, #26) – passed

**Traceability:** Requirement → issues #2, #9, #10, #18, #19, #25, #26 → PRs #17, #21, #23, #24 → main → `v2.0.0-sprint2` → tests → Doxygen (`docs/sprint2/html`).
