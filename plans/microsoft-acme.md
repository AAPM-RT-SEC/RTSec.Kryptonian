# Microsoft ACME Implementation Plan

## Summary

RTSec.Kryptonian should implement ACME as a southbound Certificate Authority backend behind the existing EST-facing device interface. Devices continue to speak EST to Kryptonian, and Kryptonian speaks ACME to Let's Encrypt, ZeroSSL, private ACME CAs, or future Microsoft-compatible ACME services where available.

This fits the README architecture: Kryptonian is a unified certificate-management gateway for medical device manufacturers and hospitals, abstracting CA-specific details behind a common connector model.

There are adjacent commercial and open-source solutions in the PKI/CLM space, including Microsoft AD CS enrollment services, EJBCA, Smallstep, Venafi, and Keyfactor-style platforms. They solve parts of the problem, but they are broad PKI products rather than a focused medical-device EST gateway library. Kryptonian can still be useful as the device-facing abstraction layer.

## Key Design Decisions

- Keep EST as the primary device-facing protocol for v1.
- Implement ACME as an `ICaConnector` backend, not as a new northbound ACME server.
- Treat "Microsoft ACME" as RFC 8555 ACME unless a specific Microsoft proprietary profile is later identified.
- Do not generate or store device private keys during ACME enrollment. The device owns the private key, sends a CSR through EST, and Kryptonian finalizes the ACME order with that CSR.
- Keep Microsoft AD CS support as a separate connector roadmap item. AD CS commonly uses Microsoft enrollment services, NDES/SCEP, web enrollment, or RPC/COM flows rather than ACME.

## Implementation Changes

### ACME Connector

- Update `AcmeCaConnector` so issuance follows the RFC 8555 flow:
  - Load or create an ACME account for the configured directory URL and email.
  - Extract DNS identifiers from the CSR common name and subject alternative names.
  - Validate extracted identifiers against the EST profile hostname policy.
  - Create a new ACME order for the validated identifiers.
  - Fulfill every authorization using the configured challenge provider.
  - Finalize the order with the original CSR DER bytes from `ParsedCsr.RawData`.
  - Download the issued certificate chain.
  - Parse the leaf and issuer certificates into `X509Certificate2` objects without creating a PFX or attaching a private key.
  - Return the leaf certificate and chain through `CertificateIssuanceResult`.

- Remove the current behavior that generates a new ACME certificate private key and builds a PFX. That breaks EST's key ownership model because the resulting certificate would not match the device-held private key.

- Preserve the cached issuer chain behavior for `/cacerts`, but make it clear that unknown ACME CAs may not have a chain until first issuance succeeds.

### ACME Account Handling

- Reuse existing `AcmeAccount` storage:
  - `DirectoryUrl`
  - `Email`
  - `AccountUrl`
  - encrypted account private key
  - Terms of Service acceptance
  - External Account Binding metadata

- Create a new account only when no active account exists for the configured directory URL and email.

- Require External Account Binding fields as a pair:
  - `EabKeyId`
  - `EabHmacKey`

- Continue protecting account private keys and EAB secrets with the existing `IDataProtectionService`.

### Challenge Providers

- Keep `Http01ChallengeStore` as the HTTP-01 provider.
- Keep `AcmeChallengeController` serving `/.well-known/acme-challenge/{token}`.
- Require HTTP-01 deployments to route public challenge traffic for requested DNS names to Kryptonian.
- Do not silently fall back from HTTP-01 to DNS-01 unless a real DNS-01 provider is configured.
- Replace `Dns01ChallengeStub` with a real provider abstraction before DNS-01 is documented as production-ready. The stub can remain as a development/manual provider if named and documented that way.

### Configuration Validation

- Validate ACME backend config before connector creation:
  - `DirectoryUrl` is required and must be an absolute URI.
  - `Email` is required.
  - `PreferredChallengeType` must be one of the supported challenge provider names.
  - EAB key ID and EAB HMAC key must be supplied together.

- For public ACME CAs, reject CSRs containing local-only names or identifiers that are not valid DNS names.

- For private ACME CAs, allow internal DNS names only when the configured backend explicitly opts into private/internal issuance.

### Documentation And UI

- Update README/backend support tables so ACME is described by real capability:
  - HTTP-01 support
  - EAB support
  - staging-tested public ACME support
  - DNS-01 production provider pending unless implemented

- Add admin help text explaining when ACME is appropriate:
  - Public DNS certificates for reachable names.
  - Private ACME for internal device names.
  - AD CS/EJBCA/Smallstep/Vault connectors for many hospital-internal identities.

## Test Plan

### Unit Tests

- CSR DNS extraction:
  - CN-only DNS name.
  - SAN DNS names.
  - Duplicate CN/SAN names.
  - CSR with no DNS identifiers.
  - CSR with invalid/local identifiers.

- Profile matching:
  - CSR identifiers match exact EST profile hostname.
  - CSR identifiers match wildcard/profile policy where supported.
  - CSR identifiers outside profile policy are rejected.

- Connector behavior:
  - ACME issuance finalizes using original CSR bytes.
  - ACME issuance does not generate a replacement private key.
  - Downloaded certificate chain is parsed without PFX creation.
  - Account is reused when present.
  - Account is created when absent.
  - EAB settings are validated as a pair.

- Challenge behavior:
  - HTTP-01 prepare stores token and key authorization.
  - HTTP-01 endpoint returns the expected key authorization.
  - Cleanup removes the challenge token.
  - Unsupported challenge type fails clearly.

### Integration Tests

- Run an ACME integration suite against a local Pebble/Boulder test server or Let's Encrypt staging when network credentials/environment allow it.
- Verify EST `/simpleenroll` returns a base64 PKCS#7 response containing the ACME-issued certificate chain.
- Verify `/cacerts` returns a usable chain after first successful ACME issuance.
- Verify failed challenge validation records an enrollment event with an actionable error.

### Regression Tests

- Self-signed CA enrollment still passes.
- EST `/cacerts`, `/simpleenroll`, and `/simplereenroll` response formats remain unchanged.
- Existing admin CA backend CRUD tests remain valid.

## Acceptance Criteria

- A device can send a valid EST enrollment request with a CSR for an approved DNS name and receive an ACME-issued certificate whose public key matches the original CSR.
- Kryptonian never stores or returns a private key for ACME enrollment.
- ACME account credentials are encrypted at rest and reused across requests.
- Failed ACME challenges fail with clear audit and log messages.
- The public documentation accurately distinguishes ACME backend support from Microsoft AD CS connector support.

## Assumptions

- The first implementation target is EST-to-ACME bridging.
- RFC 8555 is the relevant ACME standard.
- Public ACME CAs are only appropriate for DNS-valid names that the ACME CA can validate.
- Internal hospital/device identities will usually need private ACME, AD CS, EJBCA, Smallstep, Vault PKI, or another enterprise CA backend.
