import { useState } from "react";
import IncidentReportsPanel from "./IncidentReportsPanel";
import MaintenancePage from "./MaintenancePage";
import OperationsPanel from "./OperationsPanel";
import WeatherAlertsPanel from "./WeatherAlertsPanel";

function App() {
  // Re-mounts the intervention list when an incident report is accepted (ESV-02)
  // or a weather alert action changes interventions (ESV-05).
  const [refreshKey, setRefreshKey] = useState(0);
  const refresh = () => setRefreshKey((k) => k + 1);

  return (
    <>
      <IncidentReportsPanel onInterventionCreated={refresh} />
      <WeatherAlertsPanel onInterventionsChanged={refresh} />
      <MaintenancePage key={refreshKey} />
      <OperationsPanel refreshKey={refreshKey} />
    </>
  );
}

export default App;
