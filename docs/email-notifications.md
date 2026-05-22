# Email Notifications

Kryptonian can send email alerts when gateway events need administrator attention. The notification system is intentionally limited to operational events that can prevent device enrollment or break certificate-based communication if they are ignored.

## What Triggers Email

Email notifications support two event types:

| Event | When it sends | Why it matters |
|---|---|---|
| Enrollment rejected | A device attempts EST enrollment or re-enrollment and the gateway rejects the request. | An admin can investigate an unknown device, invalid activation, missing client certificate, failed certificate validation, or another policy issue. |
| Certificate near expiry | A valid device certificate is inside the configured warning window and the device has not recorded a newer renewal certificate. | An admin can contact the device owner, restore connectivity, or trigger renewal before the certificate expires and secure connections fail. |

The certificate expiry watcher runs in the API process. It scans daily, skips already-expired certificates, skips certificates whose device record points at a newer certificate, and suppresses repeated warnings for the same certificate for seven days.

## Settings Model

The notification configuration has three layers:

1. **Master switch**: `enabled` must be on. When it is off, no notification email is sent.
2. **Global event switches**: `notifyOnEnrollmentRejected` and `notifyOnCertificateNearExpiry` must be on for their event type.
3. **Recipient subscriptions**: each recipient independently opts into enrollment rejection alerts, certificate expiry alerts, or both.

All three layers must allow an event before a recipient receives mail. For example, if certificate expiry notifications are globally enabled but a recipient has only enrollment rejection selected, that recipient will not receive expiry alerts.

## SMTP Delivery

Configure SMTP delivery from **Settings > Email Notifications** in the admin UI.

Required fields:

| Field | Description |
|---|---|
| SMTP host | Hostname or IP address of the relay, for example `smtp.hospital.local`. |
| SMTP port | Common values are `587` for STARTTLS, `465` for implicit TLS, and `25` for plain relay. |
| TLS mode | `STARTTLS`, `Implicit TLS`, or `None`. |
| Authentication | `None`, `Basic`, or `NTLM`. |
| From address | Sender address used on notification messages. |

Optional fields:

| Field | Description |
|---|---|
| Username and password | Required when the auth mode is Basic or NTLM. Passwords are stored encrypted through ASP.NET Data Protection and are never returned to the UI. |
| From display name | Friendly sender name, such as `Kryptonian Gateway`. |
| Trust SMTP server certificate | Skips SMTP TLS certificate chain validation for internal-CA or self-signed relay certificates. Leave this off unless the relay certificate cannot be validated normally. |

Password update behavior:

| Submitted password value | Result |
|---|---|
| `null` | Keep the stored password. |
| Empty string | Clear the stored password. |
| Non-empty string | Replace the stored password. |

## Recipients

Recipients are configured separately from SMTP delivery. Add each admin, security team, or operations distribution list once, then choose which alert types that address should receive.

Common patterns:

| Recipient | Suggested subscriptions |
|---|---|
| Security or registration team | Enrollment rejected |
| Device operations team | Certificate near expiry |
| Shared support mailbox | Both events |

Removing a recipient stops future notification delivery to that address. It does not change previously logged enrollment events or certificate records.

## Test Email

Use **Send test email** from the Settings panel after entering SMTP details and a test recipient address. The test action uses the current draft settings on the page, including an unsaved password value, so admins can validate SMTP connectivity before saving.

The test email does not prove that event subscriptions are enabled. After SMTP delivery works, confirm the master switch, global event toggles, and recipient subscriptions are set as intended.

## Operational Notes

- Production notification failures are logged and swallowed so email outages do not block enrollment requests.
- Enrollment rejection emails include the operation, subject DN, device ID when known, client IP when known, time, and rejection reason.
- Certificate expiry emails include subject DN, device ID when known, serial number, expiration time, and days remaining.
- Notification settings and recipients are available through `/api/settings/notifications` and `/api/settings/notifications/recipients` for automation.
