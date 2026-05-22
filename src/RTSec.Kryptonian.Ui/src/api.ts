export type ApiMap = Record<string, unknown>;

export interface CaBackend {
  id: string;
  name: string;
  type: string;
  url?: string | null;
  config: ApiMap;
  isEnabled: boolean;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface CaBackendInput {
  name: string;
  type: string;
  url?: string | null;
  config?: ApiMap;
  isEnabled: boolean;
  isActive?: boolean;
}

export interface Device {
  id: string;
  displayName: string;
  subjectCommonName: string;
  manufacturer?: string | null;
  model?: string | null;
  serialNumber?: string | null;
  status: string;
  hasActivationCode: boolean;
  activationCodeExpiresAt?: string | null;
  activationCodeUsedAt?: string | null;
  approvedAt?: string | null;
  removedAt?: string | null;
  lastCertificateId?: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface DeviceInput {
  displayName: string;
  subjectCommonName?: string;
  manufacturer?: string | null;
  model?: string | null;
  serialNumber?: string | null;
}

export interface DeviceActivationCode {
  deviceId: string;
  subjectCommonName: string;
  serialNumber: string;
  activationCode: string;
  qrPayload: string;
  expiresAt: string;
}

export interface Certificate {
  id: string;
  /**
   * GUID of the Device record this certificate belongs to. Null for "orphan"
   * certs issued via EST without a matching pending-device row.
   */
  deviceId?: string | null;
  serialNumber: string;
  subjectDn: string;
  issuerDn: string;
  thumbprint: string;
  certificatePem: string;
  certificateDerBase64?: string | null;
  caBackendId?: string | null;
  caBackendType?: string | null;
  gatewayOid?: string | null;
  notBefore: string;
  notAfter: string;
  createdAt: string;
}

export interface DemoEnrollResponse {
  device: Device;
  certificate: Certificate;
  issuedByActiveBackend: CaBackend;
}

export interface EstProfile {
  id: string;
  name: string;
  hostnames: string[];
  hostnameMatchType: string;
  allowedWildcardSuffix?: string | null;
  pathPrefix: string;
  caBackendId: string;
  certificateTemplate?: string | null;
  allowedKeyUsages: string[];
  validityDays: number;
  requireClientCertificate: boolean;
  validateClientCertificateChain: boolean;
  trustedClientCaThumbprints: string[];
  isEnabled: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface EstProfileInput {
  name: string;
  hostnames: string[];
  hostnameMatchType: string;
  allowedWildcardSuffix?: string | null;
  pathPrefix: string;
  caBackendId: string;
  certificateTemplate?: string | null;
  allowedKeyUsages: string[];
  validityDays: number;
  requireClientCertificate: boolean;
  validateClientCertificateChain: boolean;
  trustedClientCaThumbprints: string[];
  isEnabled: boolean;
}

export interface EnrollmentEvent {
  id: string;
  timestamp: string;
  profileId: string;
  deviceId?: string | null;
  status: string;
  subjectDn?: string | null;
  requestorIpAddress?: string | null;
  errorMessage?: string | null;
  issuedCertificateId?: string | null;
}

export interface GatewaySettings {
  id: string;
  defaultCertificateLifetimeHours: number;
  minCertificateLifetimeHours: number;
  maxCertificateLifetimeHours: number;
  createdAt: string;
  updatedAt: string;
}

export interface GatewaySettingsInput {
  defaultCertificateLifetimeHours: number;
}

export type SmtpTlsMode = 'none' | 'starttls' | 'implicit';
export type SmtpAuthMode = 'none' | 'basic' | 'ntlm';

export interface NotificationSettings {
  id: string;
  enabled: boolean;
  smtpHost: string;
  smtpPort: number;
  tlsMode: SmtpTlsMode;
  authMode: SmtpAuthMode;
  username?: string | null;
  hasPassword: boolean;
  fromAddress: string;
  fromDisplayName?: string | null;
  trustServerCertificate: boolean;
  notifyOnEnrollmentRejected: boolean;
  notifyOnCertificateNearExpiry: boolean;
  expiryWarningDays: number;
  createdAt: string;
  updatedAt: string;
}

export interface NotificationSettingsInput {
  enabled: boolean;
  smtpHost: string;
  smtpPort: number;
  tlsMode: SmtpTlsMode;
  authMode: SmtpAuthMode;
  username?: string | null;
  /** null: keep stored. empty string: clear. non-empty: replace. */
  password?: string | null;
  fromAddress: string;
  fromDisplayName?: string | null;
  trustServerCertificate: boolean;
  notifyOnEnrollmentRejected: boolean;
  notifyOnCertificateNearExpiry: boolean;
  expiryWarningDays: number;
}

export interface NotificationRecipient {
  id: string;
  email: string;
  displayName?: string | null;
  notifyOnEnrollmentRejected: boolean;
  notifyOnCertificateNearExpiry: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface NotificationRecipientInput {
  email: string;
  displayName?: string | null;
  notifyOnEnrollmentRejected: boolean;
  notifyOnCertificateNearExpiry: boolean;
}

export interface NotificationTestRequest {
  settings: NotificationSettingsInput;
  recipientEmail: string;
}

export class ApiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
    public readonly body: unknown,
  ) {
    super(message);
  }
}

const apiBaseUrl = (import.meta.env.VITE_API_BASE_URL as string | undefined)?.replace(/\/$/, '') ?? '';
const staticApiKey = import.meta.env.VITE_API_KEY as string | undefined;

const TOKEN_KEY = 'kry_token';

export function getStoredToken(): string | null {
  return localStorage.getItem(TOKEN_KEY);
}

export function setStoredToken(token: string): void {
  localStorage.setItem(TOKEN_KEY, token);
}

export function clearStoredToken(): void {
  localStorage.removeItem(TOKEN_KEY);
}

type UnauthorizedCallback = () => void;
let onUnauthorized: UnauthorizedCallback | null = null;
export function setUnauthorizedHandler(cb: UnauthorizedCallback): void {
  onUnauthorized = cb;
}

async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers);
  if (!headers.has('Content-Type') && init.body) {
    headers.set('Content-Type', 'application/json');
  }

  const token = getStoredToken();
  if (token) {
    headers.set('Authorization', `Bearer ${token}`);
  } else if (staticApiKey) {
    headers.set('X-API-Key', staticApiKey);
  }

  const response = await fetch(`${apiBaseUrl}${path}`, {
    ...init,
    headers,
  });

  if (response.status === 401) {
    clearStoredToken();
    onUnauthorized?.();
    const body = await readJsonOrText(response);
    throw new ApiError('Session expired. Please log in again.', 401, body);
  }

  if (!response.ok) {
    const body = await readJsonOrText(response);
    const message =
      typeof body === 'object' && body && 'error' in body
        ? String((body as { error: unknown }).error)
        : `Request failed with HTTP ${response.status}`;
    throw new ApiError(message, response.status, body);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

async function readJsonOrText(response: Response): Promise<unknown> {
  const contentType = response.headers.get('content-type') ?? '';
  if (contentType.includes('application/json')) {
    return response.json();
  }
  return response.text();
}

const jsonBody = (value: unknown): RequestInit => ({
  method: 'POST',
  body: JSON.stringify(value),
});

export type AuthMode = 'setup' | 'login' | 'ok';

export interface AuthStatus {
  mode: AuthMode;
  username?: string | null;
  role?: string | null;
  userId?: string | null;
}

export interface AuthResponse {
  token: string;
  expiresAt: string;
  role: string;
  username: string;
  userId: string;
}

export interface UserProfile {
  id: string;
  username: string;
  email: string;
  role: string;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface ApiKeyRecord {
  id: string;
  name: string;
  prefix: string;
  role: string;
  ownerId: string;
  ownerUsername: string;
  expiresAt?: string | null;
  lastUsedAt?: string | null;
  revokedAt?: string | null;
  createdAt: string;
}

export interface GenerateApiKeyResponse {
  id: string;
  name: string;
  prefix: string;
  rawKey: string;
  role: string;
  expiresAt?: string | null;
  createdAt: string;
}

export const api = {
  getAuthStatus: () => request<AuthStatus>('/api/auth/status'),
  setup: (username: string, email: string, password: string) =>
    request<AuthResponse>('/api/auth/setup', jsonBody({ username, email, password })),
  login: (username: string, password: string) =>
    request<AuthResponse>('/api/auth/login', jsonBody({ username, password })),
  logout: () => request<void>('/api/auth/logout', { method: 'POST' }),
  getMe: () => request<{ id: string; username: string; role: string }>('/api/auth/me'),

  getUsers: () => request<UserProfile[]>('/api/users'),
  createUser: (username: string, email: string, password: string, role: string) =>
    request<UserProfile>('/api/users', jsonBody({ username, email, password, role })),
  updateUser: (id: string, update: { role?: string; isActive?: boolean; newPassword?: string }) =>
    request<UserProfile>(`/api/users/${id}`, { method: 'PUT', body: JSON.stringify(update) }),
  deleteUser: (id: string) => request<void>(`/api/users/${id}`, { method: 'DELETE' }),

  getApiKeys: (userId: string) => request<ApiKeyRecord[]>(`/api/users/${userId}/api-keys`),
  generateApiKey: (userId: string, name: string, expiresAt?: string | null) =>
    request<GenerateApiKeyResponse>(`/api/users/${userId}/api-keys`, jsonBody({ name, expiresAt })),
  revokeApiKey: (userId: string, keyId: string) =>
    request<void>(`/api/users/${userId}/api-keys/${keyId}`, { method: 'DELETE' }),

  getCaBackends: () => request<CaBackend[]>('/api/cas'),
  getActiveCaBackend: () => request<CaBackend>('/api/cas/active'),
  createCaBackend: (input: CaBackendInput) => request<CaBackend>('/api/cas', jsonBody(input)),
  updateCaBackend: (id: string, input: CaBackendInput) =>
    request<CaBackend>(`/api/cas/${id}`, { method: 'PUT', body: JSON.stringify(input) }),
  deleteCaBackend: (id: string) => request<void>(`/api/cas/${id}`, { method: 'DELETE' }),
  testCaBackend: (id: string) =>
    request<{ success: boolean }>(`/api/cas/${id}/test`, { method: 'POST' }),
  activateCaBackend: (id: string) =>
    request<CaBackend>(`/api/cas/${id}/activate`, { method: 'POST' }),

  getDevices: () => request<Device[]>('/api/devices'),
  createDevice: (input: DeviceInput) => request<Device>('/api/devices', jsonBody(input)),
  approveDevice: (id: string) => request<Device>(`/api/devices/${id}/approve`, { method: 'POST' }),
  generateActivationCode: (id: string, validForMinutes?: number) =>
    request<DeviceActivationCode>(
      `/api/devices/${id}/activation-code`,
      jsonBody(validForMinutes == null ? {} : { validForMinutes }),
    ),
  reactivateActivationCode: (id: string, validForMinutes?: number) =>
    request<DeviceActivationCode>(
      `/api/devices/${id}/activation-code/reactivate`,
      jsonBody(validForMinutes == null ? {} : { validForMinutes }),
    ),
  removeDevice: (id: string) => request<Device>(`/api/devices/${id}/remove`, { method: 'POST' }),
  deleteDevice: (id: string) => request<void>(`/api/devices/${id}`, { method: 'DELETE' }),
  getDeviceCertificates: (id: string) => request<Certificate[]>(`/api/devices/${id}/certificates`),
  // Batch: one DB query for every cert in the system; the dashboard groups by deviceId
  // client-side instead of firing a /api/devices/{id}/certificates per row.
  getAllCertificates: () => request<Certificate[]>(`/api/devices/certificates`),
  demoEnrollDevice: (id: string) =>
    request<DemoEnrollResponse>(`/api/devices/${id}/demo-enroll`, { method: 'POST' }),

  getEstProfiles: () => request<EstProfile[]>('/api/est-profiles'),
  createEstProfile: (input: EstProfileInput) =>
    request<EstProfile>('/api/est-profiles', jsonBody(input)),
  updateEstProfile: (id: string, input: EstProfileInput) =>
    request<EstProfile>(`/api/est-profiles/${id}`, { method: 'PUT', body: JSON.stringify(input) }),
  deleteEstProfile: (id: string) => request<void>(`/api/est-profiles/${id}`, { method: 'DELETE' }),

  getEnrollmentEvents: (limit = 50) =>
    request<EnrollmentEvent[]>(`/api/status/enrollments?limit=${encodeURIComponent(limit)}`),

  getGatewaySettings: () => request<GatewaySettings>('/api/settings/gateway'),
  updateGatewaySettings: (input: GatewaySettingsInput) =>
    request<GatewaySettings>('/api/settings/gateway', { method: 'PUT', body: JSON.stringify(input) }),

  getNotificationSettings: () =>
    request<NotificationSettings>('/api/settings/notifications'),
  updateNotificationSettings: (input: NotificationSettingsInput) =>
    request<NotificationSettings>('/api/settings/notifications', {
      method: 'PUT',
      body: JSON.stringify(input),
    }),
  getNotificationRecipients: () =>
    request<NotificationRecipient[]>('/api/settings/notifications/recipients'),
  upsertNotificationRecipient: (input: NotificationRecipientInput) =>
    request<NotificationRecipient>(
      '/api/settings/notifications/recipients',
      jsonBody(input),
    ),
  deleteNotificationRecipient: (id: string) =>
    request<void>(`/api/settings/notifications/recipients/${id}`, { method: 'DELETE' }),
  testNotificationSettings: (request_: NotificationTestRequest) =>
    request<void>('/api/settings/notifications/test', jsonBody(request_)),
};
