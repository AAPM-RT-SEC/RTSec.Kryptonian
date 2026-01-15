# TODO: RTSec.Kryptonian CI Pipeline

## Critical Path (Blocks PR #4 Merge)

### ☐ Fix 30 Code Quality Issues
**Status**: BLOCKED  
**Priority**: HIGH  
**Reference**: HANDOFF.md

**Files to fix**:
```
src/RTSec.Kryptonian.Domain/Enums/RevocationReason.cs
  - CA1027: Add [Flags] attribute

src/RTSec.Kryptonian.Domain/ValueObjects/
  - CA1819: Change array properties to collections (3 files)

src/RTSec.Kryptonian.Domain/Entities/
  - CA1056: Change string URLs to Uri (3 files)
  - CA2227: Make properties read-only (6 files)
  - CA1002: Change List<T> to Collection<T> (4 files)
  - CA1032: Add exception constructors (1 file)
  - CA1805: Remove redundant initialization (1 file)

src/RTSec.Kryptonian.Domain/Interfaces/IRepository.cs
  - CA1054: Change string directoryUrl to Uri (2 methods)

src/RTSec.Kryptonian.Domain/Services/HostnameMatcher.cs
  - CA1062: Add null validation (1 method)
  - CA1307: Add StringComparison parameter (1 method)
  - CA1308: Change ToLowerInvariant to ToUpperInvariant (5 occurrences)
```

**Command to see all issues**:
```bash
dotnet build --no-restore -p:AnalysisLevel=latest-all
```

**Estimated effort**: 2-4 hours

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