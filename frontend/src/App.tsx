import { useState } from "react";
import IncidentReportsPanel from "./IncidentReportsPanel";
import MaintenancePage from "./MaintenancePage";

function App() {
  // Re-mounts the intervention list when an incident report is accepted (ESV-02)
  const [refreshKey, setRefreshKey] = useState(0);

  return (
    <>
      <IncidentReportsPanel onInterventionCreated={() => setRefreshKey((k) => k + 1)} />
      <MaintenancePage key={refreshKey} />
    </>
  );
}

export default App;
