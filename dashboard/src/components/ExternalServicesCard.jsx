import { useEffect, useState } from 'react';
import CopyableText from './CopyableText';
import './ExternalServicesCard.css';

// Links operators to Grafana and the rest of the observability stack, with browser-reachable URLs and
// local development credentials. The list comes from the dashboard backend (env-configurable) so a
// deployment can hide services it does not run. See TELEMETRY.md.
export default function ExternalServicesCard() {
  const [services, setServices] = useState(null);
  const [error, setError] = useState(false);

  useEffect(() => {
    let cancelled = false;
    fetch('/api/observability/services')
      .then((res) => (res.ok ? res.json() : Promise.reject(new Error(`HTTP ${res.status}`))))
      .then((data) => {
        if (!cancelled) setServices(Array.isArray(data) ? data : []);
      })
      .catch(() => {
        if (!cancelled) setError(true);
      });
    return () => {
      cancelled = true;
    };
  }, []);

  return (
    <div className="info-section external-services">
      <h3 className="info-section-title">External Services</h3>
      {error && (
        <p className="external-services-empty">
          Observability links are unavailable: the dashboard backend did not return the service list.
        </p>
      )}
      {!error && services === null && <p className="external-services-empty">Loading services...</p>}
      {!error && services !== null && services.length === 0 && (
        <p className="external-services-empty">No observability services are configured for this deployment.</p>
      )}
      {!error && services !== null && services.length > 0 && (
        <div className="external-services-list">
          {services.map((svc) => (
            <div key={svc.name} className="external-service">
              <div className="external-service-header">
                <a className="external-service-name" href={svc.url} target="_blank" rel="noopener noreferrer">
                  {svc.name} ↗
                </a>
                {svc.description && <span className="external-service-description">{svc.description}</span>}
              </div>
              <div className="external-service-details">
                <CopyableText text={svc.url} />
                {svc.username && (
                  <span className="external-service-credentials">
                    <span className="external-service-label">user</span>
                    <code className="external-service-token">{svc.username}</code>
                    <span className="external-service-label">password</span>
                    <code className="external-service-token">{svc.password}</code>
                  </span>
                )}
              </div>
            </div>
          ))}
          <p className="external-services-note">
            Credentials shown are local development defaults. Change them before exposing these services.
          </p>
        </div>
      )}
    </div>
  );
}
