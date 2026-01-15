#!/bin/bash
# Cross-platform build script for RTSec.Kryptonian
# Works on Linux, macOS, and Windows (via WSL/Git Bash)

set -e  # Exit on error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Configuration
SOLUTION="RTSec.Kryptonian.sln"
DOTNET_BUILD_FLAGS="-p:AnalysisLevel=latest-all -p:EnforceCodeStyleInBuild=true -p:WarningLevel=9999"
# TODO: Re-enable  after fixing all 30 code quality issues

# Function to print colored output
print_status() {
    echo -e "${GREEN}[$(date +'%H:%M:%S')] $1${NC}"
}

print_warning() {
    echo -e "${YELLOW}[$(date +'%H:%M:%S')] WARNING: $1${NC}"
}

print_error() {
    echo -e "${RED}[$(date +'%H:%M:%S')] ERROR: $1${NC}"
}

# Detect platform
PLATFORM="unknown"
case "$(uname -s)" in
    Linux*)     PLATFORM="linux" ;;
    Darwin*)    PLATFORM="macos" ;;
    CYGWIN*|MINGW*|MSYS*) PLATFORM="windows" ;;
esac

print_status "Detected platform: $PLATFORM"

# Usage function
usage() {
    echo "Usage: $0 <command>"
    echo ""
    echo "Commands:"
    echo "  build       - Build the solution"
    echo "  test        - Run all tests"
    echo "  format      - Format code (auto-fix)"
    echo "  lint        - Strict linting (no fixes)"
    echo "  ci          - Full CI pipeline (strict)"
    echo "  ci-local    - CI pipeline with auto-fix"
    echo "  coverage    - Run tests with coverage report"
    echo "  clean       - Clean build artifacts"
    echo "  restore     - Restore NuGet packages"
    echo ""
    echo "Examples:"
    echo "  ./build.sh ci-local    # Fix everything then verify"
    echo "  ./build.sh ci          # Strict CI check"
    echo "  ./build.sh coverage    # Generate coverage report"
    exit 1
}

# Command implementations
cmd_restore() {
    print_status "Restoring NuGet packages..."
    dotnet restore $SOLUTION
}

cmd_clean() {
    print_status "Cleaning build artifacts..."
    dotnet clean $SOLUTION
    rm -rf **/bin **/obj **/TestResults **/coverage-report 2>/dev/null || true
}

cmd_build() {
    print_status "Building solution..."
    dotnet build $SOLUTION --no-restore $DOTNET_BUILD_FLAGS
}

cmd_test() {
    print_status "Running tests..."
    dotnet test $SOLUTION --no-build --verbosity normal
}

cmd_format() {
    print_status "Formatting code (auto-fix)..."
    dotnet format whitespace $SOLUTION
    dotnet format style $SOLUTION
    print_status "Formatting complete"
}

cmd_lint() {
    print_status "Running strict linting..."
    dotnet build $SOLUTION --no-restore $DOTNET_BUILD_FLAGS
}

cmd_ci_strict() {
    print_status "=== CI STRICT MODE ==="
    print_status "Step 1: Formatting verification..."
    dotnet format whitespace --verify-no-changes $SOLUTION
    dotnet format style --verify-no-changes $SOLUTION
    
    print_status "Step 2: Building with strict analyzers..."
    dotnet build $SOLUTION --no-restore $DOTNET_BUILD_FLAGS
    
    print_status "Step 3: Running tests..."
    dotnet test $SOLUTION --no-build --verbosity normal
    
    print_status "=== ALL CHECKS PASSED ==="
}

cmd_ci_local() {
    print_status "=== CI LOCAL MODE (Auto-fix) ==="
    print_status "Step 1: Auto-formatting..."
    dotnet format $SOLUTION
    
    print_status "Step 2: Building..."
    dotnet build $SOLUTION --no-restore $DOTNET_BUILD_FLAGS
    
    print_status "Step 3: Running tests..."
    dotnet test $SOLUTION --no-build --verbosity normal
    
    print_status "=== ALL CHECKS PASSED ==="
}

cmd_coverage() {
    print_status "Running tests with coverage..."
    
    # Clean previous results
    rm -rf TestResults coverage-report 2>/dev/null || true
    
    # Run tests with coverage
    dotnet test $SOLUTION --no-build \
        --collect:"XPlat Code Coverage" \
        --results-directory TestResults \
        --logger trx \
        --verbosity normal
    
    # Generate report if ReportGenerator is available
    if command -v reportgenerator &> /dev/null; then
        print_status "Generating coverage report..."
        reportgenerator \
            -reports:**/TestResults/**/coverage.cobertura.xml \
            -targetdir:coverage-report \
            -reporttypes:Html;MarkdownSummaryGithub;Badges \
            -verbosity:Info
        
        print_status "Coverage report generated in coverage-report/"
        
        # Show summary
        if [ -f "coverage-report/SummaryGithub.md" ]; then
            echo ""
            print_status "Coverage Summary:"
            cat coverage-report/SummaryGithub.md
        fi
    else
        print_warning "ReportGenerator not installed. Install with:"
        print_warning "  dotnet tool install -g dotnet-reportgenerator-globaltool"
        print_status "Raw coverage data available in TestResults/"
    fi
}

# Main command dispatcher
case "$1" in
    restore)  cmd_restore ;;
    clean)    cmd_clean ;;
    build)    cmd_restore && cmd_build ;;
    test)     cmd_restore && cmd_build && cmd_test ;;
    format)   cmd_restore && cmd_format ;;
    lint)     cmd_restore && cmd_lint ;;
    ci)       cmd_restore && cmd_ci_strict ;;
    ci-local) cmd_restore && cmd_ci_local ;;
    coverage) cmd_restore && cmd_build && cmd_coverage ;;
    *)
        usage
        ;;
esac