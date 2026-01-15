# TODO: RTSec.Kryptonian CI Pipeline

## Critical Path (Blocks PR #4 Merge)

### ☐ Fix Remaining 32 Code Quality Issues
**Status**: IN PROGRESS (HANDOFF REQUIRED)  
**Priority**: HIGH  
**Reference**: HANDOFF.md

**Progress**:
- ✅ Domain layer: 30 issues fixed in 10 files (commit 2216052)
- ⚠️ Application layer: 32 issues remaining in 6 files
- ⏳ Context: 23% (AWARENESS) - handoff needed

**Completed Domain fixes** (examples from commit 2216052):
```csharp
// CA1027: Add [Flags] to enums
[Flags]
public enum RevocationReason { ... }

// CA1032: Add exception constructors
public AcmeChallengeException() { }
public AcmeChallengeException(string message) : base(message) { }
public AcmeChallengeException(string message, Exception inner) : base(message, inner) { }

// CA1056: Use Uri instead of string for URLs
public Uri DirectoryUrl { get; set; }  // was: string

// CA2227: Make collections read-only
public Collection<string> Hostnames { get; }  // was: { get; set; }

// CA1819: Use IReadOnlyList instead of arrays
public IReadOnlyList<X509Certificate2>? CertificateChain { get; init; }  // was: X509Certificate2[]

// CA1308: Use ToUpperInvariant
hostname = hostname.Trim().ToUpperInvariant();  // was: ToLowerInvariant
```

**Remaining Application layer issues** (32 errors):
```
EstProfileDto.cs:     CA2227 (8), CA1819 (4)  = 12 errors
CaBackendDto.cs:      CA2227 (3)              = 3 errors
CaBackendService.cs:  CA1062 (1)              = 1 error
EnrollmentOrchestrator.cs: CA1062 (1)         = 1 error
EstProfileService.cs: CA1062 (1)              = 1 error
DependencyInjection.cs: CA1724 (1)            = 1 error
```

**To see all errors**:
```bash
dotnet build --no-restore -p:AnalysisLevel=latest-all
```

**To fix remaining issues**:
1. Follow patterns from Domain layer fixes (commit 2216052)
2. Use `git add -u` to stage modified files
3. Commit with message: "fix: Resolve remaining Application layer issues"
4. Run `./build.sh ci-local` to verify

**Estimated effort**: 1-2 hours  
**Context**: 23% - handoff to another developer recommended

---

### ☐ Re-enable Strict Mode
**Status**: PENDING  
**Priority**: HIGH  
**Depends on**: Fixing all 30 issues

**Files to update**:
- [ ] `build.sh`: Add `-p:TreatWarningsAsErrors=true`
- [ ] `build.ps1`: Add `-p:TreatWarningsAsErrors=true`
- [ ] `Makefile`: Add `-p:TreatWarningsAsErrors=true`
- [ ] `.github/workflows/ci.yml`: Add `-p:TreatWarningsAsErrors=true`

**Remove TODO comments** from all files

---

### ☐ Verify CI Passes
**Status**: PENDING  
**Priority**: HIGH  
**Depends on**: Above tasks

**Steps**:
1. Push changes to `ci/multi-platform-pipeline`
2. Check GitHub Actions tab
3. All 3 platforms should show green checkmarks
4. Coverage reports should be generated
5. PR should be mergeable

---

## Post-Merge Tasks

### ☐ Merge PR #4
**Status**: WAITING  
**Priority**: HIGH  
**Depends on**: CI passing

**Steps**:
1. Click "Merge pull request" on GitHub
2. Use merge commit message
3. Verify `main` branch CI runs
4. Delete `ci/multi-platform-pipeline` branch

---

### ☐ Merge PR #3 (Timezone Fix)
**Status**: WAITING  
**Priority**: HIGH  
**Depends on**: PR #4 merged

**Steps**:
1. Rebase `fix/timezone-bug-selfsigned-connector` on `main`
2. Push force
3. Wait for CI to pass
4. Merge

---

### ☐ Enable Branch Protection
**Status**: NOT STARTED  
**Priority**: MEDIUM

**Settings**:
- Require CI to pass before merge
- Require PR reviews
- Require status checks:
  - `build-and-test (windows-latest)`
  - `build-and-test (ubuntu-latest)`
  - `build-and-test (macos-latest)`
  - `quality-gate`

---

### ☐ Set Coverage Thresholds
**Status**: NOT STARTED  
**Priority**: MEDIUM

**Decision needed**: What's a realistic threshold?
- Current: 0% (disabled)
- Suggested: 60-70% initially, 80% eventually

**Files to update**:
- `.github/workflows/ci.yml`: `fail_below_min: true`, `thresholds: '70 80'`
- `build.sh`: Uncomment threshold lines
- `build.ps1`: Uncomment threshold lines
- `Makefile`: Uncomment threshold lines

---

## Documentation Tasks

### ☐ Update BUILD.md
**Status**: NOT STARTED  
**Priority**: LOW

**Add**:
- Real examples from actual usage
- Troubleshooting section
- Coverage report viewing instructions

---

### ☐ Create CODEOWNERS
**Status**: NOT STARTED  
**Priority**: LOW

**Suggested**:
```
/src/RTSec.Kryptonian.Infrastructure/Crypto/ @stuart
/src/RTSec.Kryptonian.Domain/ @cora
/tests/ @clement
.github/workflows/ @stuart
```

---

### ☐ Add Integration Tests
**Status**: NOT STARTED  
**Priority**: LOW

**Target**: RTSec.Kryptonian.Api.Tests
- End-to-end API tests
- Certificate issuance flow
- Error handling

---

## Quality Improvements

### ☐ Security Audit
**Status**: NOT STARTED  
**Priority**: MEDIUM

**Focus**:
- CA2100: SQL injection checks
- CA2000: Dispose patterns
- CA1062: Null validation

---

### ☐ Performance Benchmarks
**Status**: NOT STARTED  
**Priority**: LOW

**Target**:
- HostnameMatcher
- PkcsService
- AcmeConnector

---

### ☐ Refactoring
**Status**: NOT STARTED  
**Priority**: LOW

**Candidates**:
- Use `Uri` type consistently
- Make collections immutable
- Add proper exception constructors

---

## Tracking

### Issues Fixed
- [x] Formatting (21 files)
- [x] Build system (3 scripts + Makefile)
- [x] Documentation (BUILD.md, HANDOFF.md)

### Issues Remaining
- [ ] 30 code quality issues
- [ ] CI merge
- [ ] Branch protection
- [ ] Coverage thresholds

---

## Quick Reference

### To see what needs fixing:
```bash
dotnet build --no-restore -p:AnalysisLevel=latest-all
```

### To fix formatting:
```bash
dotnet format
```

### To test locally:
```bash
./build.sh ci-local
```

### To check CI status:
```bash
gh pr checks 4  # PR #4 status
```

---

**Last updated**: 2026-01-15  
**Next action**: Fix the 30 code quality issues  
**Context**: 28% (AWARENESS threshold)