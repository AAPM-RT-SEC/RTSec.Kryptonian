# Hackathon Team Structures

This document describes several ways to divide the hackathon team. The expected group is about eight programmers plus a few non-programmers who can help with testing, documentation, demo flow, screenshots, diagrams, and user-facing explanation.

There is no single correct structure. The best choice depends on whether the group wants one coordinated deliverable, competing prototypes, or two separate technical explorations.

## Option 1: Three Teams, One Shared Project

Everyone works in the same codebase, but each team owns a different part of the system.

### Team 1: Certificate Enrollment Gateway

Focus:

- EST enrollment
- Device activation
- Certificate renewal
- Trust removal
- CA backend abstraction
- Audit trail

Primary deliverable:

> A device cannot receive a certificate until activated, can renew once trusted, and loses enrollment or renewal ability when marked untrusted.

### Team 2: DICOM Transport

Focus:

- Simulated Device A and Device B
- DICOM file send and receive
- Transfer success and failure behavior
- Optional DIMSE support
- Optional DICOMweb bridge

Primary deliverable:

> Two endpoints can transfer a DICOM object, and the transfer can be made to fail when certificate trust is missing or removed.

### Team 3: Integration and Demo

Focus:

- Run scripts
- Docker or local setup
- Sample DICOM files
- Admin workflow
- Demo script
- Logs and screenshots
- Final acceptance testing
- Documentation

Primary deliverable:

> The whole demo can be started, explained, observed, and repeated.

This team should be flexible. If the setup and demo tasks are light, the Integration and Demo team should help with the Certificate Enrollment Gateway or DICOM Transport work as needed. In practice, this team may become the integration glue: writing test clients, wiring scripts, fixing configuration, helping with TLS certificates, or building the final acceptance-test runner.

### Suggested Split

With eight programmers:

- 3 programmers on Certificate Enrollment Gateway
- 3 programmers on DICOM Transport
- 2 programmers on Integration and Demo

Non-programmers can join the Integration and Demo team by default, but they can also help any team by testing workflows, writing user-facing notes, and checking whether the demo story makes sense.

### Best Fit

This option is best when the group wants one combined deliverable and coordinated progress.

### Main Risk

The main risk is integration conflict. The teams should agree early on the API boundaries, command-line contracts, file locations, and acceptance criteria.

## Option 2: One Big Team, Prioritized Goals

Everyone works as one team with clear owners, but the work is not split into the three main subteams.

### Recommended Goal Order

1. Run the gateway locally.
2. Create two small simulated devices.
3. Reject certificate enrollment before activation.
4. Activate Device A and Device B.
5. Issue certificates.
6. Transfer one DICOM file from Device A to Device B.
7. Renew certificates.
8. Transfer back from Device B to Device A.
9. Mark one device untrusted.
10. Show renewal or transfer failure.
11. Add audit and demo evidence.
12. Stretch: DIMSE-to-DICOMweb bridge.
13. Stretch: three-computer network demo.

### Suggested Roles

- Repo and integration captain
- Gateway/API owner
- Admin UI/API owner
- EST client/device simulation owner
- DICOM sender owner
- DICOM receiver owner
- Test/demo script owner
- Documentation/evidence owner

These are ownership roles, not isolated teams. People should help wherever the active blocker is.

### Best Fit

This option is best when the group wants maximum coordination and one strong demo.

### Main Risk

The main risk is that too many people wait on the same blockers. The team needs one person actively managing the task queue and reassigning people when work stalls.

## Option 3: Two Competing Teams, Same Challenge

Two teams independently build the best solution to the same acceptance criteria. Each team can choose its own architecture, libraries, UI, and implementation approach.

### Shared Acceptance Criteria

Both teams must prove:

1. Unknown device cannot get a certificate.
2. Admin can activate a device.
3. Activated device can enroll.
4. Device can renew its certificate.
5. Two trusted endpoints can transfer a DICOM object.
6. Admin can remove trust.
7. Transfer fails after trust removal.
8. The design can support different hospital CA backends.

### Winning Criteria

The strongest result is the first working system that meets all acceptance criteria and can be explained clearly.

Judging should consider:

- Working demo
- Clear architecture
- Evidence through logs, UI, or command output
- Minimal mocked behavior
- Practical path to multiple hospital CA backends
- Practical path to real DICOM transport

### Suggested Split

With eight programmers:

- 4 programmers on Team A
- 4 programmers on Team B

Non-programmers can act as reviewers, testers, demo judges, and documentation support. They should apply the same acceptance criteria to both teams.

### Best Fit

This option is best when the group wants creativity, speed, and competing design ideas.

### Main Risk

The main risk is duplicate work. The group may end the hackathon with two partial systems instead of one polished combined system.

## Option 4: Two Teams, Two Different Projects

Two teams work on separate but related projects. The final showcase can present both, and the stretch goal is to combine them.

### Team 1: DICOM Certificate Gateway

Focus:

- Certificate Enrollment Gateway
- EST enrollment
- Device activation
- Certificate renewal
- Trust removal
- CA backend abstraction
- Audit trail
- Cert-controlled DICOM transfer if time allows

Primary deliverable:

> A hospital can control whether devices receive and renew certificates, and that trust state can affect whether DICOM transfer is allowed.

### Team 2: DICOM to DICOMweb Network Transfer

Focus:

- Legacy DIMSE sender
- Bridge from DIMSE to DICOMweb/HTTPS
- Bridge from DICOMweb/HTTPS back to DIMSE
- Legacy DIMSE receiver
- TLS-secured HTTP transport
- Optional mTLS

Primary deliverable:

> A DICOM object can move from one legacy DIMSE endpoint to another through a secure HTTP/DICOMweb bridge.

### Combined Stretch Goal

The top honor result is to combine the two:

1. Bridge A and Bridge B enroll through the Certificate Enrollment Gateway.
2. Bridge certificates are issued and renewed through EST.
3. The bridge-to-bridge DICOMweb tunnel uses TLS or mTLS.
4. Admin trust removal prevents the tunnel from continuing.
5. The system demonstrates secure modernization of legacy DICOM transfer.

### Suggested Split

With eight programmers:

- 4 programmers on DICOM Certificate Gateway
- 4 programmers on DICOM to DICOMweb Network Transfer

Non-programmers can split across both teams or remain centralized as a demo/review group.

### Best Fit

This option is best when the group wants both major ideas explored without forcing early integration.

### Main Risk

The main risk is that the two outputs may not connect. If this option is chosen, assign one person from each team to meet periodically and define how the two systems would integrate.

## Non-Programmer Roles

Non-programmers can provide real value, especially because this project needs to be understandable to clinical, IT, security, and vendor audiences.

Useful roles:

- Demo narrator
- Acceptance criteria tracker
- Hospital administrator persona tester
- Device manufacturer persona tester
- Screenshot and evidence collector
- Diagram creator
- Setup documentation writer
- Security question reviewer
- Final presentation editor

The non-programmer group should be empowered to ask simple but important questions:

- What is the device doing?
- What is the hospital administrator doing?
- Why was the device rejected?
- How do we know the certificate changed?
- How do we know the DICOM file moved?
- How do we know trust removal caused the failure?
- What was mocked?
- What would need to be real in production?

## Recommended Default

If the group wants the best chance of a working combined demo, use **Option 1: Three Teams, One Shared Project**.

If the group wants the most creative exploration, use **Option 3: Two Competing Teams, Same Challenge**.

If the group wants to explore both major technical ideas without forcing them together too early, use **Option 4: Two Teams, Two Different Projects**.

If the group is small, uncertain, or short on time, use **Option 2: One Big Team, Prioritized Goals**.

