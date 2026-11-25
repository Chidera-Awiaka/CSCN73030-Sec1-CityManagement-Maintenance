# Sprint 3 — Regression test record

Branch `feature/maintenance-sprint-3`, based on `sprint-3` 588516e (ESV-01/ESV-02 by Chidera/Noe, Sprint 2 work merged).

## 1. Automated tests (`dotnet test CityManagement.Maintenance.slnx`)

| Test class | Requirements | Passed | Failed |
|---|---|---|---|
| WeatherAlertTests (new) | ESV-03, ESV-04, ESV-05 (+TEC-05) | 17 | 0 |
| BudgetTests (new) | BUD-01 .. BUD-05 | 13 | 0 |
| SecurityAndNoticeTests (new) | SEC-01, SEC-02, SEC-03, ESV-06, TRP-02, REG-04 | 11 | 0 |
| IntegrationPlatformTests (new) | TEC-03, TEC-04, TEC-05, STF-01, STF-02, TRP-01, TRP-03, REG-03 | 13 | 0 |
| Sprint3EndpointTests (new) | HTTP level: BUD-04, 403 inbound, audit, STF-02 warnings | 9 | 0 |
| AssignmentAndCloseTests | MNT-06, MNT-08 (regression) | 13 | 0 |
| IncidentReportServiceTests | ESV-01, ESV-02 (regression) | 19 | 0 |
| InterventionServiceTests | MNT-01, MNT-02 (regression) | 16 | 0 |
| PropertyEndpointTests | REG-01, REG-02 (regression) | 11 | 0 |
| PropertyLinkTests | REG-02 (regression) | 7 | 0 |
| PropertyRegistryTests | REG-01 (regression) | 7 | 0 |
| MaintenanceAuthorizationTests | TEC-01 (regression) | 18 | 0 |
| **Total** | | **154** | **0** |

## 2. Defects found during Sprint 3 testing (fixed and re-tested)

| Defect | Found by | Fix |
|---|---|---|
| Closing an intervention logged "Completed -> Completed" in the audit history | Live smoke test SM-49 | Previous status captured before closing (InterventionsController) |
| Status changes made from a weather alert (Reschedule / Put on hold) were not in the intervention's audit history | Live smoke test SM-49 | Audit entries added in WeatherAlertService; test WeatherStatusChanges_AuditedOnIntervention |
| A budget request that succeeded on retry had no BudgetSubmitted audit entry | Live smoke test SM-49 | Audit entry added on retry success (BudgetService) |
| Possible lock-order deadlock between Security responses and retry events | Code review | Inbound record moved outside the Security lock |

## 3. Detailed automated results

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
  Passed BudgetTests.Estimate_TotalCalculated
  Passed BudgetTests.Estimate_Invalid
  Passed BudgetTests.Submit_SendsBreakdownAndSetsPending
  Passed BudgetTests.Submit_NoEstimate_Invalid
  Passed BudgetTests.Submit_Twice_NoDuplicate
  Passed BudgetTests.Submit_BudgetDown_FailedThenRetried
  Passed BudgetTests.Decision_RecordedOnIntervention
  Passed BudgetTests.Decision_SourceReferenceAndDuplicates
  Passed BudgetTests.Rejected_RequiresRevisedEstimate
  Passed BudgetTests.Estimate_LockedWhilePending
  Passed BudgetTests.Policy_BlocksStartUnlessApproved
  Passed BudgetTests.Compare_EstimatedVsActual
  Passed BudgetTests.Compare_MissingData_Explained
  Passed IncidentReportServiceTests.Receive_ValidReport_StoredAsPending
  Passed IncidentReportServiceTests.Receive_TwoReports_GetUniqueIds
  Passed IncidentReportServiceTests.Receive_MissingRequiredField_IsInvalid(, Pothole, King St, Road, External report ID is required.)
  Passed IncidentReportServiceTests.Receive_MissingRequiredField_IsInvalid(ES-1, , King St, Road, Description is required.)
  Passed IncidentReportServiceTests.Receive_MissingRequiredField_IsInvalid(ES-1, Pothole,  , Road, Location is required.)
  Passed IncidentReportServiceTests.Receive_MissingRequiredField_IsInvalid(ES-1, Pothole, King St, , Issue type is required.)
  Passed IncidentReportServiceTests.Receive_FutureReportedDate_IsInvalid
  Passed IncidentReportServiceTests.Receive_SameExternalIdTwice_IsConflict
  Passed IncidentReportServiceTests.GetAll_FilterByStatus_ReturnsOnlyMatching
  Passed IncidentReportServiceTests.Receive_IsSavedAndReloadedFromFile
  Passed IncidentReportServiceTests.Accept_PendingReport_CreatesLinkedIntervention
  Passed IncidentReportServiceTests.Accept_NoPriority_InterventionDefaultsToMedium
  Passed IncidentReportServiceTests.Accept_MissingReviewerOrBadPriority_IsInvalidAndCreatesNothing
  Passed IncidentReportServiceTests.Reject_PendingReport_RecordsReasonAndCreatesNoIntervention
  Passed IncidentReportServiceTests.Reject_WithoutReason_IsInvalid
  Passed IncidentReportServiceTests.Review_UnknownReport_IsNotFound
  Passed IncidentReportServiceTests.Review_AlreadyReviewedReport_IsConflict
  Passed IncidentReportServiceTests.Filter_ByTechnicianId_ReturnsOnlyThatTechniciansWork
  Passed IncidentReportServiceTests.Filter_TechnicianIdCombinedWithStatus_AndUnknownTechnician
  Passed IntegrationPlatformTests.Exchange_IdempotentSend
  Passed IntegrationPlatformTests.Exchange_FailureVisibleAndRetry
  Passed IntegrationPlatformTests.Exchange_InboundDuplicate
  Passed IntegrationPlatformTests.Exchange_Persisted
  Passed IntegrationPlatformTests.Audit_ActorAndSource
  Passed IntegrationPlatformTests.Mock_ReferencesAndAvailability
  Passed IntegrationPlatformTests.Staff_ListAndFilter
  Passed IntegrationPlatformTests.Conflicts_Overlaps
  Passed IntegrationPlatformTests.Conflicts_Ignored
  Passed IntegrationPlatformTests.Transit_Routes
  Passed IntegrationPlatformTests.Diversions_ReceiveAndLink
  Passed IntegrationPlatformTests.Diversions_Invalid
  Passed IntegrationPlatformTests.Utilities_MissingIdentified
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
  Passed SecurityAndNoticeTests.SecurityRequest_SentWithAllFields
  Passed SecurityAndNoticeTests.SecurityRequest_Invalid
  Passed SecurityAndNoticeTests.SecurityRequest_FailedThenRetried
  Passed SecurityAndNoticeTests.SecurityResponse_Linked
  Passed SecurityAndNoticeTests.SecurityResponse_OnlySecurity
  Passed SecurityAndNoticeTests.RoadClosure_SentToThreeServices
  Passed SecurityAndNoticeTests.RoadClosure_ReopenAndNoDuplicate
  Passed SecurityAndNoticeTests.RoadClosure_PartialFailureRecorded
  Passed SecurityAndNoticeTests.RoadClosure_Invalid
  Passed SecurityAndNoticeTests.WorkStatus_SentToSecurity
  Passed SecurityAndNoticeTests.PropertyDisruption_NewAndRestored
  Passed Sprint3EndpointTests.WeatherEndpoints
  Passed Sprint3EndpointTests.StatusEndpoint_EnforcesBudgetRule
  Passed Sprint3EndpointTests.InterventionChanges_Audited
  Passed Sprint3EndpointTests.Schedule_ReturnsOverlapWarning
  Passed Sprint3EndpointTests.InboundEndpoints_RequireSourceService
  Passed Sprint3EndpointTests.BudgetDown_502ThenRetryEndpoint
  Passed Sprint3EndpointTests.LookupEndpoints
  Passed Sprint3EndpointTests.NoticeEndpoints
  Passed Sprint3EndpointTests.Close_AuditsPreviousStatus
  Passed WeatherAlertTests.Receive_StoresAllSuppliedFields
  Passed WeatherAlertTests.Receive_OptionalFieldsOmitted_Accepted
  Passed WeatherAlertTests.Receive_VisibleInListNewestFirst
  Passed WeatherAlertTests.Receive_DoesNotTouchInterventions
  Passed WeatherAlertTests.Receive_SameIdTwice_NoDuplicate
  Passed WeatherAlertTests.Receive_Invalid_Errors
  Passed WeatherAlertTests.Assess_RecordsAssessmentAndMarksReviewed
  Passed WeatherAlertTests.Assess_Invalid_Errors
  Passed WeatherAlertTests.ReviewStatus_Transitions
  Passed WeatherAlertTests.Action_CreateIntervention_LinkedAndRecorded
  Passed WeatherAlertTests.Action_LinkInterventions
  Passed WeatherAlertTests.Action_NoActionRequired
  Passed WeatherAlertTests.Action_Reschedule_LinkedOnly
  Passed WeatherAlertTests.Action_PutOnHold
  Passed WeatherAlertTests.Decisions_KeptOnAlertAndAudited
  Passed WeatherAlertTests.Action_InvalidType
  Passed WeatherAlertTests.WeatherStatusChanges_AuditedOnIntervention
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
Total: 154  Passed: 154  Failed: 0
```

## 4. Live API smoke test (running service, mock external services)

```
Sprint 3 live API smoke test 2026-11-25 04:46 UTC - branch feature/maintenance-sprint-3 (based on sprint-3 588516e)
ID    | Check                                                      | Expected | Result
SM-01 | ESV-03 receive alert with all fields                       | 201 | Pass
SM-02 | ESV-03 same alert again -> duplicate, nothing created      | 200 | Pass
SM-03 | ESV-03 invalid alert (no ID, bad severity)                 | 400 | Pass
SM-04 | ESV-03 alert list (Awaiting Review)                        | 200 | Pass
SM-05 | ESV-03 no intervention created automatically               | 200 | Pass
SM-06 | ESV-04 assessment without rationale                        | 400 | Pass
SM-07 | ESV-04 assessment (reviewer, date, decision, rationale)    | 200 | Pass
SM-08 | ESV-05 create intervention from alert                      | 200 | Pass
SM-09 | ESV-05 link existing intervention #1                       | 200 | Pass
SM-10 | ESV-05 reschedule linked intervention #1                   | 200 | Pass
SM-11 | ESV-05 put UNLINKED #2 on hold -> refused                  | 409 | Pass
SM-12 | ESV-05 put linked #3 on hold                               | 200 | Pass
SM-13 | ESV-05 'no action required' without notes                  | 400 | Pass
SM-14 | ESV-05 decisions kept on the alert                         | 200 | Pass
SM-15 | BUD-01 estimate 1200+350.50+400 -> total 1950.50           | 200 | Pass
SM-16 | TEC-03 Budget mock switched DOWN                           | 200 | Pass
SM-17 | BUD-02 submit while Budget down -> 502, recorded           | 502 | Pass
SM-18 | TEC-04 failed exchange visible (?status=Failed)            | 200 | Pass
SM-19 | TEC-03 Budget mock switched UP                             | 200 | Pass
SM-20 | TEC-04 retry the failed exchange -> succeeded              | 200 | Pass
SM-21 | BUD-02 status Pending, Budget reference kept               | 200 | Pass
SM-22 | BUD-02 submit again -> no duplicate request                | 200 | Pass
SM-23 | BUD-03 decision sent by a Maintenance user -> 403          | 403 | Pass
SM-24 | BUD-03 decision from Budget linked and recorded            | 200 | Pass
SM-25 | BUD-03 same decision again -> duplicate ignored            | 200 | Pass
SM-26 | BUD-05 comparison before actual cost -> explained          | 200 | Pass
SM-27 | STF-01 personnel filtered by specialization Road           | 200 | Pass
SM-28 | MNT-05 assign #1 to technician 101                         | 200 | Pass
SM-29 | MNT-05 assign #2 to technician 101                         | 200 | Pass
SM-30 | STF-02 schedule #2 overlapping #1 -> warning               | 200 | Pass
SM-31 | ESV-06/SEC-03/TRP-02 closure to all 3 services             | 200 | Pass
SM-32 | ESV-06 same closure again -> no duplicate                  | 200 | Pass
SM-33 | TRP-02 reopening sent to Public Transport                  | 200 | Pass
SM-34 | SEC-03 work-status update sent to Security                 | 200 | Pass
SM-35 | REG-04 disruption with no linked property -> 400           | 400 | Pass
SM-36 | REG-02 link PR-1002 to #1                                  | 200 | Pass
SM-37 | REG-04 water disruption sent to Property Registry          | 200 | Pass
SM-38 | REG-04 restoration sent                                    | 200 | Pass
SM-39 | REG-03 utilities PR-1002 -> Gas listed as missing          | 200 | Pass
SM-40 | SEC-01 assistance request -> transmission Succeeded        | 201 | Pass
SM-41 | SEC-02 response sent by Maintenance user -> 403            | 403 | Pass
SM-42 | SEC-02 response from Security linked to request            | 200 | Pass
SM-43 | TRP-01 routes on King Street with source/update time       | 200 | Pass
SM-44 | TRP-03 diversion received from Public Transport            | 201 | Pass
SM-45 | TRP-03 diversion linked to intervention #1                 | 200 | Pass
SM-46 | BUD-04 off by default: #2 -> In Progress allowed           | 200 | Pass
SM-47 | MNT-08 close #1 with actual cost 2100                      | 200 | Pass
SM-48 | BUD-05 1950.50 vs 2100 -> +149.50 (7.7%)                   | 200 | Pass
SM-49 | TEC-05 audit history of #1 with actor/source               | 200 | Pass
SM-50 | TEC-04 exchange log: 0 failed after retry                  | 200 | Pass
SM-51 | Regression: ESV-01 incident reports still work             | 200 | Pass

Audit history of intervention #1 (TEC-05):
   2026-11-25T04:46:02 | Scheduled | 2026-11-28 08:00 - 2026-11-28 16:00 (weather alert #1) | mgr-01
   2026-11-25T04:46:02 | StatusChanged | To Do -> Scheduled (weather alert #1) | mgr-01
   2026-11-25T04:46:02 | CostEstimated | Repair 1200.00 + material 350.50 + equipment 400.00 = 1950.50 | mgr-01
   2026-11-25T04:46:02 | BudgetSubmitted | Estimate 1950.50 submitted on retry (exchange #2), reference BUD-1001 | exchange retry
   2026-11-25T04:46:02 | BudgetDecisionReceived | Approved (1950.50) by B. Officer: Within Q4 envelope | source: Budget
   2026-11-25T04:46:02 | TechnicianAssigned | Alex Technician (101) | mgr-01
   2026-11-25T04:46:03 | Closed | Scheduled -> Completed | mgr-01
   2026-11-25T04:46:03 | StatusChanged | Scheduled -> Completed | mgr-01
```
