param(
    [Parameter(Mandatory = $true)]
    [string]$BackupPath,

    [Parameter(Mandatory = $true)]
    [string]$KeyPath,

    [Parameter(Mandatory = $true)]
    [string]$OutputPath
)

$ErrorActionPreference = "Stop"

function Read-KeyFile {
    param([string]$Path)

    $values = @{}
    foreach ($line in Get-Content -LiteralPath $Path) {
        if ([string]::IsNullOrWhiteSpace($line) -or $line.TrimStart().StartsWith("#")) {
            continue
        }

        $parts = $line.Split("=", 2)
        if ($parts.Length -eq 2) {
            $values[$parts[0]] = $parts[1]
        }
    }

    return $values
}

$keyValues = Read-KeyFile -Path $KeyPath
if (-not $keyValues.ContainsKey("password")) {
    throw "Key file does not contain a password entry."
}

$password = [string]$keyValues["password"]
$backupBytes = [IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $BackupPath))
$magic = [Text.Encoding]::ASCII.GetBytes("PTGDBAES1")

if ($backupBytes.Length -lt ($magic.Length + 16 + 16 + 32)) {
    throw "Backup file is too small to be a valid encrypted PTG database backup."
}

for ($i = 0; $i -lt $magic.Length; $i++) {
    if ($backupBytes[$i] -ne $magic[$i]) {
        throw "Invalid backup magic header."
    }
}

$salt = New-Object byte[] 16
$iv = New-Object byte[] 16
$tag = New-Object byte[] 32
$cipherLength = $backupBytes.Length - $magic.Length - $salt.Length - $iv.Length - $tag.Length
$cipher = New-Object byte[] $cipherLength

$offset = $magic.Length
[Array]::Copy($backupBytes, $offset, $salt, 0, $salt.Length)
$offset += $salt.Length
[Array]::Copy($backupBytes, $offset, $iv, 0, $iv.Length)
$offset += $iv.Length
[Array]::Copy($backupBytes, $offset, $cipher, 0, $cipher.Length)
$offset += $cipher.Length
[Array]::Copy($backupBytes, $offset, $tag, 0, $tag.Length)

$kdf = New-Object System.Security.Cryptography.Rfc2898DeriveBytes(
    $password,
    $salt,
    200000,
    [System.Security.Cryptography.HashAlgorithmName]::SHA256)
$keyMaterial = $kdf.GetBytes(64)
$aesKey = New-Object byte[] 32
$macKey = New-Object byte[] 32
[Array]::Copy($keyMaterial, 0, $aesKey, 0, 32)
[Array]::Copy($keyMaterial, 32, $macKey, 0, 32)

$bodyLength = $backupBytes.Length - $tag.Length
$body = New-Object byte[] $bodyLength
[Array]::Copy($backupBytes, 0, $body, 0, $body.Length)

$hmac = New-Object System.Security.Cryptography.HMACSHA256(, $macKey)
$expectedTag = $hmac.ComputeHash($body)
for ($i = 0; $i -lt $tag.Length; $i++) {
    if ($tag[$i] -ne $expectedTag[$i]) {
        throw "Backup authentication failed. The key or file is invalid."
    }
}

$aes = [System.Security.Cryptography.Aes]::Create()
$aes.KeySize = 256
$aes.Mode = [System.Security.Cryptography.CipherMode]::CBC
$aes.Padding = [System.Security.Cryptography.PaddingMode]::PKCS7
$aes.Key = $aesKey
$aes.IV = $iv

$decryptor = $aes.CreateDecryptor()
$plain = $decryptor.TransformFinalBlock($cipher, 0, $cipher.Length)

$parent = Split-Path -Parent $OutputPath
if ($parent) {
    New-Item -ItemType Directory -Force -Path $parent | Out-Null
}

[IO.File]::WriteAllBytes($OutputPath, $plain)

if ($keyValues.ContainsKey("raw_sha256")) {
    $actualHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $OutputPath).Hash.ToLowerInvariant()
    if ($actualHash -ne ([string]$keyValues["raw_sha256"]).ToLowerInvariant()) {
        throw "Decrypted file hash does not match raw_sha256 from the key file."
    }
}

Write-Host "Decrypted backup written to $OutputPath"
