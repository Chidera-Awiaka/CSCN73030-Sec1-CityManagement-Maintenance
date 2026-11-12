
## Sprint 2 additions (Nikhil Lakhwani): REG-01, REG-02, MNT-06, MNT-08

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
