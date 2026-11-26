## Maintenance Service – Design Sprint 1

**Tag:** `v1.0.0-sprint1` (main @ 0d5faab, merged via PR #16 on 24 Oct 2026)

### Issues fixed
| Issue | Requirement | Owner | PR |
|---|---|---|---|
| #1 | [MNT-01] Create an intervention – `POST /api/interventions` (unique ID, status To Do, default priority Medium, validation) | Chidera | #15 |
| #2 | [MNT-02] View and filter interventions – `GET /api/interventions?priority=&status=&location=`, sorted by priority, saved to file | Chidera | #15 |
| #3 | [MNT-03] Prioritize interventions – `PUT /api/interventions/{id}/priority` | Yaksh | #12, #13 |
| #4 | [MNT-07] Update operational status – `PUT /api/interventions/{id}/status` | Yaksh | #12, #13 |
| #5 | [TEC-01] Maintenance roles and permissions (Agent / Manager / Technician, `service_access` claim) | Noe | #8, #11 |
| #6 | [TEC-06] Test the core business rules (xUnit test projects) | Noe | #8, #11 |

Also: React/Vite front end (create form + filterable list) and Doxygen design docs (`docs/generated/html`).

### Known issues
- #9 [REG-01] and #10 [REG-02] were not finished this sprint and carried over to Sprint 2.
- TEC-01 roles (#5) are defined and unit-tested but not yet enforced on API endpoints (no authentication yet).
- MNT-03 (#3) and MNT-07 (#4) verified manually; no automated tests yet.
- Data is stored in a local JSON file; no external services connected yet.

### Quick start
```
git checkout v1.0.0-sprint1
dotnet run --project backend/Maintenance.Api --launch-profile http   # http://localhost:5164
cd frontend && npm install && npm run dev                             # http://localhost:5173
```

### Testing
`dotnet test` → **34 executed, 34 passed, 0 failed**
- Maintenance.Api.Tests / InterventionServiceTests: 16/16 (#1, #2)
- Maintenance.Security.Tests / MaintenanceAuthorizationTests: 18/18 (#5, #6)
- Manual: change priority (#3), change status (#4), required-field validation (#1) – passed

**Traceability:** Requirement → issues #1–#6 → PRs #8, #11, #12, #13, #15 → sprint-1 → main (PR #16) → `v1.0.0-sprint1` → tests → Doxygen.
