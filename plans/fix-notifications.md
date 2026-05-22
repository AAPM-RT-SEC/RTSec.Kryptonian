# Fix Email Notifications Settings

## Summary

Make the Settings email notification feature clear and useful for admins. The backend already supports the intended behavior: a master notification switch, SMTP config, per-event toggles, per-recipient subscriptions, test email, rejected enrollment alerts, and certificate-expiry alerts. The UI should expose those capabilities as an organized configuration workflow instead of rendering boolean controls like unusable text fields.

## Key Changes

- Replace the clunky "Notifications enabled" field with a real switch-style control that clearly shows whether all outbound notification emails are active or paused.
- Reorganize `NotificationsSection` in `src/RTSec.Kryptonian.Ui/src/App.tsx` into:
  - status and master enablement
  - alert triggers for rejected enrollments and near-expiry certificates
  - SMTP delivery settings
  - draft test email action
  - recipient subscriptions
- Make the feature purpose explicit:
  - rejected enrollment alerts help admins investigate devices that tried to enroll and were rejected
  - certificate expiry alerts help admins act before a certificate expires when no newer renewal certificate has been recorded
- Keep "Save Settings" in a consistent right-aligned panel action area, separate from "Send test email" so testing draft SMTP settings does not feel like saving.
- Improve recipient management by keeping one row per recipient, using compact switches for each event subscription, and making the add-recipient form read as "who receives which alerts."
- Add CSS for notification-specific sections, switch controls, action grouping, compact recipient controls, and checkbox-specific sizing so boolean inputs no longer inherit text-input styling.

## Public Interfaces

- No backend API changes.
- Keep existing TypeScript API types:
  - `NotificationSettings`
  - `NotificationSettingsInput`
  - `NotificationRecipient`
  - `NotificationRecipientInput`
  - `NotificationTestRequest`
- Keep existing endpoints:
  - `GET/PUT /api/settings/notifications`
  - `GET/POST /api/settings/notifications/recipients`
  - `DELETE /api/settings/notifications/recipients/{id}`
  - `POST /api/settings/notifications/test`

## Test Plan

- Run `npm run build` from `src/RTSec.Kryptonian.Ui`.
- Verify the Settings page renders without TypeScript errors.
- Manually check the Settings panel at desktop and mobile widths:
  - master notification switch is visibly interactive
  - event toggles are readable and not text-input shaped
  - Save Settings is consistently aligned
  - test email controls are separate from save
  - recipient add/edit/remove flows remain usable
- Smoke-test behavior against the existing API:
  - toggling master enabled saves and reloads correctly
  - changing event toggles saves and reloads correctly
  - adding a recipient with one or both subscriptions works
  - removing a recipient works
  - sending a test email uses the draft SMTP settings without requiring save first

## Assumptions

- This is primarily a UI/UX fix; backend notification dispatch logic is already present and should not be changed unless implementation reveals a bug.
- Admins need a configuration screen, not a new notification history dashboard.
- The existing two alert types are the complete v1 scope.
- Password semantics stay unchanged: `null` keeps the stored password, empty string clears it, non-empty replaces it.
