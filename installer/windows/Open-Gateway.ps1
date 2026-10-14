# Runs as the interactive user, never as an MSI SYSTEM custom action.
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
try {
    $certificatePath = Join-Path $env:ProgramData 'KryptonianGatewayPublic/admin-ca.pem'
    if (-not (Test-Path -LiteralPath $certificatePath)) { throw 'Gateway setup is incomplete. Run the MSI again and choose Repair.' }
    $ca = [Security.Cryptography.X509Certificates.X509Certificate2]::CreateFromPem([IO.File]::ReadAllText($certificatePath))
    try {
        $store = [Security.Cryptography.X509Certificates.X509Store]::new('Root', 'CurrentUser')
        $store.Open('ReadOnly')
        try { $trusted = $store.Certificates.Find('FindByThumbprint', $ca.Thumbprint, $false).Count -gt 0 } finally { $store.Close() }
        if (-not $trusted) {
            $fingerprint = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($ca.RawData))
            $message = "Trust this installation's local administration CA for your Windows account?`n`n$($ca.Subject)`nSHA256: $fingerprint`n`nThis allows the HTTPS dashboard to open without a warning. The device-issuing CA is NOT installed as trusted. Select No to leave trust unchanged."
            if ([Windows.Forms.MessageBox]::Show($message, 'Kryptonian Gateway: browser trust', 'YesNo', 'Question') -ne 'Yes') { exit 0 }
            Import-Certificate -FilePath $certificatePath -CertStoreLocation Cert:\CurrentUser\Root | Out-Null
        }
    } finally { $ca.Dispose() }
    Start-Process 'https://localhost:7445/'
} catch {
    [void][Windows.Forms.MessageBox]::Show($_.Exception.Message, 'Kryptonian Gateway could not open', 'OK', 'Error')
    exit 1
}
