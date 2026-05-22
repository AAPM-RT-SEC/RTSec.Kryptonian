# Device Log Feature Plan

## Context

The Devices tab in the admin UI shows a table of registered medical devices but provides no way to see the history of what happened to a device over its lifetime — activations, enrollments, re-enrollments, errors, and status changes. Admins need this for troubleshooting and compliance auditing. The feature adds a `[...]` button per row that opens a modal showing a chronological event log for that specific device.

All the data already exists in the database (device lifecycle timestamps on the `Device` entity + `EnrollmentEvent` records linked via `DeviceRecordId`). No schema changes are required.

---

## Backend — New endpoint `GET /api/devices/{id}/events`

### 1. Add `DeviceEventDto`
Create `src/RTSec.Kryptonian.Application/DTOs/DeviceEventDto.cs`:
```csharp
public class DeviceEventDto
{
    public Guid Id { get; init; }
    public DateTime Timestamp { get; init; }
    public string EventType { get; init; } = string.Empty;
    // Issued | Pending | Rejected | Error | (null for lifecycle events)
    public string? Status { get; init; }
    public string? Detail { get; init; }       // error message or cert serial
    public string? IpAddress { get; init; }
    public Guid? CertificateId { get; init; }
}
```

### 2. Add handler logic in `DevicesController.cs`
File: `src/RTSec.Kryptonian.Api/Controllers/DevicesController.cs`

Add a new action:
```
GET /api/devices/{id}/events
Authorization: DeviceAdmin
```

Implementation steps inside the action:
1. Load the `Device` record (404 if not found).
2. Build lifecycle events from device timestamps (using `Guid.NewGuid()` as synthetic IDs):
   - `CreatedAt` → `EventType = "Created"`
   - `ApprovedAt` → `EventType = "Approved"`
   - `ActivationCodeUsedAt` → `EventType = "ActivationCodeUsed"`
   - `RemovedAt` → `EventType = "Removed"`
3. Query `IEnrollmentEventRepository` for records where `DeviceRecordId == id` (the existing index covers this).
4. Map enrollment events: `EventType` = `"Enrolled"` for the first cert issuance, `"ReEnrolled"` for subsequent ones; `Status` from `EnrollmentStatus`; `Detail` = cert serial or error message; `IpAddress` from `RequestorIpAddress`.
5. Merge and order all events by `Timestamp` descending.
6. Return `IEnumerable<DeviceEventDto>`.

### 3. Repository — filter by DeviceRecordId
`EnrollmentEventRepository` currently has `GetByProfileIdAsync` and `GetRecentAsync`. Add:
```csharp
public async Task<IEnumerable<EnrollmentEvent>> GetByDeviceIdAsync(Guid deviceId, CancellationToken ct = default)
```
Filter on `e.DeviceRecordId == deviceId`, order by `Timestamp` descending. The `DeviceRecordId` index in `EnrollmentEventConfiguration` already exists — no migration needed.

---

## Frontend — `DeviceLogModal` in `App.tsx`

### 1. New TypeScript type in `api.ts`
```typescript
export interface DeviceEvent {
  id: string;
  timestamp: string;
  eventType: string;
  status?: string | null;
  detail?: string | null;
  ipAddress?: string | null;
  certificateId?: string | null;
}
```

### 2. New API function in `api.ts`
```typescript
export async function getDeviceEvents(token: string, deviceId: string): Promise<DeviceEvent[]> {
  return apiFetch(`/api/devices/${deviceId}/events`, { token });
}
```

### 3. State in `DevicesPage`
Add alongside existing `creating` / `reactivatedCode` state:
```typescript
const [logDevice, setLogDevice] = useState<Device | null>(null);
```

### 4. `[...]` button in the device table row actions
In the actions cell (around line 1171 of App.tsx), add before or after the existing action buttons:
```tsx
<button
  className="btn btn-icon"
  title="Device Log"
  onClick={() => setLogDevice(device)}
>
  ...
</button>
```

### 5. `DeviceLogModal` component
Add alongside the existing `DeviceModal` / `ActivationCodeModal` components (after line ~1720). Follow the existing `Modal` wrapper pattern exactly.

Structure:
- `Modal` with `title="Device Log — {logDevice.displayName}"` and `onClose={() => setLogDevice(null)}`
- On mount (`useEffect` keyed on `logDevice?.id`), call `getDeviceEvents(token, logDevice.id)` and store in local state
- Show loading spinner while fetching (reuse existing `.loading` / spinner pattern from app)
- Render a table with columns: **Time**, **Event**, **Status**, **Detail**, **IP Address**
- Color-code the Status badge to match existing enrollment status colors (Issued = green, Error = red, Rejected = orange, Pending = gray)
- Show "No events recorded" empty state if the array is empty

Conditional render in DevicesPage JSX (alongside other modal conditionals):
```tsx
{logDevice && (
  <DeviceLogModal
    device={logDevice}
    token={token}
    onClose={() => setLogDevice(null)}
  />
)}
```

---

## Files to Modify

| File | Change |
|------|--------|
| `src/RTSec.Kryptonian.Application/DTOs/DeviceEventDto.cs` | **New file** — DTO |
| `src/RTSec.Kryptonian.Infrastructure/Repositories/EnrollmentEventRepository.cs` | Add `GetByDeviceIdAsync` |
| `src/RTSec.Kryptonian.Domain/Repositories/IEnrollmentEventRepository.cs` | Add interface method |
| `src/RTSec.Kryptonian.Api/Controllers/DevicesController.cs` | Add `GET /api/devices/{id}/events` action |
| `src/RTSec.Kryptonian.Ui/src/api.ts` | Add `DeviceEvent` type + `getDeviceEvents()` |
| `src/RTSec.Kryptonian.Ui/src/App.tsx` | Add state, `[...]` button, `DeviceLogModal` component |

---

## Verification

1. `dotnet build` — no compiler errors in the solution.
2. Run the app (`docker-compose up` or dev server).
3. Navigate to the Devices tab as an admin.
4. Verify `[...]` button appears on each device row with tooltip "Device Log".
5. Click `[...]` on a device that has enrollment history → modal opens, events appear in chronological order.
6. Click `[...]` on a newly registered pending device → modal shows only the "Created" event.
7. Click outside the modal or the X button → modal closes cleanly.
8. Network tab in browser DevTools: confirm `GET /api/devices/{id}/events` returns 200 with the expected JSON shape.
