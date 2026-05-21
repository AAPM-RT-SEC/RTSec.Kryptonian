#requires -Version 7
# Repro for WPF Renew 401. Loads PFX, builds new CSR with same CN, mTLS POST to /simplereenroll.
param(
    [string]$Gateway = 'https://kryptonian-gateway.mangotree-b3d09362.eastus.azurecontainerapps.io',
    [string]$PfxPath = 'C:\Users\rexca\OneDrive\Desktop\CN-MyLinac.pfx',
    [string]$PfxPassword = ''
)

$ErrorActionPreference = 'Stop'

Write-Host "Loading $PfxPath..."
$bytes = [IO.File]::ReadAllBytes($PfxPath)
$flags = [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::Exportable
$cert = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($bytes, $PfxPassword, $flags)
Write-Host "  Subject:     $($cert.Subject)"
Write-Host "  Thumbprint:  $($cert.Thumbprint)"
Write-Host "  NotAfter:    $($cert.NotAfter)"
Write-Host "  HasPrivKey:  $($cert.HasPrivateKey)"

$cn = ($cert.Subject -split ',' | Where-Object { $_.Trim().StartsWith('CN=', 'OrdinalIgnoreCase') } | Select-Object -First 1).Trim().Substring(3)
Write-Host "  CN:          $cn"

Write-Host "`nGenerating new RSA-4096 key + CSR for $cn..."
$newRsa = [System.Security.Cryptography.RSA]::Create(4096)
$subject = [System.Security.Cryptography.X509Certificates.X500DistinguishedName]::new("CN=$cn")
$req = [System.Security.Cryptography.X509Certificates.CertificateRequest]::new(
    $subject,
    $newRsa,
    [System.Security.Cryptography.HashAlgorithmName]::SHA256,
    [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)
$csrDer = $req.CreateSigningRequest()
$csrB64 = [Convert]::ToBase64String($csrDer)
Write-Host "  CSR bytes:   $($csrDer.Length)  (base64 len $($csrB64.Length))"

Write-Host "`nPOST $Gateway/.well-known/est/simplereenroll  (mTLS)"
$handler = [System.Net.Http.HttpClientHandler]::new()
$handler.ClientCertificateOptions = [System.Net.Http.ClientCertificateOption]::Manual
$null = $handler.ClientCertificates.Add($cert)
$http = [System.Net.Http.HttpClient]::new($handler)

$content = [System.Net.Http.StringContent]::new($csrB64, [System.Text.Encoding]::ASCII)
$content.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::new('application/pkcs10')
$content.Headers.Add('Content-Transfer-Encoding', 'base64')

$request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Post, "$Gateway/.well-known/est/simplereenroll")
$request.Content = $content

try {
    $response = $http.SendAsync($request).GetAwaiter().GetResult()
    $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    Write-Host ""
    Write-Host "Status: $([int]$response.StatusCode) $($response.StatusCode)"
    Write-Host "Body (first 600):"
    Write-Host ($body.Substring(0, [Math]::Min(600, $body.Length)))
} catch {
    Write-Host "Request failed: $($_.Exception.Message)"
    if ($_.Exception.InnerException) {
        Write-Host "  Inner: $($_.Exception.InnerException.Message)"
    }
}
