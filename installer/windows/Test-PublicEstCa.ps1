# Exercise only the export helper; never start services or touch Public Documents.
$ErrorActionPreference = 'Stop'
$tokens = $null; $errors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot 'Initialize-Sandbox.ps1'), [ref]$tokens, [ref]$errors)
if ($errors.Count) { throw $errors }
$function = $ast.Find({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Export-PublicEstCa' }, $true)
. ([scriptblock]::Create($function.Extent.Text))
$testDirectory = [IO.Directory]::CreateTempSubdirectory('kryptonian-public-ca-')
try {
    $key = [Security.Cryptography.RSA]::Create(2048)
    try {
        $request = [Security.Cryptography.X509Certificates.CertificateRequest]::new('CN=Export test CA', $key, [Security.Cryptography.HashAlgorithmName]::SHA256, [Security.Cryptography.RSASignaturePadding]::Pkcs1)
        $ca = $request.CreateSelfSigned([DateTimeOffset]::UtcNow.AddMinutes(-1), [DateTimeOffset]::UtcNow.AddDays(1))
        try {
            $source = Join-Path $testDirectory.FullName 'ca.pem'
            $target = Join-Path $testDirectory.FullName 'Documents/Kryptonian-EST-CA.crt'
            [IO.File]::WriteAllText($source, $ca.ExportCertificatePem())
            Export-PublicEstCa $source $target
            Export-PublicEstCa $source $target # repair/upgrade replacement
            $copy = [Security.Cryptography.X509Certificates.X509Certificate2]::CreateFromPem([IO.File]::ReadAllText($target))
            try { if ($copy.HasPrivateKey -or $copy.Thumbprint -ne $ca.Thumbprint) { throw 'Wrong certificate or private key exported.' } }
            finally { $copy.Dispose() }
            if (-not (Get-Acl -LiteralPath $target).AreAccessRulesProtected) { throw 'Public file ACL not protected.' }
            if (Get-ChildItem $testDirectory.FullName -Recurse -Filter '*.tmp') { throw 'Temporary file leaked.' }
        } finally { $ca.Dispose() }
    } finally { $key.Dispose() }
    Write-Host 'PASS: public certificate export, repeat replacement, protected ACL, no private key or temporary files.'
} finally { $testDirectory.Delete($true) }
