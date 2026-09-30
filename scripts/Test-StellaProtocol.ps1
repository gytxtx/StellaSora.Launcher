#requires -Version 7.0
<#
.SYNOPSIS
Read-only sampling of the official StellaSora CN launcher API and CDN.
.DESCRIPTION
Uses the authentication algorithm shipped in the official 1.3.0 package.
Persists public response samples and evidence only. Never writes game state,
downloads a whole game file, executes a binary, or saves Authorization headers.
Network results are observations, not a CI gate or installation acceptance.
#>
[CmdletBinding()]
param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '../artifacts/stella-protocol-probe'),
    [string]$OfficialAppDirectory = 'E:/Repos/_StellaSora_Gamelauncher/cn_app-32/resources/app'
)

$ErrorActionPreference = 'Stop'
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
[IO.Directory]::CreateDirectory($outputRoot) | Out-Null
$utf8 = [Text.UTF8Encoding]::new($false)
$observations = [Collections.Generic.List[object]]::new()
$handler = [Net.Http.HttpClientHandler]::new()
$handler.AllowAutoRedirect = $false
$client = [Net.Http.HttpClient]::new($handler)
$client.Timeout = [TimeSpan]::FromSeconds(25)

function Save-Json([string]$Name, [object]$Value) {
    $json = ConvertTo-Json -InputObject $Value -Depth 30
    [IO.File]::WriteAllText((Join-Path $outputRoot $Name), ($json -replace "`r`n", "`n") + "`n", $utf8)
}

function Get-Sha256([byte[]]$Bytes) {
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($Bytes)).ToLowerInvariant()
}

function New-OfficialAuthorization {
    $head = [ordered]@{
        game_tag = 'StellaSora_CN'
        time = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
        version = '1.3.0'
    } | ConvertTo-Json -Compress
    $signSource = $head + '872550AD59A235662C5B7D5F88CEBE4B'
    $sign = [Convert]::ToHexString([Security.Cryptography.MD5]::HashData($utf8.GetBytes($signSource))).ToLowerInvariant()
    return '{"head":' + $head + ',"sign":"' + $sign + '"}'
}

function Read-Remote(
    [string]$Name,
    [string]$Url,
    [string]$Method = 'GET',
    [int]$BodyLimit = 262144,
    [switch]$Signed,
    [switch]$RangeProbe
) {
    $uri = [Uri]$Url
    if ($uri.Scheme -ne 'https' -or $uri.Host -notin @(
        'launcher-api.yostar.net', 'game-launcher-ss-cn.yostar.net', 'game-launcher-ss-cn-bk.yostar.net'
    ) -or $uri.UserInfo -or -not $uri.IsDefaultPort) {
        throw 'Probe URL is outside the observed official HTTPS hosts.'
    }

    $request = [Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::new($Method), $uri)
    $response = $null
    $stream = $null
    $record = [ordered]@{
        name = $Name
        sampled_at_utc = [DateTimeOffset]::UtcNow.ToString('o')
        method = $Method
        url = $Url
        status = $null
        requested_range = $(if ($RangeProbe) { 'bytes=0-4095' } else { $null })
    }
    try {
        if ($Signed) {
            $request.Headers.TryAddWithoutValidation('Authorization', (New-OfficialAuthorization)) | Out-Null
        }
        if ($RangeProbe) {
            $request.Headers.TryAddWithoutValidation('Range', 'bytes=0-4095') | Out-Null
        }
        $response = $client.SendAsync($request, [Net.Http.HttpCompletionOption]::ResponseHeadersRead).GetAwaiter().GetResult()
        $record.status = [int]$response.StatusCode
        $headers = [ordered]@{}
        foreach ($headerName in @('Content-Type', 'Content-Length', 'Content-Range', 'Accept-Ranges', 'ETag', 'x-oss-hash-crc64ecma')) {
            $values = $null
            if ($response.Headers.TryGetValues($headerName, [ref]$values) -or
                $response.Content.Headers.TryGetValues($headerName, [ref]$values)) {
                $headers[$headerName] = @($values) -join ', '
            }
        }
        $record.response_headers = $headers
        $bytes = [byte[]]::new(0)
        if ($Method -ne 'HEAD') {
            $stream = $response.Content.ReadAsStreamAsync().GetAwaiter().GetResult()
            $memory = [IO.MemoryStream]::new()
            try {
                $buffer = [byte[]]::new(8192)
                $bodyTimeout = [Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds(25))
                try {
                    while ($memory.Length -lt $BodyLimit) {
                        $count = [Math]::Min($buffer.Length, $BodyLimit - [int]$memory.Length)
                        $read = $stream.ReadAsync($buffer, 0, $count, $bodyTimeout.Token).GetAwaiter().GetResult()
                        if ($read -eq 0) { break }
                        $memory.Write($buffer, 0, $read)
                    }
                    $bytes = $memory.ToArray()
                } finally { $bodyTimeout.Dispose() }
            } finally { $memory.Dispose() }
        }
        $record.bytes_read = $bytes.Length
        $record.body_limit = $BodyLimit
        $record.body_limit_reached = $bytes.Length -eq $BodyLimit
        if ($bytes.Length) { $record.body_sha256 = Get-Sha256 $bytes }
        if ($RangeProbe -and $bytes.Length) {
            $record.prefix_hex = [Convert]::ToHexString($bytes[0..([Math]::Min(15, $bytes.Length - 1))])
        }
        if ($record.status -ge 400 -and $bytes.Length) {
            # Keep only the public storage error code, never request identifiers or headers.
            try {
                $errorBody = [xml]$utf8.GetString($bytes)
                if ($errorBody.Error.Code) { $record.storage_error_code = [string]$errorBody.Error.Code }
            } catch { }
        }
        $observations.Add($record)
        return [pscustomobject]@{ Record = $record; Bytes = $bytes; Text = $utf8.GetString($bytes) }
    } catch {
        # Record exception type only; exception details may include request data.
        $record.error_type = $_.Exception.GetType().FullName
        $observations.Add($record)
        throw "Read-only probe '$Name' failed; inspect evidence.json for metadata."
    } finally {
        if ($stream) { $stream.Dispose() }
        if ($response) { $response.Dispose() }
        $request.Dispose()
    }
}

function Read-Api([string]$Name, [string]$Path) {
    $result = Read-Remote $Name ('https://launcher-api.yostar.net/api/launcher/' + $Path) -Signed
    if ($result.Record.body_limit_reached) { throw 'API response exceeded the sampling limit.' }
    $body = ConvertFrom-Json -InputObject $result.Text
    Save-Json ($Name + '.json') $body
    $result.Record.api_code = $body.code
    if ($result.Record.status -ne 200 -or $body.code -ne 200) {
        throw "API '$Name' did not return HTTP 200 / code 200."
    }
    return $body.data
}

try {
    $game = Read-Api 'game-config' 'game/config'
    Read-Api 'base-config' 'base/config' | Out-Null
    Read-Api 'installation-config' 'installation/config' | Out-Null
    $cdn = Read-Api 'cdn-config' 'advanced/game/download/cdn'
    # Supplementary official endpoint exposes public website links.
    Read-Api 'social-media-resource' 'social/media/resource' | Out-Null
    $manifestPath = 'game/config/json?version=' + [Uri]::EscapeDataString($game.game_latest_version) +
        '&file_path=' + [Uri]::EscapeDataString($game.game_latest_file_path)
    $manifestLocation = Read-Api 'manifest-location' $manifestPath
    $manifestResult = Read-Remote 'manifest' $manifestLocation.url -BodyLimit 4194304
    if ($manifestResult.Record.status -ne 200 -or $manifestResult.Record.body_limit_reached) {
        throw 'Manifest could not be read completely within the 4 MiB sampling limit.'
    }
    $manifest = ConvertFrom-Json -InputObject $manifestResult.Text
    $executables = @($manifest.file | Where-Object { $_.path -match '\.exe$' })
    $scripts = @($manifest.file | Where-Object { $_.path -match '\.(bat|cmd|ps1|sh)$' })
    $totalBytes = ($manifest.file | ForEach-Object { [long]$_.size } | Measure-Object -Sum).Sum
    $sample = [ordered]@{
        sample_kind = 'Selected original entries; not a complete installable manifest'
        source_url = $manifestLocation.url
        original_body_sha256 = $manifestResult.Record.body_sha256
        original_body_bytes = $manifestResult.Record.bytes_read
        original_root_fields = @($manifest.PSObject.Properties.Name)
        source = $manifest.source
        original_file_count = $manifest.file.Count
        original_total_file_bytes = [long]$totalBytes
        executable_entries = $executables
        script_entries = $scripts
        file = @($manifest.file | Select-Object -First 3)
    }
    Save-Json 'manifest-selected.json' $sample

    $representative = $manifest.file | Where-Object { $_.path -eq ('/' + $game.game_start_exe_name + '.exe') } | Select-Object -First 1
    if (-not $representative) { throw 'The start executable has no manifest entry; no game file probe was attempted.' }
    foreach ($domain in @($cdn.primary_cdn, $cdn.back_up_cdn)) {
        $index = $(if ($domain -eq $cdn.primary_cdn) { 'primary' } else { 'backup' })
        # Official worker splits source/path into non-empty segments and encodes the last segment.
        $segments = @($manifest.source.Split('/') + $representative.path.Split('/') | Where-Object { $_ })
        $segments[-1] = [Uri]::EscapeDataString($segments[-1])
        $url = $domain.TrimEnd('/') + '/' + ($segments -join '/')
        Read-Remote ($index + '-executable-head') $url -Method HEAD | Out-Null
        Read-Remote ($index + '-executable-range') $url -BodyLimit 4096 -RangeProbe | Out-Null
    }

    $staticSources = @(
        'package.json', 'out/main/index.js', 'out/main/index-52e4c114.js',
        'out/renderer/assets/main-2d82028d.js', 'out/renderer/assets/GameControl-48cc65aa.js',
        'node_modules/@launcher/utils/src/crypto.ts'
    )
    $staticHashes = foreach ($relative in $staticSources) {
        $path = Join-Path $OfficialAppDirectory $relative
        if (Test-Path -LiteralPath $path -PathType Leaf) {
            [ordered]@{ path = $relative; sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() }
        }
    }
    Save-Json 'official-source-hashes.json' @($staticHashes)
} finally {
    Save-Json 'evidence.json' ([ordered]@{
        official_launcher_version = '1.3.0'
        game_tag = 'StellaSora_CN'
        authentication_headers_persisted = $false
        game_state_written = $false
        binaries_executed = $false
        observations = @($observations.ToArray())
    })
    $client.Dispose()
}

Write-Host "Public samples saved to $outputRoot"
foreach ($record in $observations) {
    Write-Host ("{0}: HTTP {1}, read {2} bytes" -f $record.name, $record.status, $record.bytes_read)
}
