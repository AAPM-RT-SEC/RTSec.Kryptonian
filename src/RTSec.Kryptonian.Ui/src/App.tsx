import { FormEvent, useEffect, useMemo, useState } from 'react';
import {
  Activity,
  Archive,
  CheckCircle2,
  Download,
  KeyRound,
  MonitorCheck,
  Plus,
  RefreshCw,
  Search,
  ServerCog,
  Settings,
  ShieldCheck,
  Trash2,
  UserCheck,
} from 'lucide-react';
import {
  api,
  ApiError,
  CaBackend,
  CaBackendInput,
  Certificate,
  Device,
  DeviceActivationCode,
  DeviceInput,
  EnrollmentEvent,
  EstProfile,
  EstProfileInput,
  GatewaySettings,
  GatewaySettingsInput,
  NotificationRecipient,
  NotificationSettings,
  NotificationSettingsInput,
  SmtpAuthMode,
  SmtpTlsMode,
} from './api';

type Page = 'dashboard' | 'settings' | 'cas' | 'devices' | 'profiles' | 'events';

interface Flash {
  kind: 'success' | 'error' | 'info';
  message: string;
}

interface Snapshot {
  backends: CaBackend[];
  devices: Device[];
  profiles: EstProfile[];
  events: EnrollmentEvent[];
  settings: GatewaySettings | null;
  certificatesByDevice: Record<string, Certificate[]>;
}

const emptySnapshot: Snapshot = {
  backends: [],
  devices: [],
  profiles: [],
  events: [],
  settings: null,
  certificatesByDevice: {},
};

const backendTypes = ['selfsigned', 'adcs', 'ejbca', 'acme'];
const oidByBackend: Record<string, string> = {
  selfsigned: '1.3.6.1.4.1.99999.1',
  adcs: '1.3.6.1.4.1.99999.2',
  ejbca: '1.3.6.1.4.1.99999.3',
  acme: 'ACME-issued certificate',
};

const defaultBackendConfig: Record<string, Record<string, unknown>> = {
  selfsigned: {
    PfxPath: '',
    PfxPassword: '',
    CertPath: '',
    KeyPath: '',
  },
  adcs: {
    BaseUrl: '',
    TemplateName: 'DicomDeviceAuthentication',
    ValidityDays: 7,
  },
  ejbca: {
    BaseUrl: '',
    CertificateProfile: 'MedicalDeviceTLS',
    EndEntityProfile: 'DicomDevice',
    IssuerDn: '',
    ValidityDays: 7,
  },
  acme: {
    DirectoryUrl: '',
    Email: '',
    PreferredChallengeType: 'http-01',
    EabKeyId: '',
    EabHmacKey: '',
  },
};

export function App() {
  const [page, setPage] = useState<Page>('dashboard');
  const [snapshot, setSnapshot] = useState<Snapshot>(emptySnapshot);
  const [loading, setLoading] = useState(true);
  const [flash, setFlash] = useState<Flash | null>(null);

  const refresh = async () => {
    setLoading(true);
    try {
      const [backends, devices, profiles, events, settings, certificates] = await Promise.all([
        api.getCaBackends(),
        api.getDevices(),
        api.getEstProfiles(),
        api.getEnrollmentEvents(100),
        api.getGatewaySettings(),
        api.getAllCertificates(),
      ]);

      // Group by deviceId once; certificates from the API are already ordered newest
      // first. Orphan certs (deviceId null — issued via EST without a matching
      // pending-device row) are dropped here on purpose; the registry only shows
      // certs that belong to a known device.
      const certificatesByDevice: Record<string, Certificate[]> = {};
      for (const cert of certificates) {
        if (!cert.deviceId) continue;
        (certificatesByDevice[cert.deviceId] ??= []).push(cert);
      }

      setSnapshot({
        backends,
        devices,
        profiles,
        events,
        settings,
        certificatesByDevice,
      });
    } catch (error) {
      setFlash({ kind: 'error', message: describeError(error) });
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void refresh();
  }, []);

  const run = async <T,>(
    operation: () => Promise<T>,
    success: string,
    options: { refresh?: boolean } = {},
  ): Promise<T | undefined> => {
    try {
      const result = await operation();
      setFlash({ kind: 'success', message: success });
      if (options.refresh !== false) {
        await refresh();
      }
      return result;
    } catch (error) {
      setFlash({ kind: 'error', message: describeError(error) });
      return undefined;
    }
  };

  const activeBackend = snapshot.backends.find((backend) => backend.isActive);

  return (
    <div className="shell">
      <aside className="sidebar">
        <div className="brand">
          <ShieldCheck size={28} />
          <div>
            <strong>Kryptonian</strong>
            <span>MEDIATE Gateway</span>
          </div>
        </div>
        <nav>
          <NavButton icon={<MonitorCheck />} active={page === 'dashboard'} onClick={() => setPage('dashboard')}>
            Dashboard
          </NavButton>
          <NavButton icon={<Settings />} active={page === 'settings'} onClick={() => setPage('settings')}>
            Settings
          </NavButton>
          <NavButton icon={<ServerCog />} active={page === 'cas'} onClick={() => setPage('cas')}>
            CA Backends
          </NavButton>
          <NavButton icon={<UserCheck />} active={page === 'devices'} onClick={() => setPage('devices')}>
            Devices
          </NavButton>
          <NavButton icon={<KeyRound />} active={page === 'profiles'} onClick={() => setPage('profiles')}>
            EST Profiles
          </NavButton>
          <NavButton icon={<Activity />} active={page === 'events'} onClick={() => setPage('events')}>
            Events
          </NavButton>
        </nav>
      </aside>

      <main>
        <header className="topbar">
          <div>
            <p className="eyebrow">Active backend</p>
            <h1>{activeBackend ? `${activeBackend.name} (${activeBackend.type})` : 'No active backend'}</h1>
          </div>
          <button className="iconButton" onClick={() => void refresh()} title="Refresh" aria-label="Refresh">
            <RefreshCw size={18} />
          </button>
        </header>

        {flash && (
          <div className={`flash ${flash.kind}`}>
            <span>{flash.message}</span>
            <button onClick={() => setFlash(null)}>Dismiss</button>
          </div>
        )}

        {loading ? (
          <section className="loading">Loading gateway state...</section>
        ) : (
          <>
            {page === 'dashboard' && <Dashboard snapshot={snapshot} />}
            {page === 'settings' && (
              <SettingsPage settings={snapshot.settings} run={run} />
            )}
            {page === 'cas' && <CaBackendsPage backends={snapshot.backends} run={run} />}
            {page === 'devices' && (
              <DevicesPage
                devices={snapshot.devices}
                certificatesByDevice={snapshot.certificatesByDevice}
                run={run}
                refresh={refresh}
              />
            )}
            {page === 'profiles' && (
              <ProfilesPage profiles={snapshot.profiles} backends={snapshot.backends} run={run} />
            )}
            {page === 'events' && <EventsPage events={snapshot.events} />}
          </>
        )}
      </main>
    </div>
  );
}

const lifetimePresetsHours: Array<{ label: string; hours: number }> = [
  { label: '24 hours', hours: 24 },
  { label: '7 days', hours: 24 * 7 },
  { label: '30 days', hours: 24 * 30 },
  { label: '90 days', hours: 24 * 90 },
  { label: '1 year', hours: 24 * 365 },
  { label: '2 years', hours: 24 * 365 * 2 },
];

function formatLifetime(hours: number): string {
  if (hours < 24) return `${hours} hour${hours === 1 ? '' : 's'}`;
  if (hours % (24 * 365) === 0) {
    const years = hours / (24 * 365);
    return `${years} year${years === 1 ? '' : 's'}`;
  }
  if (hours % 24 === 0) {
    const days = hours / 24;
    return `${days} day${days === 1 ? '' : 's'}`;
  }
  return `${hours} hours`;
}

function SettingsPage({ settings, run }: { settings: GatewaySettings | null; run: Runner }) {
  return (
    <div className="stack">
      <GatewayDefaultsSection settings={settings} run={run} />
      <NotificationsSection run={run} />
    </div>
  );
}

function GatewayDefaultsSection({ settings, run }: { settings: GatewaySettings | null; run: Runner }) {
  const minHours = settings?.minCertificateLifetimeHours ?? 24;
  const maxHours = settings?.maxCertificateLifetimeHours ?? 24 * 365 * 2;
  const [hours, setHours] = useState<number>(settings?.defaultCertificateLifetimeHours ?? 24);

  useEffect(() => {
    if (settings) {
      setHours(settings.defaultCertificateLifetimeHours);
    }
  }, [settings]);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    const input: GatewaySettingsInput = { defaultCertificateLifetimeHours: hours };
    await run(() => api.updateGatewaySettings(input), 'Saved gateway settings');
  };

  const clamp = (value: number) => Math.min(maxHours, Math.max(minHours, value));

  return (
    <section className="panel">
      <div className="panelHeader">
        <div>
          <h2>Device Registration Defaults</h2>
          <p>Gateway-wide defaults applied when issuing certificates to registered devices.</p>
        </div>
      </div>
      <form className="settingsGrid" onSubmit={(event) => void submit(event)}>
        <label className="settingsField">
          <span>Default Certificate Lifetime (hours)</span>
          <input
            type="number"
            min={minHours}
            max={maxHours}
            value={hours}
            onChange={(e) => setHours(clamp(Number(e.target.value)))}
            required
          />
          <small>
            {formatLifetime(hours)} &middot; range {formatLifetime(minHours)} to {formatLifetime(maxHours)}
          </small>
        </label>
        <label className="settingsField">
          <span>Quick presets</span>
          <select value={lifetimePresetsHours.find((p) => p.hours === hours)?.hours ?? ''} onChange={(e) => setHours(Number(e.target.value))}>
            <option value="">Custom</option>
            {lifetimePresetsHours.map((preset) => (
              <option key={preset.hours} value={preset.hours}>{preset.label}</option>
            ))}
          </select>
          <small>Choose a common lifetime or enter a custom hour value.</small>
        </label>
        <div className="settingsActions">
          <button className="primary" type="submit">Save Settings</button>
        </div>
      </form>
    </section>
  );
}

function NotificationsSection({ run }: { run: Runner }) {
  const [settings, setSettings] = useState<NotificationSettings | null>(null);
  const [recipients, setRecipients] = useState<NotificationRecipient[]>([]);
  const [draft, setDraft] = useState<NotificationSettingsInput | null>(null);
  // null = keep stored; '' = clear; anything else = replace
  const [passwordDraft, setPasswordDraft] = useState<string | null>(null);
  const [testRecipient, setTestRecipient] = useState('');
  const [newRecipient, setNewRecipient] = useState<{
    email: string;
    displayName: string;
    notifyOnEnrollmentRejected: boolean;
    notifyOnCertificateNearExpiry: boolean;
  }>({ email: '', displayName: '', notifyOnEnrollmentRejected: true, notifyOnCertificateNearExpiry: true });

  useEffect(() => {
    void reload();
  }, []);

  const reload = async () => {
    const [s, r] = await Promise.all([
      api.getNotificationSettings(),
      api.getNotificationRecipients(),
    ]);
    setSettings(s);
    setRecipients(r);
    setDraft(toInput(s));
    setPasswordDraft(null);
  };

  if (!draft) {
    return (
      <section className="panel">
        <div className="panelHeader"><div><h2>Email Notifications</h2><p>Loading…</p></div></div>
      </section>
    );
  }

  const update = <K extends keyof NotificationSettingsInput>(key: K, value: NotificationSettingsInput[K]) => {
    setDraft({ ...draft, [key]: value });
  };

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    const payload: NotificationSettingsInput = { ...draft, password: passwordDraft };
    const result = await run(() => api.updateNotificationSettings(payload), 'Saved notification settings');
    if (result) {
      setSettings(result);
      setDraft(toInput(result));
      setPasswordDraft(null);
    }
  };

  const sendTest = async () => {
    if (!testRecipient.trim()) return;
    await run(
      () => api.testNotificationSettings({
        settings: { ...draft, password: passwordDraft },
        recipientEmail: testRecipient.trim(),
      }),
      `Test email sent to ${testRecipient.trim()}`,
    );
  };

  const addRecipient = async (event: FormEvent) => {
    event.preventDefault();
    if (!newRecipient.email.trim()) return;
    const result = await run(
      () => api.upsertNotificationRecipient({
        email: newRecipient.email.trim(),
        displayName: newRecipient.displayName.trim() || null,
        notifyOnEnrollmentRejected: newRecipient.notifyOnEnrollmentRejected,
        notifyOnCertificateNearExpiry: newRecipient.notifyOnCertificateNearExpiry,
      }),
      'Recipient saved',
    );
    if (result) {
      await reload();
      setNewRecipient({ email: '', displayName: '', notifyOnEnrollmentRejected: true, notifyOnCertificateNearExpiry: true });
    }
  };

  const toggleRecipient = async (
    recipient: NotificationRecipient,
    key: 'notifyOnEnrollmentRejected' | 'notifyOnCertificateNearExpiry',
  ) => {
    await run(
      () => api.upsertNotificationRecipient({
        email: recipient.email,
        displayName: recipient.displayName ?? null,
        notifyOnEnrollmentRejected: key === 'notifyOnEnrollmentRejected' ? !recipient[key] : recipient.notifyOnEnrollmentRejected,
        notifyOnCertificateNearExpiry: key === 'notifyOnCertificateNearExpiry' ? !recipient[key] : recipient.notifyOnCertificateNearExpiry,
      }),
      'Recipient updated',
    );
    await reload();
  };

  const deleteRecipient = async (id: string) => {
    await run(() => api.deleteNotificationRecipient(id), 'Recipient removed');
    await reload();
  };

  const passwordPlaceholder = settings?.hasPassword
    ? '••••••• (stored — leave blank to keep)'
    : 'Leave blank for anonymous relay';

  return (
    <>
      <section className="panel">
        <div className="panelHeader">
          <div>
            <h2>Email Notifications</h2>
            <p>SMTP server for outbound alerts. Designed for on-prem relays (anonymous, Basic, or NTLM auth).</p>
          </div>
        </div>
        <form className="settingsGrid" onSubmit={(event) => void submit(event)}>
          <label className="settingsField">
            <span>Notifications enabled</span>
            <input
              type="checkbox"
              checked={draft.enabled}
              onChange={(e) => update('enabled', e.target.checked)}
            />
            <small>Master switch. When off, no emails are sent regardless of event toggles.</small>
          </label>

          <label className="settingsField">
            <span>SMTP host</span>
            <input
              type="text"
              value={draft.smtpHost}
              onChange={(e) => update('smtpHost', e.target.value)}
              placeholder="smtp.hospital.local"
              required
            />
          </label>

          <label className="settingsField">
            <span>SMTP port</span>
            <input
              type="number"
              min={1}
              max={65535}
              value={draft.smtpPort}
              onChange={(e) => update('smtpPort', Number(e.target.value))}
            />
            <small>587 (STARTTLS) · 465 (Implicit TLS) · 25 (plain)</small>
          </label>

          <label className="settingsField">
            <span>TLS mode</span>
            <select
              value={draft.tlsMode}
              onChange={(e) => update('tlsMode', e.target.value as SmtpTlsMode)}
            >
              <option value="starttls">STARTTLS</option>
              <option value="implicit">Implicit TLS</option>
              <option value="none">None (plain)</option>
            </select>
          </label>

          <label className="settingsField">
            <span>Authentication</span>
            <select
              value={draft.authMode}
              onChange={(e) => update('authMode', e.target.value as SmtpAuthMode)}
            >
              <option value="none">None (anonymous relay)</option>
              <option value="basic">Basic (username + password)</option>
              <option value="ntlm">NTLM (on-prem Exchange)</option>
            </select>
          </label>

          {draft.authMode !== 'none' && (
            <>
              <label className="settingsField">
                <span>Username</span>
                <input
                  type="text"
                  value={draft.username ?? ''}
                  onChange={(e) => update('username', e.target.value)}
                  required
                />
              </label>

              <label className="settingsField">
                <span>Password</span>
                <input
                  type="password"
                  value={passwordDraft ?? ''}
                  onChange={(e) => setPasswordDraft(e.target.value)}
                  placeholder={passwordPlaceholder}
                />
                <small>
                  Stored encrypted via ASP.NET Data Protection.{' '}
                  {settings?.hasPassword && (
                    <button
                      type="button"
                      onClick={() => setPasswordDraft('')}
                      style={{ background: 'none', border: 'none', padding: 0, marginLeft: 6, color: '#5b8ef7', cursor: 'pointer', font: 'inherit' }}
                    >
                      Clear stored password
                    </button>
                  )}
                </small>
              </label>
            </>
          )}

          <label className="settingsField">
            <span>From address</span>
            <input
              type="email"
              value={draft.fromAddress}
              onChange={(e) => update('fromAddress', e.target.value)}
              placeholder="kryptonian@hospital.local"
              required
            />
          </label>

          <label className="settingsField">
            <span>From display name</span>
            <input
              type="text"
              value={draft.fromDisplayName ?? ''}
              onChange={(e) => update('fromDisplayName', e.target.value)}
              placeholder="Kryptonian Gateway"
            />
          </label>

          <label className="settingsField">
            <span>Trust SMTP server certificate</span>
            <input
              type="checkbox"
              checked={draft.trustServerCertificate}
              onChange={(e) => update('trustServerCertificate', e.target.checked)}
            />
            <small>Skip TLS chain validation for internal-CA or self-signed SMTP servers. Off by default.</small>
          </label>

          <label className="settingsField">
            <span>Notify on enrollment rejection</span>
            <input
              type="checkbox"
              checked={draft.notifyOnEnrollmentRejected}
              onChange={(e) => update('notifyOnEnrollmentRejected', e.target.checked)}
            />
          </label>

          <label className="settingsField">
            <span>Notify on certificate near expiry</span>
            <input
              type="checkbox"
              checked={draft.notifyOnCertificateNearExpiry}
              onChange={(e) => update('notifyOnCertificateNearExpiry', e.target.checked)}
            />
          </label>

          <label className="settingsField">
            <span>Expiry warning window (days)</span>
            <input
              type="number"
              min={1}
              max={365}
              value={draft.expiryWarningDays}
              onChange={(e) => update('expiryWarningDays', Number(e.target.value))}
            />
            <small>Alert when a device certificate is within this many days of expiry and no renewal has been requested.</small>
          </label>

          <div className="settingsActions">
            <button className="primary" type="submit">Save Settings</button>
            <input
              type="email"
              value={testRecipient}
              onChange={(e) => setTestRecipient(e.target.value)}
              placeholder="test recipient email"
              style={{ marginLeft: 12, minWidth: 220 }}
            />
            <button type="button" onClick={() => void sendTest()} disabled={!testRecipient.trim()}>
              Send test email
            </button>
          </div>
        </form>
      </section>

      <section className="panel">
        <div className="panelHeader">
          <div>
            <h2>Notification Recipients</h2>
            <p>Each address subscribes independently per event type. Common pattern: security team for rejections, ops for expiry warnings.</p>
          </div>
        </div>
        <table className="table">
          <thead>
            <tr>
              <th>Email</th>
              <th>Name</th>
              <th style={{ textAlign: 'center' }}>Rejections</th>
              <th style={{ textAlign: 'center' }}>Expiry</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {recipients.length === 0 ? (
              <EmptyRow columns={5} text="No recipients configured. Add one below to start receiving notifications." />
            ) : (
              recipients.map((r) => (
                <tr key={r.id}>
                  <td>{r.email}</td>
                  <td>{r.displayName ?? <span className="muted">—</span>}</td>
                  <td style={{ textAlign: 'center' }}>
                    <input
                      type="checkbox"
                      checked={r.notifyOnEnrollmentRejected}
                      onChange={() => void toggleRecipient(r, 'notifyOnEnrollmentRejected')}
                    />
                  </td>
                  <td style={{ textAlign: 'center' }}>
                    <input
                      type="checkbox"
                      checked={r.notifyOnCertificateNearExpiry}
                      onChange={() => void toggleRecipient(r, 'notifyOnCertificateNearExpiry')}
                    />
                  </td>
                  <td>
                    <button type="button" onClick={() => void deleteRecipient(r.id)}>
                      <Trash2 size={14} /> Remove
                    </button>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>

        <form className="settingsGrid" onSubmit={(event) => void addRecipient(event)} style={{ marginTop: 16 }}>
          <label className="settingsField">
            <span>Email</span>
            <input
              type="email"
              value={newRecipient.email}
              onChange={(e) => setNewRecipient({ ...newRecipient, email: e.target.value })}
              placeholder="security@hospital.local"
              required
            />
          </label>
          <label className="settingsField">
            <span>Display name</span>
            <input
              type="text"
              value={newRecipient.displayName}
              onChange={(e) => setNewRecipient({ ...newRecipient, displayName: e.target.value })}
              placeholder="Security team"
            />
          </label>
          <label className="settingsField">
            <span>Enrollment rejections</span>
            <input
              type="checkbox"
              checked={newRecipient.notifyOnEnrollmentRejected}
              onChange={(e) => setNewRecipient({ ...newRecipient, notifyOnEnrollmentRejected: e.target.checked })}
            />
          </label>
          <label className="settingsField">
            <span>Cert near expiry</span>
            <input
              type="checkbox"
              checked={newRecipient.notifyOnCertificateNearExpiry}
              onChange={(e) => setNewRecipient({ ...newRecipient, notifyOnCertificateNearExpiry: e.target.checked })}
            />
          </label>
          <div className="settingsActions">
            <button className="primary" type="submit"><Plus size={14} /> Add recipient</button>
          </div>
        </form>
      </section>
    </>
  );
}

function toInput(s: NotificationSettings): NotificationSettingsInput {
  return {
    enabled: s.enabled,
    smtpHost: s.smtpHost,
    smtpPort: s.smtpPort,
    tlsMode: s.tlsMode,
    authMode: s.authMode,
    username: s.username ?? '',
    password: null,
    fromAddress: s.fromAddress,
    fromDisplayName: s.fromDisplayName ?? '',
    trustServerCertificate: s.trustServerCertificate,
    notifyOnEnrollmentRejected: s.notifyOnEnrollmentRejected,
    notifyOnCertificateNearExpiry: s.notifyOnCertificateNearExpiry,
    expiryWarningDays: s.expiryWarningDays,
  };
}

function Dashboard({ snapshot }: { snapshot: Snapshot }) {
  const issued24h = snapshot.events.filter((event) => {
    const ageMs = Date.now() - new Date(event.timestamp).getTime();
    return ageMs <= 24 * 60 * 60 * 1000 && event.status.toLowerCase() === 'issued';
  }).length;

  const rejected24h = snapshot.events.filter((event) => {
    const ageMs = Date.now() - new Date(event.timestamp).getTime();
    return ageMs <= 24 * 60 * 60 * 1000 && ['error', 'rejected'].includes(event.status.toLowerCase());
  }).length;

  const byBackend = useMemo(() => {
    const certs = Object.values(snapshot.certificatesByDevice).flat();
    return backendTypes.map((type) => ({
      type,
      certificate: certs.find((cert) => cert.caBackendType?.toLowerCase() === type),
    }));
  }, [snapshot.certificatesByDevice]);

  return (
    <div className="stack">
      <section className="metrics">
        <Metric label="CA Backends" value={snapshot.backends.length} detail={`${snapshot.backends.filter((b) => b.isEnabled).length} enabled`} />
        <Metric label="Registered Devices" value={snapshot.devices.length} detail={`${snapshot.devices.filter((d) => d.status === 'active').length} active`} />
        <Metric label="Issued 24h" value={issued24h} detail="successful enrollments" />
        <Metric label="Rejected 24h" value={rejected24h} detail="blocked or failed" tone={rejected24h ? 'warn' : 'neutral'} />
      </section>

      <section className="panel">
        <div className="panelHeader">
          <div>
            <h2>Backend Enrollment Evidence</h2>
            <p>Issued certificates remain visible after active-backend switches.</p>
          </div>
        </div>
        <div className="backendGrid">
          {byBackend.map(({ type, certificate }) => (
            <div className="backendTile" key={type}>
              <strong>{type.toUpperCase()}</strong>
              <span>{oidByBackend[type]}</span>
              {certificate ? (
                <p className="ok">Last cert {shortId(certificate.serialNumber)} · {certificate.gatewayOid ?? 'OID not observed'}</p>
              ) : (
                <p>No successful enrollment recorded</p>
              )}
            </div>
          ))}
        </div>
      </section>
    </div>
  );
}

function CaBackendsPage({ backends, run }: { backends: CaBackend[]; run: Runner }) {
  const [editing, setEditing] = useState<CaBackend | null>(null);
  const [creating, setCreating] = useState(false);

  return (
    <section className="panel">
      <div className="panelHeader">
        <div>
          <h2>CA Backends</h2>
          <p>Administrators choose one active upstream CA. Devices never choose a backend.</p>
        </div>
        <button className="primary" onClick={() => setCreating(true)}>
          <Plus size={16} /> Add CA Backend
        </button>
      </div>
      <table>
        <thead>
          <tr>
            <th>Name</th>
            <th>Type</th>
            <th>URL</th>
            <th>Status</th>
            <th>Active</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          {backends.map((backend) => (
            <tr key={backend.id}>
              <td>{backend.name}</td>
              <td><Badge>{backend.type}</Badge></td>
              <td>{backend.url ?? '-'}</td>
              <td>{backend.isEnabled ? 'Enabled' : 'Disabled'}</td>
              <td>{backend.isActive ? <span className="ok">Active</span> : 'Inactive'}</td>
              <td className="actions">
                <button disabled={backend.isActive} onClick={() => void run(() => api.activateCaBackend(backend.id), `Activated ${backend.name}`)}>
                  <CheckCircle2 size={15} /> Activate
                </button>
                <button onClick={() => void run(() => api.testCaBackend(backend.id), `Connection check completed for ${backend.name}`)}>Test</button>
                <button onClick={() => setEditing(backend)}>Edit</button>
                <button className="danger" onClick={() => void run(() => api.deleteCaBackend(backend.id), `Deleted ${backend.name}`)}>
                  <Trash2 size={15} />
                </button>
              </td>
            </tr>
          ))}
          {!backends.length && <EmptyRow columns={6} text="No CA backends configured." />}
        </tbody>
      </table>
      {(creating || editing) && (
        <BackendModal
          backend={editing}
          onClose={() => {
            setCreating(false);
            setEditing(null);
          }}
          run={run}
        />
      )}
    </section>
  );
}

function DevicesPage({
  devices,
  certificatesByDevice,
  run,
  refresh,
}: {
  devices: Device[];
  certificatesByDevice: Record<string, Certificate[]>;
  run: Runner;
  refresh: () => Promise<void>;
}) {
  const [creating, setCreating] = useState(false);
  const [tab, setTab] = useState<'registry' | 'archive'>('registry');
  const [registrySearch, setRegistrySearch] = useState('');
  const [archiveSearch, setArchiveSearch] = useState('');
  const [now, setNow] = useState(() => Date.now());
  const [reactivatedCode, setReactivatedCode] = useState<DeviceActivationCode | null>(null);
  const registryDevices = useMemo(() => devices.filter((device) => device.status !== 'removed'), [devices]);
  const archivedDevices = useMemo(() => devices.filter((device) => device.status === 'removed'), [devices]);
  const search = tab === 'registry' ? registrySearch : archiveSearch;
  const visibleDevices = useMemo(
    () => filterDevices(tab === 'registry' ? registryDevices : archivedDevices, certificatesByDevice, search),
    [archivedDevices, certificatesByDevice, registryDevices, search, tab],
  );

  const deleteArchivedDevice = (device: Device) => {
    const confirmed = window.confirm(
      `Permanently delete ${device.displayName} from the device registry? This cannot be undone.`,
    );
    if (!confirmed) {
      return;
    }

    void run(() => api.deleteDevice(device.id), `Permanently deleted ${device.displayName}`);
  };

  const reactivateDevice = async (device: Device) => {
    const activation = await run(
      () => api.reactivateActivationCode(device.id),
      `Generated a new activation code for ${device.displayName}`,
      { refresh: false },
    );
    if (activation) {
      setReactivatedCode(activation);
    }
  };

  const closeReactivatedCode = async () => {
    setReactivatedCode(null);
    await refresh();
  };

  useEffect(() => {
    const interval = window.setInterval(() => setNow(Date.now()), 1000);
    return () => window.clearInterval(interval);
  }, []);

  return (
    <section className="panel">
      <div className="panelHeader">
        <div>
          <h2>Devices</h2>
          <p>Register an alias to issue a one-time activation code; the device supplies its identity during activation.</p>
        </div>
        <button className="primary" onClick={() => setCreating(true)}>
          <Plus size={16} /> Register Device
        </button>
      </div>
      <div className="deviceToolbar">
        <div className="segmented" aria-label="Device views">
          <button className={tab === 'registry' ? 'active' : ''} onClick={() => setTab('registry')}>
            <MonitorCheck size={16} /> Active Devices <span>{registryDevices.length}</span>
          </button>
          <button className={tab === 'archive' ? 'active' : ''} onClick={() => setTab('archive')}>
            <Archive size={16} /> Archived Devices <span>{archivedDevices.length}</span>
          </button>
        </div>
        <label className="searchBox">
          <Search size={16} />
          <input
            value={search}
            onChange={(event) => {
              if (tab === 'registry') {
                setRegistrySearch(event.target.value);
              } else {
                setArchiveSearch(event.target.value);
              }
            }}
            placeholder={tab === 'registry' ? 'Search active devices' : 'Search archived devices'}
          />
        </label>
      </div>
      <table>
        <thead>
          <tr>
            <th>Alias</th>
            <th>Device Identity</th>
            <th>Status</th>
            <th>Activation</th>
            <th>Latest Certificate</th>
            <th>Last Renewed</th>
            <th>Expires In</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          {visibleDevices.map((device) => {
            const latest = certificatesByDevice[device.id]?.[0];
            const canReactivate = canReactivateActivationCode(device, now);
            return (
              <tr key={device.id}>
                <td>{device.displayName}</td>
                <td>
                  {device.status === 'pending' ? (
                    <span className="muted">Waiting for device activation</span>
                  ) : (
                    <>
                      <code>{device.subjectCommonName}</code>
                      <small>{[device.manufacturer, device.model, device.serialNumber].filter(Boolean).join(' · ')}</small>
                    </>
                  )}
                </td>
                <td><Badge tone={device.status === 'active' ? 'success' : device.status === 'removed' ? 'danger' : 'warn'}>{device.status}</Badge></td>
                <td>{activationState(device, now)}</td>
                <td>{latest ? `${latest.caBackendType ?? 'unknown'} · ${latest.gatewayOid ?? 'no OID'}` : '-'}</td>
                <td>{latest ? <RenewedAt certificate={latest} /> : <span className="muted">No certificate</span>}</td>
                <td><ExpiryCountdown certificate={latest} now={now} /></td>
                <td className="actions">
                  {latest && (
                    <button
                      onClick={() => downloadCertificate(device, latest)}
                      title="Download latest certificate (.pem)"
                      aria-label={`Download latest certificate for ${device.displayName}`}
                    >
                      <Download size={15} />
                    </button>
                  )}
                  {tab === 'archive' ? (
                    <button className="danger" onClick={() => deleteArchivedDevice(device)}>
                      <Trash2 size={15} /> Delete
                    </button>
                  ) : (
                    <>
                      {canReactivate && (
                        <button onClick={() => void reactivateDevice(device)}>
                          <KeyRound size={15} /> Reactivate
                        </button>
                      )}
                      <button className="danger" onClick={() => void run(() => api.removeDevice(device.id), `Archived ${device.displayName}`)}>
                        <Trash2 size={15} /> Archive
                      </button>
                    </>
                  )}
                </td>
              </tr>
            );
          })}
          {!visibleDevices.length && (
            <EmptyRow
              columns={8}
              text={search.trim() ? 'No devices match your search.' : tab === 'registry' ? 'No active devices registered.' : 'No archived devices.'}
            />
          )}
        </tbody>
      </table>
      <CertificateHistory devices={visibleDevices} certificatesByDevice={certificatesByDevice} />
      {creating && <DeviceModal onClose={() => setCreating(false)} run={run} refresh={refresh} />}
      {reactivatedCode && (
        <ActivationCodeModal
          title="Device Reactivated"
          activation={reactivatedCode}
          onClose={() => void closeReactivatedCode()}
        />
      )}
    </section>
  );
}

function RenewedAt({ certificate }: { certificate: Certificate }) {
  return (
    <>
      <span>{formatDate(certificate.createdAt)}</span>
      <small>{shortId(certificate.serialNumber)}</small>
    </>
  );
}

function ExpiryCountdown({ certificate, now }: { certificate?: Certificate; now: number }) {
  if (!certificate) {
    return <span className="countdown empty">No certificate</span>;
  }

  const remainingMs = new Date(certificate.notAfter).getTime() - now;
  if (!Number.isFinite(remainingMs)) {
    return <span className="countdown empty">Unknown</span>;
  }

  if (remainingMs <= 0) {
    return (
      <span className="countdown expired" title={`Expired ${formatDate(certificate.notAfter)}`}>
        Expired
      </span>
    );
  }

  const totalSeconds = Math.floor(remainingMs / 1000);
  const days = Math.floor(totalSeconds / 86400);
  const hours = Math.floor((totalSeconds % 86400) / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  const seconds = totalSeconds % 60;
  const tone = remainingMs <= 24 * 60 * 60 * 1000 ? 'urgent' : remainingMs <= 7 * 24 * 60 * 60 * 1000 ? 'soon' : 'healthy';
  const label = days > 0
    ? `${days}d ${hours}h ${minutes}m`
    : `${hours}h ${minutes}m ${seconds}s`;

  return (
    <span className={`countdown ${tone}`} title={`Expires ${formatDate(certificate.notAfter)}`}>
      <span className="pulse" />
      {label}
    </span>
  );
}

function filterDevices(
  devices: Device[],
  certificatesByDevice: Record<string, Certificate[]>,
  search: string,
) {
  const query = search.trim().toLowerCase();
  if (!query) {
    return devices;
  }

  return devices.filter((device) => {
    const certificates = certificatesByDevice[device.id] ?? [];
    const haystack = [
      device.displayName,
      device.subjectCommonName,
      device.manufacturer,
      device.model,
      device.serialNumber,
      device.status,
      ...certificates.flatMap((certificate) => [
        certificate.serialNumber,
        certificate.subjectDn,
        certificate.issuerDn,
        certificate.thumbprint,
        certificate.caBackendType,
        certificate.gatewayOid,
      ]),
    ]
      .filter(Boolean)
      .join(' ')
      .toLowerCase();

    return haystack.includes(query);
  });
}

function CertificateHistory({
  devices,
  certificatesByDevice,
}: {
  devices: Device[];
  certificatesByDevice: Record<string, Certificate[]>;
}) {
  const rows = devices.flatMap((device) =>
    (certificatesByDevice[device.id] ?? []).map((certificate) => ({ device, certificate })),
  );
  if (!rows.length) {
    return null;
  }
  return (
    <div className="subsection">
      <h3>Certificate History</h3>
      <table>
        <thead>
          <tr>
            <th>Device</th>
            <th>Backend</th>
            <th>Gateway OID</th>
            <th>Serial</th>
            <th>Expires</th>
          </tr>
        </thead>
        <tbody>
          {rows.map(({ device, certificate }) => (
            <tr key={certificate.id}>
              <td>{device.displayName}</td>
              <td>{certificate.caBackendType ?? '-'}</td>
              <td>{certificate.gatewayOid ?? 'not observed'}</td>
              <td>{shortId(certificate.serialNumber)}</td>
              <td>{formatDate(certificate.notAfter)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function ProfilesPage({
  profiles,
  backends,
  run,
}: {
  profiles: EstProfile[];
  backends: CaBackend[];
  run: Runner;
}) {
  const [creating, setCreating] = useState(false);
  const [editing, setEditing] = useState<EstProfile | null>(null);

  return (
    <section className="panel">
      <div className="panelHeader">
        <div>
          <h2>EST Profiles</h2>
          <p>Profiles describe the device-facing EST surface. Connector routing uses the active CA backend.</p>
        </div>
        <button className="primary" onClick={() => setCreating(true)}>
          <Plus size={16} /> Add EST Profile
        </button>
      </div>
      <table>
        <thead>
          <tr>
            <th>Name</th>
            <th>Hostnames</th>
            <th>Path</th>
            <th>Validity</th>
            <th>Status</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          {profiles.map((profile) => (
            <tr key={profile.id}>
              <td>{profile.name}</td>
              <td>{profile.hostnames.join(', ') || '-'}</td>
              <td><code>{profile.pathPrefix}</code></td>
              <td>{profile.validityDays} days</td>
              <td>{profile.isEnabled ? 'Enabled' : 'Disabled'}</td>
              <td><button onClick={() => setEditing(profile)}>Edit</button></td>
            </tr>
          ))}
          {!profiles.length && <EmptyRow columns={6} text="No EST profiles configured." />}
        </tbody>
      </table>
      {(creating || editing) && (
        <ProfileModal
          profile={editing}
          backends={backends}
          onClose={() => {
            setCreating(false);
            setEditing(null);
          }}
          run={run}
        />
      )}
    </section>
  );
}

function EventsPage({ events }: { events: EnrollmentEvent[] }) {
  return (
    <section className="panel">
      <div className="panelHeader">
        <div>
          <h2>Enrollment Events</h2>
          <p>Rejections show the policy reason before any CA connector is called.</p>
        </div>
      </div>
      <table>
        <thead>
          <tr>
            <th>Time</th>
            <th>Status</th>
            <th>Subject</th>
            <th>Device</th>
            <th>Reason</th>
          </tr>
        </thead>
        <tbody>
          {events.map((event) => (
            <tr key={event.id}>
              <td>{formatDate(event.timestamp)}</td>
              <td><Badge tone={event.status === 'issued' ? 'success' : 'danger'}>{event.status}</Badge></td>
              <td>{event.subjectDn ?? '-'}</td>
              <td>{event.deviceId ? shortId(event.deviceId) : '-'}</td>
              <td>{event.errorMessage ?? '-'}</td>
            </tr>
          ))}
          {!events.length && <EmptyRow columns={5} text="No enrollment events recorded." />}
        </tbody>
      </table>
    </section>
  );
}

function BackendModal({ backend, onClose, run }: { backend: CaBackend | null; onClose: () => void; run: Runner }) {
  const [name, setName] = useState(backend?.name ?? '');
  const [type, setType] = useState(backend?.type ?? 'selfsigned');
  const [url, setUrl] = useState(backend?.url ?? '');
  const [config, setConfig] = useState<Record<string, unknown>>({
    ...(defaultBackendConfig[backend?.type ?? 'selfsigned'] ?? {}),
    ...(backend?.config ?? {}),
  });
  const [rawConfig, setRawConfig] = useState(() => JSON.stringify({
    ...(defaultBackendConfig[backend?.type ?? 'selfsigned'] ?? {}),
    ...(backend?.config ?? {}),
  }, null, 2));
  const [showRawConfig, setShowRawConfig] = useState(false);
  const [isEnabled, setIsEnabled] = useState(backend?.isEnabled ?? true);
  const [isActive, setIsActive] = useState(backend?.isActive ?? false);
  const [error, setError] = useState<string | null>(null);

  const updateType = (nextType: string) => {
    setType(nextType);
    const nextConfig = {
      ...(defaultBackendConfig[nextType] ?? {}),
      ...config,
    };
    setConfig(nextConfig);
    setRawConfig(JSON.stringify(nextConfig, null, 2));
  };

  const updateConfig = (key: string, value: string | number) => {
    setConfig((current) => {
      const next = { ...current, [key]: value };
      setRawConfig(JSON.stringify(next, null, 2));
      return next;
    });
  };

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    try {
      const parsedConfig = JSON.parse(rawConfig || '{}') as Record<string, unknown>;
      const input: CaBackendInput = {
        name,
        type,
        url: url || null,
        config: parsedConfig,
        isEnabled,
        isActive,
      };
      await run(
        () => (backend ? api.updateCaBackend(backend.id, input) : api.createCaBackend(input)),
        backend ? `Updated ${name}` : `Created ${name}`,
      );
      onClose();
    } catch (err) {
      setError(describeError(err));
    }
  };

  return (
    <Modal title={backend ? 'Edit CA Backend' : 'Add CA Backend'} onClose={onClose}>
      <form onSubmit={(event) => void submit(event)} className="form">
        {error && <p className="formError">{error}</p>}
        <label>Name<input value={name} onChange={(e) => setName(e.target.value)} required /></label>
        <label>Type<select value={type} onChange={(e) => updateType(e.target.value)}>{backendTypes.map((item) => <option key={item}>{item}</option>)}</select></label>
        <label>Primary URL<input value={url} onChange={(e) => setUrl(e.target.value)} placeholder="https://..." /></label>
        <BackendConfigFields type={type} config={config} updateConfig={updateConfig} />
        <details className="advancedConfig" open={showRawConfig} onToggle={(e) => setShowRawConfig(e.currentTarget.open)}>
          <summary>Advanced JSON config</summary>
          <label>Config JSON<textarea value={rawConfig} onChange={(e) => setRawConfig(e.target.value)} rows={8} /></label>
        </details>
        <label className="check"><input type="checkbox" checked={isEnabled} onChange={(e) => setIsEnabled(e.target.checked)} /> Enabled</label>
        <label className="check"><input type="checkbox" checked={isActive} onChange={(e) => setIsActive(e.target.checked)} /> Activate after save</label>
        <div className="modalActions"><button type="button" onClick={onClose}>Cancel</button><button className="primary" type="submit">Save</button></div>
      </form>
    </Modal>
  );
}

function BackendConfigFields({
  type,
  config,
  updateConfig,
}: {
  type: string;
  config: Record<string, unknown>;
  updateConfig: (key: string, value: string | number) => void;
}) {
  const stringValue = (key: string) => String(config[key] ?? '');
  const numberValue = (key: string, fallback: number) => Number(config[key] ?? fallback);

  if (type === 'selfsigned') {
    return (
      <fieldset className="configFieldset">
        <legend>Self-signed CA Settings</legend>
        <p>Use a PFX file, or provide separate PEM certificate and key files.</p>
        <label>PFX Path<input value={stringValue('PfxPath')} onChange={(e) => updateConfig('PfxPath', e.target.value)} placeholder="certs/dev-ca.pfx" /></label>
        <label>PFX Password<input type="password" value={stringValue('PfxPassword')} onChange={(e) => updateConfig('PfxPassword', e.target.value)} /></label>
        <label>PEM Certificate Path<input value={stringValue('CertPath')} onChange={(e) => updateConfig('CertPath', e.target.value)} placeholder="certs/dev-ca.crt" /></label>
        <label>PEM Key Path<input value={stringValue('KeyPath')} onChange={(e) => updateConfig('KeyPath', e.target.value)} placeholder="certs/dev-ca.key" /></label>
      </fieldset>
    );
  }

  if (type === 'adcs') {
    return (
      <fieldset className="configFieldset">
        <legend>ADCS SCEP Settings</legend>
        <label>SCEP Base URL<input value={stringValue('BaseUrl')} onChange={(e) => updateConfig('BaseUrl', e.target.value)} placeholder="https://adcs.example/scep/mscep" /></label>
        <label>Template Name<input value={stringValue('TemplateName')} onChange={(e) => updateConfig('TemplateName', e.target.value)} /></label>
        <label>Validity Days<input type="number" min={1} value={numberValue('ValidityDays', 7)} onChange={(e) => updateConfig('ValidityDays', Number(e.target.value))} /></label>
      </fieldset>
    );
  }

  if (type === 'ejbca') {
    return (
      <fieldset className="configFieldset">
        <legend>EJBCA REST Settings</legend>
        <label>REST Base URL<input value={stringValue('BaseUrl')} onChange={(e) => updateConfig('BaseUrl', e.target.value)} placeholder="https://ejbca.example" /></label>
        <label>Certificate Profile<input value={stringValue('CertificateProfile')} onChange={(e) => updateConfig('CertificateProfile', e.target.value)} /></label>
        <label>End Entity Profile<input value={stringValue('EndEntityProfile')} onChange={(e) => updateConfig('EndEntityProfile', e.target.value)} /></label>
        <label>Issuer DN<input value={stringValue('IssuerDn')} onChange={(e) => updateConfig('IssuerDn', e.target.value)} placeholder="CN=ManagementCA,O=Hospital" /></label>
        <label>Validity Days<input type="number" min={1} value={numberValue('ValidityDays', 7)} onChange={(e) => updateConfig('ValidityDays', Number(e.target.value))} /></label>
      </fieldset>
    );
  }

  if (type === 'acme') {
    return (
      <fieldset className="configFieldset">
        <legend>ACME Settings</legend>
        <label>Directory URL<input value={stringValue('DirectoryUrl')} onChange={(e) => updateConfig('DirectoryUrl', e.target.value)} placeholder="https://acme-staging-v02.api.letsencrypt.org/directory" /></label>
        <label>Account Email<input type="email" value={stringValue('Email')} onChange={(e) => updateConfig('Email', e.target.value)} placeholder="device-admin@example.com" /></label>
        <label>Challenge Type<select value={stringValue('PreferredChallengeType') || 'http-01'} onChange={(e) => updateConfig('PreferredChallengeType', e.target.value)}><option value="http-01">HTTP-01</option><option value="dns-01">DNS-01</option></select></label>
        <label>EAB Key ID<input value={stringValue('EabKeyId')} onChange={(e) => updateConfig('EabKeyId', e.target.value)} /></label>
        <label>EAB HMAC Key<input type="password" value={stringValue('EabHmacKey')} onChange={(e) => updateConfig('EabHmacKey', e.target.value)} /></label>
      </fieldset>
    );
  }

  return null;
}

function DeviceModal({ onClose, run, refresh }: { onClose: () => void; run: Runner; refresh: () => Promise<void> }) {
  const stamp = new Date().toISOString().replace(/\D/g, '').slice(0, 14);
  const [alias, setAlias] = useState(`Demo Device ${stamp}`);
  const [activation, setActivation] = useState<DeviceActivationCode | null>(null);
  const [copyStatus, setCopyStatus] = useState<string | null>(null);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    const input: DeviceInput = { displayName: alias };
    const result = await run(async () => {
      const device = await api.createDevice(input);
      return api.generateActivationCode(device.id);
    }, `Registered ${alias} and generated an activation code`, { refresh: false });
    if (result) {
      setActivation(result);
      await copyActivationCode(result.activationCode);
    }
  };

  const copyActivationCode = async (code: string) => {
    if (!navigator.clipboard) {
      setCopyStatus('Clipboard is unavailable. Select and copy the code below.');
      return;
    }

    try {
      await navigator.clipboard.writeText(code);
      setCopyStatus('Copied to clipboard.');
    } catch {
      setCopyStatus('Clipboard copy was blocked. Select and copy the code below.');
    }
  };

  const closeAfterRefresh = async () => {
    await refresh();
    onClose();
  };

  return (
    <Modal title="Register Device" onClose={onClose}>
      {activation ? (
        <div className="activationResult">
          <label>
            Activation code
            <code>{activation.activationCode}</code>
          </label>
          {copyStatus && <p className="copyStatus">{copyStatus}</p>}
          <p>Expires {formatDate(activation.expiresAt)}. Give this code to the device; it will present its CN, manufacturer, model, and serial during activation.</p>
          <div className="modalActions">
            <button type="button" onClick={() => void copyActivationCode(activation.activationCode)}>Copy Code</button>
            <button className="primary" type="button" onClick={() => void closeAfterRefresh()}>Done</button>
          </div>
        </div>
      ) : (
        <form onSubmit={(event) => void submit(event)} className="form">
          <label>Alias/Nickname<input value={alias} onChange={(e) => setAlias(e.target.value)} required /></label>
          <div className="modalActions"><button type="button" onClick={onClose}>Cancel</button><button className="primary" type="submit">Register</button></div>
        </form>
      )}
    </Modal>
  );
}

function ActivationCodeModal({
  title,
  activation,
  onClose,
}: {
  title: string;
  activation: DeviceActivationCode;
  onClose: () => void;
}) {
  const [copyStatus, setCopyStatus] = useState<string | null>(null);

  const copyActivationCode = async () => {
    if (!navigator.clipboard) {
      setCopyStatus('Clipboard is unavailable. Select and copy the code below.');
      return;
    }

    try {
      await navigator.clipboard.writeText(activation.activationCode);
      setCopyStatus('Copied to clipboard.');
    } catch {
      setCopyStatus('Clipboard copy was blocked. Select and copy the code below.');
    }
  };

  return (
    <Modal title={title} onClose={onClose}>
      <div className="activationResult">
        <label>
          Activation code
          <code>{activation.activationCode}</code>
        </label>
        {copyStatus && <p className="copyStatus">{copyStatus}</p>}
        <p>Expires {formatDate(activation.expiresAt)}.</p>
        <div className="modalActions">
          <button type="button" onClick={() => void copyActivationCode()}>Copy Code</button>
          <button className="primary" type="button" onClick={onClose}>Done</button>
        </div>
      </div>
    </Modal>
  );
}

function ProfileModal({
  profile,
  backends,
  onClose,
  run,
}: {
  profile: EstProfile | null;
  backends: CaBackend[];
  onClose: () => void;
  run: Runner;
}) {
  const metadataBackendId = profile?.caBackendId || backends.find((backend) => backend.isActive)?.id || backends[0]?.id || '';
  const [name, setName] = useState(profile?.name ?? 'Default Device EST');
  const [hostnames, setHostnames] = useState((profile?.hostnames ?? ['localhost']).join(', '));
  const [pathPrefix, setPathPrefix] = useState(profile?.pathPrefix ?? '/.well-known/est');
  const [validityDays, setValidityDays] = useState(profile?.validityDays ?? 365);
  const [isEnabled, setIsEnabled] = useState(profile?.isEnabled ?? true);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    if (!metadataBackendId) {
      throw new Error('Add a CA backend before saving an EST profile.');
    }

    const input: EstProfileInput = {
      name,
      hostnames: hostnames.split(',').map((item) => item.trim()).filter(Boolean),
      hostnameMatchType: profile?.hostnameMatchType ?? 'exact',
      allowedWildcardSuffix: profile?.allowedWildcardSuffix ?? null,
      pathPrefix,
      caBackendId: metadataBackendId,
      certificateTemplate: profile?.certificateTemplate ?? null,
      allowedKeyUsages: profile?.allowedKeyUsages ?? ['digitalSignature', 'keyEncipherment'],
      validityDays,
      requireClientCertificate: profile?.requireClientCertificate ?? false,
      validateClientCertificateChain: profile?.validateClientCertificateChain ?? false,
      trustedClientCaThumbprints: profile?.trustedClientCaThumbprints ?? [],
      isEnabled,
    };
    await run(
      () => (profile ? api.updateEstProfile(profile.id, input) : api.createEstProfile(input)),
      profile ? `Updated ${name}` : `Created ${name}`,
    );
    onClose();
  };

  return (
    <Modal title={profile ? 'Edit EST Profile' : 'Add EST Profile'} onClose={onClose}>
      <form onSubmit={(event) => void submit(event)} className="form">
        <label>Name<input value={name} onChange={(e) => setName(e.target.value)} required /></label>
        <label>Hostnames<input value={hostnames} onChange={(e) => setHostnames(e.target.value)} required /></label>
        <label>Path Prefix<input value={pathPrefix} onChange={(e) => setPathPrefix(e.target.value)} required /></label>
        {!metadataBackendId && <p className="formError">Add a CA backend before saving an EST profile.</p>}
        <label>Validity Days<input type="number" min={1} value={validityDays} onChange={(e) => setValidityDays(Number(e.target.value))} /></label>
        <label className="check"><input type="checkbox" checked={isEnabled} onChange={(e) => setIsEnabled(e.target.checked)} /> Enabled</label>
        <div className="modalActions"><button type="button" onClick={onClose}>Cancel</button><button className="primary" type="submit" disabled={!metadataBackendId}>Save</button></div>
      </form>
    </Modal>
  );
}

type Runner = <T>(
  operation: () => Promise<T>,
  success: string,
  options?: { refresh?: boolean },
) => Promise<T | undefined>;

function Modal({ title, children, onClose }: { title: string; children: React.ReactNode; onClose: () => void }) {
  return (
    <div className="modalBackdrop" role="presentation">
      <div className="modal" role="dialog" aria-modal="true" aria-label={title}>
        <div className="modalHeader"><h2>{title}</h2><button onClick={onClose}>Close</button></div>
        {children}
      </div>
    </div>
  );
}

function NavButton({ icon, active, children, onClick }: { icon: React.ReactNode; active: boolean; children: React.ReactNode; onClick: () => void }) {
  return <button className={active ? 'active' : ''} onClick={onClick}>{icon}<span>{children}</span></button>;
}

function Metric({ label, value, detail, tone = 'neutral' }: { label: string; value: number; detail: string; tone?: 'neutral' | 'warn' }) {
  return <div className={`metric ${tone}`}><span>{label}</span><strong>{value}</strong><p>{detail}</p></div>;
}

function Badge({ children, tone = 'neutral' }: { children: React.ReactNode; tone?: 'neutral' | 'success' | 'warn' | 'danger' }) {
  return <span className={`badge ${tone}`}>{children}</span>;
}

function EmptyRow({ columns, text }: { columns: number; text: string }) {
  return <tr><td colSpan={columns} className="empty">{text}</td></tr>;
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat(undefined, { dateStyle: 'short', timeStyle: 'short' }).format(new Date(value));
}

function shortId(value: string) {
  return value.length > 18 ? `${value.slice(0, 8)}...${value.slice(-6)}` : value;
}

function downloadCertificate(device: Device, certificate: Certificate) {
  const blob = new Blob([certificate.certificatePem], { type: 'application/x-pem-file' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = `${sanitizeFilename(device.subjectCommonName || device.displayName || device.id)}.pem`;
  document.body.appendChild(a);
  a.click();
  a.remove();
  URL.revokeObjectURL(url);
}

function sanitizeFilename(value: string) {
  return value.replace(/[^a-zA-Z0-9._-]+/g, '_').replace(/^_+|_+$/g, '') || 'certificate';
}

function activationState(device: Device, now: number) {
  if (device.activationCodeUsedAt) {
    return `Used ${formatDate(device.activationCodeUsedAt)}`;
  }
  if (device.hasActivationCode && device.activationCodeExpiresAt) {
    if (new Date(device.activationCodeExpiresAt).getTime() <= now) {
      return `Code expired ${formatDate(device.activationCodeExpiresAt)}`;
    }
    return `Code expires ${formatDate(device.activationCodeExpiresAt)}`;
  }
  if (device.status === 'pending') {
    return 'No active code';
  }
  return '-';
}

function canReactivateActivationCode(device: Device, now: number) {
  if (device.status !== 'pending' || !device.hasActivationCode || device.activationCodeUsedAt || !device.activationCodeExpiresAt) {
    return false;
  }

  const expiresAt = new Date(device.activationCodeExpiresAt).getTime();
  return Number.isFinite(expiresAt) && expiresAt <= now;
}

function describeError(error: unknown) {
  if (error instanceof ApiError) {
    return error.message;
  }
  if (error instanceof Error) {
    return error.message;
  }
  return 'Unexpected error';
}
