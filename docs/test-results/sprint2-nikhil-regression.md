# Sprint 2 — Regression test record (Nikhil Lakhwani)

Branch `feature/nikhil-sprint-2`, based on `main` 9a52a56 (team Sprint 2 code: MNT-04 / MNT-05).

Scope: REG-01 (#9) and REG-02 (#10), carried over from Sprint 1; MNT-06 and MNT-08 taken from the Maintenance backlog.

## 1. Automated tests (`dotnet test CityManagement.Maintenance.slnx`)

| Test class | Requirement | Passed | Failed |
|---|---|---|---|
| PropertyRegistryTests | REG-01 (new) | 7 | 0 |
| PropertyEndpointTests | REG-01, REG-02 endpoints (new) | 11 | 0 |
| PropertyLinkTests | REG-02 (new) | 7 | 0 |
| AssignmentAndCloseTests | MNT-06, MNT-08 (new) | 13 | 0 |
| InterventionServiceTests | MNT-01/02 regression (existing) | 16 | 0 |
| MaintenanceAuthorizationTests | TEC-01 regression (existing) | 18 | 0 |
| **Total** | | **72** | **0** |

```
  Passed AssignmentAndCloseTests.GetAssignedTo_ReturnsOnlyMyAssignments
  Passed AssignmentAndCloseTests.GetAssignedTo_IncludesLocationDatesInstructionsPriority
  Passed AssignmentAndCloseTests.GetAssignedTo_OrderedByPlannedStart
  Passed AssignmentAndCloseTests.GetAssignedTo_HidesClosedUnlessRequested
  Passed AssignmentAndCloseTests.Endpoint_NoAssignments_Returns200Empty
  Passed AssignmentAndCloseTests.Endpoint_InvalidTechnicianId_Returns400
  Passed AssignmentAndCloseTests.Close_RecordsReportDateCostAndCompletes
  Passed AssignmentAndCloseTests.Close_NoDate_DefaultsToNow
  Passed AssignmentAndCloseTests.ValidateClose_MissingReport_Error
  Passed AssignmentAndCloseTests.ValidateClose_BadDatesAndNegativeCost_Errors
  Passed AssignmentAndCloseTests.Close_AlreadyClosedOrCancelled_ReturnsAlreadyClosed
  Passed AssignmentAndCloseTests.Endpoint_Close_StatusCodes
  Passed AssignmentAndCloseTests.InstructionsAndClose_SavedAndReloaded
  Passed InterventionServiceTests.Create_ValidRequest_AssignsUniqueIdAndToDoStatus
  Passed InterventionServiceTests.Create_ValidRequest_IsAddedToList
  Passed InterventionServiceTests.Create_NoPriority_DefaultsToMedium
  Passed InterventionServiceTests.Validate_MissingRequiredField_ReturnsError(, Erb Street, Road, Description is required.)
  Passed InterventionServiceTests.Validate_MissingRequiredField_ReturnsError(Pothole,   , Road, Location is required.)
  Passed InterventionServiceTests.Validate_MissingRequiredField_ReturnsError(Pothole, Erb Street, , Issue type is required.)
  Passed InterventionServiceTests.Validate_InvalidPriority_ReturnsError
  Passed InterventionServiceTests.Create_InvalidRequest_ThrowsAndAddsNothing
  Passed InterventionServiceTests.Create_IsSavedAndReloadedFromFile
  Passed InterventionServiceTests.Filter_NoFilters_ReturnsAll
  Passed InterventionServiceTests.Filter_ByPriority_ReturnsOnlyMatching
  Passed InterventionServiceTests.Filter_ByLocationText_ReturnsOnlyMatching
  Passed InterventionServiceTests.Filter_ByStatus_ReturnsOnlyMatching
  Passed InterventionServiceTests.Filter_CombinedFilters_ReturnsOnlyMatchingAll
  Passed InterventionServiceTests.Filter_NoMatches_ReturnsEmptyList
  Passed InterventionServiceTests.Filter_SortsByPriorityHighestFirst
  Passed PropertyEndpointTests.Search_ById_Returns200WithIdAndAddress
  Passed PropertyEndpointTests.Search_ByAddress_Returns200WithMatches
  Passed PropertyEndpointTests.Search_NoMatch_Returns404
  Passed PropertyEndpointTests.Search_NoSearchTerm_Returns400
  Passed PropertyEndpointTests.Search_RegistryDown_Returns503
  Passed PropertyEndpointTests.Link_ThenVisibleInDetails
  Passed PropertyEndpointTests.Link_UnknownProperty_Returns404
  Passed PropertyEndpointTests.Link_UnknownIntervention_Returns404
  Passed PropertyEndpointTests.Link_Twice_Returns409
  Passed PropertyEndpointTests.Link_RegistryDown_Returns503
  Passed PropertyEndpointTests.Unlink_RemovesLink
  Passed PropertyLinkTests.LinkProperty_KeepsExternalPropertyId
  Passed PropertyLinkTests.LinkProperty_SupportsMoreThanOneProperty
  Passed PropertyLinkTests.LinkProperty_SamePropertyTwice_ReturnsAlreadyLinked
  Passed PropertyLinkTests.LinkProperty_UnknownIntervention_ReturnsNotFound
  Passed PropertyLinkTests.UnlinkProperty_RemovesTheLink
  Passed PropertyLinkTests.UnlinkProperty_NotLinked_ReturnsNotLinked
  Passed PropertyLinkTests.LinkProperty_IsSavedAndReloadedFromFile
  Passed PropertyRegistryTests.Search_ByPropertyId_ReturnsThatProperty
  Passed PropertyRegistryTests.Search_ByPropertyId_IsCaseInsensitiveAndTrimmed
  Passed PropertyRegistryTests.Search_ByAddressText_ReturnsAllMatching
  Passed PropertyRegistryTests.Search_ByIdAndAddress_MustMatchBoth
  Passed PropertyRegistryTests.Search_NoMatch_ReturnsEmptyList
  Passed PropertyRegistryTests.GetById_UnknownOrBlank_ReturnsNull
  Passed PropertyRegistryTests.Search_RegistryUnavailable_Throws
  Passed MaintenanceAuthorizationTests.Can_MatchesPermissionMatrix(Maintenance.Agent, CreateIntervention, True)
  Passed MaintenanceAuthorizationTests.Can_MatchesPermissionMatrix(Maintenance.Agent, ViewAllInterventions, True)
  Passed MaintenanceAuthorizationTests.Can_MatchesPermissionMatrix(Maintenance.Agent, UpdatePriority, False)
  Passed MaintenanceAuthorizationTests.Can_MatchesPermissionMatrix(Maintenance.Manager, CreateIntervention, True)
  Passed MaintenanceAuthorizationTests.Can_MatchesPermissionMatrix(Maintenance.Manager, ViewAllInterventions, True)
  Passed MaintenanceAuthorizationTests.Can_MatchesPermissionMatrix(Maintenance.Manager, UpdatePriority, True)
  Passed MaintenanceAuthorizationTests.Can_MatchesPermissionMatrix(Maintenance.Technician, CreateIntervention, False)
  Passed MaintenanceAuthorizationTests.Can_MatchesPermissionMatrix(Maintenance.Technician, ViewAllInterventions, False)
  Passed MaintenanceAuthorizationTests.Can_MatchesPermissionMatrix(Maintenance.Technician, UpdatePriority, False)
  Passed MaintenanceAuthorizationTests.Can_NullUser_ReturnsFalse
  Passed MaintenanceAuthorizationTests.Can_UnauthenticatedIdentity_ReturnsFalse
  Passed MaintenanceAuthorizationTests.Can_MissingServiceAccess_ReturnsFalse
  Passed MaintenanceAuthorizationTests.Can_MissingUserIdentifier_ReturnsFalse
  Passed MaintenanceAuthorizationTests.Can_UnrecognizedRole_ReturnsFalse(Unknown)
  Passed MaintenanceAuthorizationTests.Can_UnrecognizedRole_ReturnsFalse(Admin)
  Passed MaintenanceAuthorizationTests.Can_UnrecognizedRole_ReturnsFalse(Security.Manager)
  Passed MaintenanceAuthorizationTests.Can_UnknownPermission_ReturnsFalse
  Passed MaintenanceAuthorizationTests.Can_DoesNotCombineClaimsFromDifferentIdentities
Total: 72  Passed: 72  Failed: 0
```

## 2. Live API smoke test

Defect found during development: the Property Registry returned no properties at runtime because dependency
injection chose the test constructor of `MockPropertyRegistry`. Fixed in `Program.cs` (factory registration) before this run.

```
Sprint 2 smoke test (Nikhil) 2026-11-12 09:50 UTC on feature/nikhil-sprint-2 (based on main 9a52a56)
### SM-01 REG-01 search by ID PR-1003
[{"propertyId":"PR-1003","address":"45 Victoria Street, Kitchener","propertyType":"Residential","ward":"Ward 10"}]  -> HTTP 200
### SM-02 REG-01 search by address King Street
  -> HTTP 200
### SM-03 REG-01 no match
{"message":"No property found matching the search."}  -> HTTP 404
### SM-04 REG-01 empty search
{"message":"Enter a Property ID or an address to search."}  -> HTTP 400
### SM-05 REG-02 link PR-1003 to #1
{"message":"Property PR-1003 linked to intervention 1."}  -> HTTP 200
### SM-06 REG-02 link PR-1003 again
{"message":"Property PR-1003 is already linked."}  -> HTTP 409
### SM-07 REG-02 link unknown PR-9999
{"message":"Property 'PR-9999' was not found in the Property Registry."}  -> HTTP 404
### SM-08 REG-02 view linked properties
[{"propertyId":"PR-1003","address":"45 Victoria Street, Kitchener","propertyType":"Residential","ward":"Ward 10"}]  -> HTTP 200
### SM-09 REG-02 unlink PR-1003
{"message":"Property PR-1003 unlinked from intervention 1."}  -> HTTP 200
### SM-10 MNT-01 create with instructions
{"id":3,"description":"Pothole at bus stop","location":"Erb Street West","issueType":"Road","priority":"High","status":"To Do","createdAt":"2026-11-12T09:50:51.1366923Z","plannedStartDate":null,"plannedEndDate":null,"assignedTechnicianId":null,"assignedTechnicianName":null,"linkedPropertyIds":[],"instructions":"Bring cold patch and cones","completionReport":null,"actualCompletionDate":null,"actualCost":null}  -> HTTP 201
### SM-11 MNT-05 assign #3 to tech 101
{"message":"Technician assigned successfully."}  -> HTTP 200
### SM-12 MNT-04 schedule #3
{"message":"Intervention scheduled successfully."}  -> HTTP 200
### SM-13 MNT-06 tech 101 assignments (location, dates, instructions, priority)
[{"id":3,"description":"Pothole at bus stop","location":"Erb Street West","issueType":"Road","priority":"High","status":"Scheduled","createdAt":"2026-11-12T09:50:51.1366923Z","plannedStartDate":"2026-11-15T09:00:00Z","plannedEndDate":"2026-11-15T17:00:00Z","assignedTechnicianId":101,"assignedTechnicianName":"Alex Technician","linkedPropertyIds":[],"instructions":"Bring cold patch and cones","completionReport":null,"actualCompletionDate":null,"actualCost":null}]  -> HTTP 200
### SM-14 MNT-06 tech 102 only own (none)
[]  -> HTTP 200
### SM-15 MNT-06 invalid technician ID
{"message":"A valid technician ID is required."}  -> HTTP 400
### SM-16 MNT-08 close without report
{"errors":["A completion report is required."]}  -> HTTP 400
### SM-17 MNT-08 close with future date + negative cost
{"errors":["Actual completion date cannot be in the future.","Actual cost cannot be negative."]}  -> HTTP 400
### SM-18 MNT-08 close #3 with report and cost
{"message":"Intervention 3 closed."}  -> HTTP 200
### SM-19 MNT-08 completed details recorded
{"id":3,"description":"Pothole at bus stop","location":"Erb Street West","issueType":"Road","priority":"High","status":"Completed","createdAt":"2026-11-12T09:50:51.1366923Z","plannedStartDate":"2026-11-15T09:00:00Z","plannedEndDate":"2026-11-15T17:00:00Z","assignedTechnicianId":101,"assignedTechnicianName":"Alex Technician","linkedPropertyIds":[],"instructions":"Bring cold patch and cones","completionReport":"Filled pothole with cold patch","actualCompletionDate":"2026-11-12T09:50:51.2325408Z","actualCost":245.50}  -> HTTP 200
### SM-20 MNT-08 close #3 again
{"message":"Intervention 3 is already Completed."}  -> HTTP 409
### SM-21 MNT-06 closed work hidden from tech 101
[]  -> HTTP 200
### SM-22 Regression: MNT-02 list / MNT-03 priority
  -> HTTP 200
```
