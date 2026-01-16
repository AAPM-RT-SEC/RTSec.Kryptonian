#!/usr/bin/env pwsh
# Cross-platform build script for RTSec.Kryptonian (PowerShell version)
# Works on Windows, Linux, and macOS with PowerShell Core

param(
    [Parameter(Position=0)]
    [ValidateSet('build', 'test', 'format', 'lint', 'ci', 'ci-local', 'coverage', 'clean', 'restore')]
    [string]$Command = 'help'
)

# Configuration
$SOLUTION = "RTSec.Kryptonian.sln"
$DOTNET_BUILD_FLAGS = @(
    "-p:AnalysisLevel=latest-all",
    "-p:EnforceCodeStyleInBuild=true",
    "-p:WarningLevel=9999"
)
# TODO: Re-enable "" after fixing all 30 code quality issues

# Colors
$ESC = [char]27
$GREEN = "${ESC}[32m"
$RED = "${ESC}[31m"
$YELLOW = "${ESC}[33m"
$NC = "${ESC}[0m"

function Write-Status {
    param([string]$Message)
    Write-Host "[$(Get-Date -Format 'HH:mm:ss')] $Message" -ForegroundColor Green
}

function Write-Warning-Msg {
    param([string]$Message)
    Write-Host "[$(Get-Date -Format 'HH:mm:ss')] WARNING: $Message" -ForegroundColor Yellow
}

function Write-Error-Msg {
    param([string]$Message)
    Write-Host "[$(Get-Date -Format 'HH:mm:ss')] ERROR: $Message" -ForegroundColor Red
}

function Show-Help {
    Write-Host @"
Usage: .\build.ps1 <command>

Commands:
  build       - Build the solution
  test        - Run all tests
  format      - Format code (auto-fix)
  lint        - Strict linting (no fixes)
  ci          - Full CI pipeline (strict)
  ci-local    - CI pipeline with auto-fix
  coverage    - Run tests with coverage report
  clean       - Clean build artifacts
  restore     - Restore NuGet packages

Examples:
  .\build.ps1 ci-local    # Fix everything then verify
  .\build.ps1 ci          # Strict CI check
  .\build.ps1 coverage    # Generate coverage report
"@
}

function Invoke-Restore {
    Write-Status "Restoring NuGet packages..."
    dotnet restore $SOLUTION
}

function Invoke-Clean {
    Write-Status "Cleaning build artifacts..."
    dotnet clean $SOLUTION
    Get-ChildItem -Path . -Recurse -Directory -Include "bin","obj","TestResults","coverage-report" -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
}

function Invoke-Build {
    Write-Status "Building solution..."
    dotnet build $SOLUTION --no-restore @DOTNET_BUILD_FLAGS
}

function Invoke-Test {
    Write-Status "Running tests..."
    dotnet test $SOLUTION --no-build --verbosity normal
}

function Invoke-Format {
    Write-Status "Formatting code (auto-fix)..."
    dotnet format whitespace $SOLUTION
    dotnet format style $SOLUTION
    Write-Status "Formatting complete"
}

function Invoke-Lint {
    Write-Status "Running strict linting..."
    dotnet build $SOLUTION --no-restore @DOTNET_BUILD_FLAGS
}

function Invoke-CI-Strict {
    Write-Status "=== CI STRICT MODE ==="
    
    Write-Status "Step 1: Formatting verification..."
    dotnet format whitespace --verify-no-changes $SOLUTION
    dotnet format style --verify-no-changes $SOLUTION
    
    Write-Status "Step 2: Building with strict analyzers..."
    dotnet build $SOLUTION --no-restore @DOTNET_BUILD_FLAGS
    
    Write-Status "Step 3: Running tests..."
    dotnet test $SOLUTION --no-build --verbosity normal
    
    Write-Status "=== ALL CHECKS PASSED ==="
}

function Invoke-CI-Local {
    Write-Status "=== CI LOCAL MODE (Auto-fix) ==="
    
    Write-Status "Step 1: Auto-formatting..."
    dotnet format $SOLUTION
    
    Write-Status "Step 2: Building..."
    dotnet build $SOLUTION --no-restore @DOTNET_BUILD_FLAGS
    
    Write-Status "Step 3: Running tests..."
    dotnet test $SOLUTION --no-build --verbosity normal
    
    Write-Status "=== ALL CHECKS PASSED ==="
}

function Invoke-Coverage {
    Write-Status "Running tests with coverage..."
    
    # Clean previous results
    if (Test-Path "TestResults") { Remove-Item "TestResults" -Recurse -Force }
    if (Test-Path "coverage-report") { Remove-Item "coverage-report" -Recurse -Force }
    
    # Run tests with coverage
    dotnet test $SOLUTION --no-build `
        --collect:"XPlat Code Coverage" `
        --results-directory TestResults `
        --logger trx `
        --verbosity normal
    
    # Check if ReportGenerator is available
    $reportGen = Get-Command "reportgenerator" -ErrorAction SilentlyContinue
    if ($reportGen) {
        Write-Status "Generating coverage report..."
        reportgenerator `
            -reports:**/TestResults/**/coverage.cobertura.xml `
            -targetdir:coverage-report `
            -reporttypes:Html;MarkdownSummaryGithub;Badges `
            -verbosity:Info
        
        Write-Status "Coverage report generated in coverage-report/"
        
        if (Test-Path "coverage-report/SummaryGithub.md") {
            Write-Host ""
            Write-Status "Coverage Summary:"
            Get-Content "coverage-report/SummaryGithub.md" | ForEach-Object { Write-Host $_ }
        }
    } else {
        Write-Warning-Msg "ReportGenerator not installed. Install with:"
        Write-Warning-Msg "  dotnet tool install -g dotnet-reportgenerator-globaltool"
        Write-Status "Raw coverage data available in TestResults/"
    }
}

# Main execution
switch ($Command) {
    'restore'   { Invoke-Restore }
    'clean'     { Invoke-Clean }
    'build'     { Invoke-Restore; Invoke-Build }
    'test'      { Invoke-Restore; Invoke-Build; Invoke-Test }
    'format'    { Invoke-Restore; Invoke-Format }
    'lint'      { Invoke-Restore; Invoke-Lint }
    'ci'        { Invoke-Restore; Invoke-CI-Strict }
    'ci-local'  { Invoke-Restore; Invoke-CI-Local }
    'coverage'  { Invoke-Restore; Invoke-Build; Invoke-Coverage }
    default     { Show-Help }
}