# RTSec.Kryptonian Build System

Cross-platform build solutions for RTSec.Kryptonian development.

## Quick Start

### **Linux / macOS**
```bash
# Make executable
chmod +x build.sh

# Run CI checks (strict)
./build.sh ci

# Fix everything locally
./build.sh ci-local

# Generate coverage report
./build.sh coverage
```

### **Windows (PowerShell)**
```powershell
# Run CI checks (strict)
.\build.ps1 ci

# Fix everything locally
.\build.ps1 ci-local

# Generate coverage report
.\build.ps1 coverage
```

### **Windows (Git Bash / WSL)**
```bash
# Use the bash script
./build.sh ci
```

### **Linux/macOS (Make)**
```bash
# Run CI checks (strict)
make ci

# Fix everything locally
make ci-local

# Generate coverage report
make coverage
```

## Platform-Specific Solutions

| Platform | Recommended Tool | Alternative |
|----------|-----------------|-------------|
| **Linux** | `make` or `build.sh` | `build.sh` |
| **macOS** | `make` or `build.sh` | `build.sh` |
| **Windows (PowerShell)** | `build.ps1` | `build.sh` (Git Bash) |
| **Windows (Git Bash)** | `build.sh` | `build.ps1` |
| **Windows (CMD)** | `build.ps1` | - |

## Available Commands

All tools support the same commands:

### **Development Workflow**
```bash
# 1. Restore packages
./build.sh restore

# 2. Build
./build.sh build

# 3. Run tests
./build.sh test

# 4. Format code (auto-fix)
./build.sh format

# 5. Strict linting
./build.sh lint
```

### **CI/CD Workflow**
```bash
# Strict CI check (no fixes, fails on issues)
./build.sh ci

# Local CI with auto-fix (recommended before commit)
./build.sh ci-local
```

### **Quality Analysis**
```bash
# Generate coverage report
./build.sh coverage

# Clean build artifacts
./build.sh clean
```

## What Each Tool Does

### **build.sh** (Bash)
- ✅ Works on Linux, macOS, Windows (Git Bash/WSL)
- ✅ No dependencies
- ✅ Color output
- ✅ Platform detection
- ✅ Exit codes for CI

### **build.ps1** (PowerShell)
- ✅ Works on Windows, Linux, macOS (PowerShell Core)
- ✅ Native Windows integration
- ✅ Color output
- ✅ Full error handling

### **Makefile** (Make)
- ✅ Native to Linux/macOS
- ✅ Fast incremental builds
- ✅ Standard for Unix systems
- ❌ Not available on Windows without WSL/Git Bash

## CI Integration

The GitHub Actions workflow uses these commands:

```yaml
# .github/workflows/ci.yml
- name: Format check
  run: ./build.sh ci  # Or make ci
  
- name: Build
  run: ./build.sh build
  
- name: Test
  run: ./build.sh test
```

## Common Workflows

### **Before Committing**
```bash
# Fix everything, then verify
./build.sh ci-local
git add .
git commit -m "..."
```

### **After Pulling Changes**
```bash
# Restore and verify
./build.sh restore
./build.sh ci
```

### **Debugging Test Failures**
```bash
# Build with verbose output
./build.sh build 2>&1 | tee build.log

# Run specific test
dotnet test --filter "FullyQualifiedName~SelfSignedCaConnector"
```

### **Coverage Analysis**
```bash
# Generate and view coverage
./build.sh coverage
open coverage-report/index.html  # Linux: xdg-open, Windows: start
```

## Platform-Specific Notes

### **Windows**
- **PowerShell**: Run in PowerShell 7+ for best experience
- **Git Bash**: Use `./build.sh` (works perfectly)
- **CMD**: Use `.\build.ps1` (requires PowerShell)

### **macOS**
- **Terminal**: All tools work natively
- **Make**: Pre-installed
- **PowerShell**: Install via `brew install powershell`

### **Linux**
- **All distros**: `build.sh` works everywhere
- **Make**: Standard on most distros
- **PowerShell**: Install via package manager

## Troubleshooting

### **"command not found"**
```bash
# Make executable
chmod +x build.sh

# Or use bash explicitly
bash build.sh ci
```

### **"dotnet: command not found"**
- Install .NET 8.0 SDK from https://dotnet.microsoft.com/download

### **Formatting errors in CI**
```bash
# Fix locally
./build.sh format

# Or let CI show errors, then fix manually
dotnet format whitespace --verify-no-changes
dotnet format style --verify-no-changes
```

### **Coverage report not generating**
```bash
# Install ReportGenerator
dotnet tool install -g dotnet-reportgenerator-globaltool

# Then run coverage again
./build.sh coverage
```

## Summary

**For team members:**
- **Linux/macOS**: Use `make ci-local` or `./build.sh ci-local`
- **Windows**: Use `.\build.ps1 ci-local` or `./build.sh ci-local` (Git Bash)
- **Before commit**: Always run `ci-local` to auto-fix issues

**For CI/CD:**
- Use `./build.sh ci` (strict, no fixes)
- Or `make ci` on Unix systems

All three tools provide the same functionality - choose based on your platform and preference.