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
const apiKey = import.meta.env.VITE_API_KEY as string | undefined;

async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers);
  if (!headers.has('Content-Type') && init.body) {
    headers.set('Content-Type', 'application/json');
  }
  if (apiKey) {
    headers.set('X-API-Key', apiKey);
  }

  const response = await fetch(`${apiBaseUrl}${path}`, {
    ...init,
    headers,
  });

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

export const api = {
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
  removeDevice: (id: string) => request<Device>(`/api/devices/${id}/remove`, { method: 'POST' }),
  getDeviceCertificates: (id: string) => request<Certificate[]>(`/api/devices/${id}/certificates`),
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
};
