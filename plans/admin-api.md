# Admin API Key Device Management for DICOM TLS Demo

## Summary

Update `Kryptonian.DICOMTls` so the demo can manage gateway device lifecycle operations through the existing admin API key. The app should be able to create fresh activation codes for the two demo devices, archive devices when they should no longer be active, and permanently delete archived devices, without requiring the admin UI.

## Key Changes

- Add CLI/env support:
  - `--admin-api-key <key>` or `KRYPTONIAN_ADMIN_API_KEY`
  - optional `--activation-valid-minutes <n>`, default `60`
  - keep `--activation-code-a` and `--activation-code-b` as explicit override inputs
  - optional cleanup flags:
    - `--archive-created-devices` archives devices created by this run after the transfer completes
    - `--delete-created-devices` archives, then permanently deletes devices created by this run after the transfer completes
  - optional direct management commands:
    - `--create-activation <display-name>` creates one pending device and activation code, prints the result, then exits
    - `--archive-device <device-id>` archives an existing device, then exits
    - `--delete-device <device-id>` permanently deletes an already archived device, then exits
- Add an admin API client in `Kryptonian.DICOMTls` that calls:
  - `POST /api/devices` with `{ "displayName": "<alias>" }`
  - `POST /api/devices/{id}/activation-code` with `{ "validForMinutes": n }`
  - `POST /api/devices/{id}/remove` to archive a device
  - `DELETE /api/devices/{id}` to permanently delete an archived device
  - `X-API-Key: <admin-api-key>` on both requests
- Demo startup behavior:
  - If both activation codes are provided, use them and do not call admin APIs.
  - If either activation code is missing and an admin API key is provided, create missing device activations automatically.
  - If activation codes are missing and no admin API key is provided, keep the current clear validation error.
- Device-management command behavior:
  - Commands require `--admin-api-key` or `KRYPTONIAN_ADMIN_API_KEY`.
  - `--create-activation` returns device ID, display name, activation code, and expiration timestamp.
  - `--archive-device` calls the gateway archive/remove endpoint and prints the resulting device status.
  - `--delete-device` calls the permanent delete endpoint and reports success; if the device is not archived, print the gateway error and suggest archiving first.
- Cleanup behavior:
  - Only applies to devices auto-created by this demo run, not devices provided via explicit activation codes.
  - `--archive-created-devices` runs after successful or failed transfer attempts, best effort.
  - `--delete-created-devices` first archives the created devices if needed, then deletes them.
  - Cleanup errors should not hide the transfer result; print them as a separate cleanup failure.
- Use generated aliases such as `DICOM TLS Demo Sender {timestamp}` and `DICOM TLS Demo Receiver {timestamp}` so gateway device records are easy to find later.
- Print the created device IDs and activation-code expiration timestamps, but never print the admin API key.

## Implementation Notes

- Reuse the existing gateway base URL from `--gateway` / `KRYPTONIAN_GATEWAY_URL`.
- Add DTOs local to `Kryptonian.DICOMTls` for the small admin API payloads/responses instead of referencing application-layer DTO projects.
- Treat admin provisioning as a bootstrap step before EST enrollment; the EST enrollment logic remains unchanged.
- Track device IDs for auto-created devices in memory so cleanup can archive/delete the right records after enrollment consumes activation codes and marks them active.
- Keep the activation token values in memory only unless normal demo artifact output already records enrollment results.
- Handle admin API failures with actionable messages:
  - `401` means missing/invalid admin API key.
  - `400` means invalid device or activation-code request.
  - `404` means the target device ID was not found.
  - network failures should name the gateway URL.

## Test Plan

- Build with `dotnet build RTSec.Kryptonian.sln`.
- Run without activation codes and without admin API key; confirm it fails before network calls with a clear message.
- Run with two activation codes; confirm no admin API calls are attempted.
- Run with `KRYPTONIAN_ADMIN_API_KEY` and no activation codes; confirm two devices and activation codes are created, then enrollment and DICOM TLS transfer proceed.
- Run with one activation code plus admin API key; confirm only the missing side is provisioned.
- Run with `--create-activation "DICOM TLS Manual Test"`; confirm one device and activation code are returned.
- Run with `--archive-device <id>`; confirm the device status becomes removed.
- Run with `--delete-device <id>` after archiving; confirm the delete endpoint returns success.
- Run with `--delete-device <active-id>`; confirm the gateway rejects it with a useful archived-device requirement.
- Run with `--archive-created-devices`; confirm created devices remain in the gateway but are removed/archived after the demo.
- Run with `--delete-created-devices`; confirm created demo devices are gone after the demo.
- Run with an invalid admin API key; confirm the error mentions unauthorized admin API access.

## Assumptions

- The hosted gateway admin API accepts `X-API-Key` as implemented by `ApiKeyAuthenticationHandler`.
- `POST /api/devices` creates a pending device using only `displayName`; the device CN, manufacturer, model, and serial are bound later during EST activation.
- Activation codes generated by `POST /api/devices/{id}/activation-code` are valid for pending devices and can be consumed immediately by the demo's EST enrollment step.
- `POST /api/devices/{id}/remove` is the archive operation for active or pending devices.
- `DELETE /api/devices/{id}` permanently deletes only archived/removed devices, matching the existing gateway safety rule.
