param([ValidateRange(1, 64)][int]$SizeMiB = 1)

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$endpoint = & git config --file (Join-Path $projectRoot '.lfsconfig') --get lfs.url
if ($LASTEXITCODE -ne 0 -or -not $endpoint.EndsWith('/info/lfs')) {
    throw 'Expected a configured HTTP Git LFS endpoint in .lfsconfig.'
}
$nasRepository = $endpoint.Substring(0, $endpoint.Length - '/info/lfs'.Length)
$validationRoot = Join-Path ([IO.Path]::GetTempPath()) ('nas-lfs-check-' + [guid]::NewGuid().ToString('N'))
$source = Join-Path $validationRoot 'source'
$download = Join-Path $validationRoot 'download'
New-Item -ItemType Directory -Path $source -Force | Out-Null

function Invoke-Git {
    param([string]$Directory, [string[]]$GitArgs)
    & git -C $Directory @GitArgs
    if ($LASTEXITCODE -ne 0) { throw "Git failed: $($GitArgs -join ' ')" }
}

$previousSkipSmudge = $env:GIT_LFS_SKIP_SMUDGE
try {
    Invoke-Git $source @('init', '-b', 'lfs-verification')
    Invoke-Git $source @('lfs', 'install', '--local')
    Invoke-Git $source @('config', 'user.name', 'NAS LFS verification')
    Invoke-Git $source @('config', 'user.email', 'lfs-check@localhost')
    Invoke-Git $source @('remote', 'add', 'origin', $nasRepository)
    Copy-Item -LiteralPath (Join-Path $projectRoot '.lfsconfig') -Destination $source
    [IO.File]::WriteAllText((Join-Path $source '.gitattributes'), "*.bin filter=lfs diff=lfs merge=lfs -text`n")
    $bytes = New-Object byte[] ($SizeMiB * 1MB)
    $random = [Security.Cryptography.RandomNumberGenerator]::Create()
    try { $random.GetBytes($bytes) } finally { $random.Dispose() }
    [IO.File]::WriteAllBytes((Join-Path $source 'probe.bin'), $bytes)
    $expectedHash = (Get-FileHash -LiteralPath (Join-Path $source 'probe.bin') -Algorithm SHA256).Hash
    Invoke-Git $source @('add', '.gitattributes', '.lfsconfig', 'probe.bin')
    Invoke-Git $source @('-c', 'commit.gpgsign=false', 'commit', '-m', 'Verify NAS LFS transfer')
    # Upload only the LFS object. No Git commits or branches are pushed to NAS or GitHub.
    Invoke-Git $source @('lfs', 'push', 'origin', 'HEAD')

    $env:GIT_LFS_SKIP_SMUDGE = '1'
    & git clone --no-local $source $download
    if ($LASTEXITCODE -ne 0) { throw 'Creating the fresh verification clone failed.' }
    $env:GIT_LFS_SKIP_SMUDGE = $previousSkipSmudge
    Invoke-Git $download @('remote', 'set-url', 'origin', $nasRepository)
    $cachedObjects = Get-ChildItem -LiteralPath (Join-Path $download '.git/lfs/objects') -File -Recurse -ErrorAction SilentlyContinue
    if ($cachedObjects) { throw 'Fresh clone unexpectedly has cached LFS objects.' }
    Invoke-Git $download @('lfs', 'fetch', 'origin', 'HEAD')
    Invoke-Git $download @('lfs', 'checkout')
    Invoke-Git $download @('lfs', 'fsck')
    $actualHash = (Get-FileHash -LiteralPath (Join-Path $download 'probe.bin') -Algorithm SHA256).Hash
    if ($actualHash -ne $expectedHash) { throw 'Downloaded object checksum does not match.' }
    $result = [ordered]@{
        status = 'passed'
        endpoint = $endpoint
        bytes = $bytes.Length
        sha256 = $actualHash.ToLowerInvariant()
        verifiedAt = (Get-Date).ToString('o')
        artifacts = $validationRoot
    }
    $json = $result | ConvertTo-Json
    [IO.File]::WriteAllText((Join-Path $validationRoot 'result.json'), $json)
    $json
} finally {
    $env:GIT_LFS_SKIP_SMUDGE = $previousSkipSmudge
}
