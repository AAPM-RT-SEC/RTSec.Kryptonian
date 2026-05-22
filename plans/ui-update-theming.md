# Kryptonian Gateway UI Branding And Theming Plan

## Summary
Create a branded UI refresh centered on the existing React/Vite admin portal. Use the new logo from `artifacts/Kryptonian.png` as the visual source of truth: deep navy, gateway teal, white clinical space, and crisp certificate/security accents.

The primary surface is `src/RTSec.Kryptonian.Ui`, because it owns login, setup, users, API keys, device registration, and gateway management. Apply a lighter consistency pass to the legacy MudBlazor admin shell in `src/RTSec.Kryptonian.Web`.

## Key Changes
- Add reusable brand assets:
  - Copy the logo into `src/RTSec.Kryptonian.Ui/public/brand/kryptonian-gateway.png`.
  - Copy the same asset into `src/RTSec.Kryptonian.Web/wwwroot/img/kryptonian-gateway.png`.
  - Add/update favicon references using the logo or a cropped mark.
- Replace the current `ShieldCheck`-only branding with a `BrandMark` React component used in:
  - `LoginPage.tsx`
  - `SetupPage.tsx`
  - `App.tsx` sidebar
  - loading/auth shell states
- Update displayed product naming to `Kryptonian Gateway`, replacing the current `Kryptonian / MEDIATE Gateway` split where user-facing branding appears.
- Rework `styles.css` around brand tokens:
  - Navy: `#003070`
  - Deep navy: `#002060`
  - Teal: `#0080A0`
  - Clinical background: near-white blue/gray surfaces
  - Keep status colors distinct for success, warning, and danger.
- Modernize the login/setup portal:
  - Logo-led header, compact trust/security cues, polished form styling, responsive two-zone layout on desktop.
  - Keep the actual login form first-class and immediately usable.
- Make device registration and management feel like the core workflow:
  - Add a prominent "Device Registration Center" section on the Devices page.
  - Keep `Register Device` as the primary action.
  - Improve activation-code presentation with stronger visual hierarchy, copy feedback, expiry emphasis, and certificate lifecycle context.
  - Improve empty states for no devices, pending devices, and no certificates.
- Refine the main shell:
  - Branded sidebar header with logo.
  - Cleaner topbar, denser but polished panels, sharper tables, stable button/icon sizing, improved responsive behavior.
  - Keep layouts utilitarian and scan-friendly for repeated admin use.
- Apply light Blazor consistency:
  - Update MudBlazor theme primary/secondary/appbar colors to match the logo palette.
  - Add the logo/product name to `MainLayout.razor` or `NavMenu.razor`.
  - Avoid reworking Blazor feature pages unless needed for brand consistency.

## Public Interfaces
- No API, DTO, database, or backend contract changes.
- New static asset paths:
  - `/brand/kryptonian-gateway.png` in the React/Vite UI.
  - `/img/kryptonian-gateway.png` in the Blazor UI.
- Optional new React component: `BrandMark`, accepting size/context props for auth and sidebar use.

## Test Plan
- Run `npm run build` in `src/RTSec.Kryptonian.Ui`.
- Run `dotnet build RTSec.Kryptonian.sln`.
- Start the React UI and verify with browser screenshots:
  - Login page desktop and mobile.
  - Setup page desktop and mobile.
  - Dashboard.
  - Devices page with empty, pending, active, archived, and activation-code states where possible.
- Check contrast/readability for primary buttons, sidebar nav, badges, table text, errors, and activation-code display.
- Confirm logo loads from production-style Vite paths and Blazor static paths.

## Assumptions
- The React/Vite UI is the primary portal users should see.
- The Blazor project remains optional/legacy but should not look off-brand.
- The existing PNG logo is acceptable as-is; no vector recreation or logo redesign is required.
- The UI refresh should be visual/component-focused only, with no backend behavior changes.
