# RTSec.Kryptonian Cross-Platform Build System
# For Linux and macOS (use build.ps1 on Windows)

SOLUTION = RTSec.Kryptonian.sln
DOTNET_BUILD_FLAGS = -p:AnalysisLevel=latest-all -p:EnforceCodeStyleInBuild=true  -p:WarningLevel=9999

.PHONY: help build test format lint ci ci-local coverage clean restore

# Default target
help:
	@echo "RTSec.Kryptonian Build System"
	@echo ""
	@echo "Usage: make <target>"
	@echo ""
	@echo "Targets:"
	@echo "  build       - Build the solution"
	@echo "  test        - Run all tests"
	@echo "  format      - Format code (auto-fix)"
	@echo "  lint        - Strict linting (no fixes)"
	@echo "  ci          - Full CI pipeline (strict)"
	@echo "  ci-local    - CI pipeline with auto-fix"
	@echo "  coverage    - Run tests with coverage report"
	@echo "  clean       - Clean build artifacts"
	@echo "  restore     - Restore NuGet packages"
	@echo ""
	@echo "Examples:"
	@echo "  make ci-local    # Fix everything then verify"
	@echo "  make ci          # Strict CI check"
	@echo "  make coverage    # Generate coverage report"

restore:
	@echo "[$(shell date +'%H:%M:%S')] Restoring NuGet packages..."
	@dotnet restore $(SOLUTION)

clean:
	@echo "[$(shell date +'%H:%M:%S')] Cleaning build artifacts..."
	@dotnet clean $(SOLUTION)
	@rm -rf **/bin **/obj **/TestResults **/coverage-report 2>/dev/null || true

build: restore
	@echo "[$(shell date +'%H:%M:%S')] Building solution..."
	@dotnet build $(SOLUTION) --no-restore $(DOTNET_BUILD_FLAGS)

test: build
	@echo "[$(shell date +'%H:%M:%S')] Running tests..."
	@dotnet test $(SOLUTION) --no-build --verbosity normal

format: restore
	@echo "[$(shell date +'%H:%M:%S')] Formatting code (auto-fix)..."
	@dotnet format whitespace $(SOLUTION)
	@dotnet format style $(SOLUTION)
	@echo "[$(shell date +'%H:%M:%S')] Formatting complete"

lint: restore
	@echo "[$(shell date +'%H:%M:%S')] Running strict linting..."
	@dotnet build $(SOLUTION) --no-restore $(DOTNET_BUILD_FLAGS)

ci: restore
	@echo "[$(shell date +'%H:%M:%S')] === CI STRICT MODE ==="
	@echo "[$(shell date +'%H:%M:%S')] Step 1: Formatting verification..."
	@dotnet format whitespace --verify-no-changes $(SOLUTION)
	@dotnet format style --verify-no-changes $(SOLUTION)
	@echo "[$(shell date +'%H:%M:%S')] Step 2: Building with strict analyzers..."
	@dotnet build $(SOLUTION) --no-restore $(DOTNET_BUILD_FLAGS)
	@echo "[$(shell date +'%H:%M:%S')] Step 3: Running tests..."
	@dotnet test $(SOLUTION) --no-build --verbosity normal
	@echo "[$(shell date +'%H:%M:%S')] === ALL CHECKS PASSED ==="

ci-local: restore
	@echo "[$(shell date +'%H:%M:%S')] === CI LOCAL MODE (Auto-fix) ==="
	@echo "[$(shell date +'%H:%M:%S')] Step 1: Auto-formatting..."
	@dotnet format $(SOLUTION)
	@echo "[$(shell date +'%H:%M:%S')] Step 2: Building..."
	@dotnet build $(SOLUTION) --no-restore $(DOTNET_BUILD_FLAGS)
	@echo "[$(shell date +'%H:%M:%S')] Step 3: Running tests..."
	@dotnet test $(SOLUTION) --no-build --verbosity normal
	@echo "[$(shell date +'%H:%M:%S')] === ALL CHECKS PASSED ==="

coverage: build
	@echo "[$(shell date +'%H:%M:%S')] Running tests with coverage..."
	@rm -rf TestResults coverage-report 2>/dev/null || true
	@dotnet test $(SOLUTION) --no-build \
		--collect:"XPlat Code Coverage" \
		--results-directory TestResults \
		--logger trx \
		--verbosity normal
	@if command -v reportgenerator >/dev/null 2>&1; then \
		echo "[$(shell date +'%H:%M:%S')] Generating coverage report..."; \
		reportgenerator \
			-reports:**/TestResults/**/coverage.cobertura.xml \
			-targetdir:coverage-report \
			-reporttypes:Html;MarkdownSummaryGithub;Badges \
			-verbosity:Info; \
		echo "[$(shell date +'%H:%M:%S')] Coverage report generated in coverage-report/"; \
		if [ -f "coverage-report/SummaryGithub.md" ]; then \
			echo ""; \
			echo "[$(shell date +'%H:%M:%S')] Coverage Summary:"; \
			cat coverage-report/SummaryGithub.md; \
		fi \
	else \
		echo "[$(shell date +'%H:%M:%S')] WARNING: ReportGenerator not installed. Install with:"; \
		echo "  dotnet tool install -g dotnet-reportgenerator-globaltool"; \
		echo "[$(shell date +'%H:%M:%S')] Raw coverage data available in TestResults/"; \
	fi