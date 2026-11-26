## Maintenance Service – Design Sprint 3

**Tag:** `v3.0.0-sprint3` (feature/maintenance-sprint-3 @ ffdd749, merged to main via PR #28 on 25 Nov 2026)

### Issues fixed
| Issue | Requirement |
|---|---|
| #52 | [ESV-03] Receive weather alerts from E-services – `POST /api/weather-alerts` (header `X-Source-Service: E-services`) |
| #30 | [ESV-04] Record my assessment of a weather alert |
| #31 | [ESV-05] Take action from a weather alert (create intervention / reschedule / put on hold) |
| #32 | [ESV-06] Send road closure and reopening information to E-services |
| #33 | [REG-03] View utility information supplied for a property |
| #34 | [REG-04] Send property-related service disruptions to the registry |
| #35–#39 | [BUD-01..05] Estimate costs, submit to Budget, receive decision, block unapproved work, compare estimated vs actual |
| #40, #41 | [STF-01, STF-02] Personnel/specializations from Staff; detect overlapping assignments |
| #42–#44 | [SEC-01..03] Assistance requests to Security, receive responses, send closure/work-status updates |
| #45–#47 | [TRP-01..03] Routes/schedules from Public Transport, send closures, receive diversions |
| #48 | [TEC-02] Integration contracts – `docs/integration-contracts.md` |
| #49 | [TEC-03] Mock external services for development and testing |
| #50 | [TEC-04] Track external exchanges and support retries |
| #51 | [TEC-05] Audit history |

Front end: Weather Alerts panel, intervention Budget / Security / Disruptions sections, Operations tab (Staff, Transit, Exchanges, Audit). Doxygen: `docs/sprint3/html`.

### Known issues
- External services are mocks (#49); real clients replace them once each team confirms the contracts in `docs/integration-contracts.md` (#48).
- TEC-01 roles (#5) are not yet enforced through a real login (authentication planned for the final).
- Data stored in local JSON files (no database).

### Quick start
```
dotnet run --project backend/Maintenance.Api --launch-profile http   # http://localhost:5164
cd frontend && npm install && npm run dev                             # http://localhost:5173
```
All requests: `backend/Maintenance.Api/Maintenance.Api.http`

### Testing
`dotnet test CityManagement.Maintenance.slnx` → **154 executed, 154 passed, 0 failed** (63 new + 91 regression). Record: `docs/test-results/sprint3-regression.md`
- New: WeatherAlertTests 17/17 · BudgetTests 13/13 · SecurityAndNoticeTests 11/11 · IntegrationPlatformTests 13/13 · Sprint3EndpointTests 9/9
- Regression: all Sprint 1 and 2 suites (91/91)
- Defects found in smoke testing and fixed before release: duplicate "Completed → Completed" audit entry, missing audit for weather-driven status changes, missing BudgetSubmitted audit on retry.

**Traceability:** Requirement → issues #30–#52 → PR #28 → main → `v3.0.0-sprint3` → tests → Doxygen (`docs/sprint3/html`).
