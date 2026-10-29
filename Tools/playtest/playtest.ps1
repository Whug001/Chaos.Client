param(
    [Parameter(Mandatory)][ValidateSet('up', 'logs', 'rebuild', 'down')][string]$Action,
    [string]$Feature,
    [string]$ServerRef
)
$ErrorActionPreference = 'Stop'
$Here = Split-Path -Parent $MyInvocation.MyCommand.Path
$Config = Get-Content "$Here\playtest.config.json" -Raw | ConvertFrom-Json
$RunRoot = Join-Path $env:LOCALAPPDATA 'chaos-playtest'
$CurrentFile = Join-Path $RunRoot 'current.txt'

function Get-Run {
    if (-not (Test-Path $CurrentFile)) { throw "no current playtest run (missing $CurrentFile)" }
    $folder = ([System.IO.File]::ReadAllText($CurrentFile)).Trim()
    $runFile = Join-Path $folder 'run.json'
    if (-not (Test-Path $runFile)) { throw "run.json not found in $folder" }
    return ([System.IO.File]::ReadAllText($runFile) | ConvertFrom-Json)
}

function Save-Run($run) {
    $utf8 = New-Object Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText((Join-Path $run.folder 'run.json'), ($run | ConvertTo-Json -Depth 5), $utf8)
}

function Get-Sha512Upper([string]$text) {
    $sha = [System.Security.Cryptography.SHA512]::Create()
    $bytes = $sha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($text))
    return (($bytes | ForEach-Object { $_.ToString('X2') }) -join '')
}

function New-Worktree($feature, $serverRef) {
    $wt = Join-Path $Config.worktreeRoot ("playtest-" + $feature)
    if (Test-Path $wt) {
        Write-Host ("reusing existing worktree " + $wt)
        return [string]$wt
    }
    if ([string]::IsNullOrWhiteSpace($serverRef)) {
        $sharedServer = Join-Path $Config.clientRepo 'Chaos-Server'
        $serverRef = (git -C $sharedServer rev-parse HEAD).Trim()
        if ($LASTEXITCODE -ne 0) { throw "could not read HEAD of shared server checkout $sharedServer" }
    }
    $stamp = Get-Date -Format yyyyMMdd
    $branch = "playtest/" + $feature + "-" + $stamp
    & git -C $Config.clientRepo worktree add -b $branch $wt HEAD | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "git worktree add failed for $wt" }
    & git -C $wt submodule update --init | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "submodule update failed in $wt" }
    $sub = Join-Path $wt 'Chaos-Server'
    $sharedServer = Join-Path $Config.clientRepo 'Chaos-Server'
    & git -C $sub fetch $sharedServer $serverRef | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "fetch of server ref $serverRef from $sharedServer failed" }
    & git -C $sub checkout $serverRef | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "checkout $serverRef failed in server submodule" }
    return [string]$wt
}

function Build-All($wt) {
    $out = & dotnet build (Join-Path $wt 'Chaos.Client.slnx') -c Debug 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) {
        $out -split "`r?`n" | Where-Object { $_ -match ': error ' } | ForEach-Object { Write-Host $_ }
        throw "client build failed"
    }
    $out = & dotnet build (Join-Path $wt 'Chaos-Server\Chaos\Chaos.csproj') -c Debug 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) {
        $out -split "`r?`n" | Where-Object { $_ -match ': error ' } | ForEach-Object { Write-Host $_ }
        throw "server build failed"
    }
}

function Copy-Data($run) {
    $src = $Config.unoraPath
    $dst = Join-Path $run.folder 'data'
    $backups = Join-Path $src 'Data\Backups'
    & robocopy $src $dst /MIR /XD .git docs $backups /NFL /NDL /NJH /NJS
    if ($LASTEXITCODE -gt 7) { throw "robocopy failed with exit code $LASTEXITCODE" }
}

function New-TestAccounts($run) {
    $data = Join-Path $run.folder 'data'
    $hash = Get-Sha512Upper $Config.password
    foreach ($name in $Config.clients) {
        $dir = Join-Path $data ("Data\Saved\Aislings\" + $name)
        $aislingPath = Join-Path $dir 'aisling.json'
        if (-not (Test-Path $aislingPath)) {
            throw ("character folder not found: " + $dir)
        }
        $text = [System.IO.File]::ReadAllText($aislingPath)
        if ($text -match '"isAdmin"') {
            $text = ([regex]'"isAdmin"\s*:\s*(true|false)').Replace($text, '"isAdmin": true', 1)
        } else {
            $text = ([regex]'\{').Replace($text, '{' + "`r`n" + '  "isAdmin": true,', 1)
        }
        [System.IO.File]::WriteAllText($aislingPath, $text, (New-Object Text.UTF8Encoding($false)))
        $accessDir = Join-Path $data ("Data\Configuration\Access\" + $name)
        if (-not (Test-Path $accessDir)) { New-Item $accessDir -ItemType Directory | Out-Null }
        [System.IO.File]::WriteAllText((Join-Path $accessDir 'password.txt'), $hash, (New-Object Text.UTF8Encoding($false)))
    }
}

function Start-Server($run) {
    $serverBin = Join-Path $run.worktree 'Chaos-Server\Chaos\bin\Debug\net10.0'
    $data = Join-Path $run.folder 'data'
    & python (Join-Path $Here 'patch_server_config.py') $serverBin $data ([int]$Config.portBase)
    if ($LASTEXITCODE -ne 0) { throw "patch_server_config.py failed" }
    $exe = Join-Path $serverBin 'Chaos.exe'
    if (-not (Test-Path $exe)) { throw "server exe not found: $exe" }
    $outLog = Join-Path $run.folder 'logs\server.out.log'
    $errLog = Join-Path $run.folder 'logs\server.err.log'
    $proc = Start-Process $exe -WorkingDirectory $serverBin `
        -RedirectStandardOutput $outLog -RedirectStandardError $errLog -PassThru
    $run.serverPid = $proc.Id
    Save-Run $run
    $port = [int]$Config.portBase
    $deadline = (Get-Date).AddSeconds(60)
    $connected = $false
    while ((Get-Date) -lt $deadline) {
        $tcp = $null
        try {
            $tcp = New-Object System.Net.Sockets.TcpClient
            $tcp.Connect('127.0.0.1', $port)
            $connected = $true
            break
        } catch {
            Start-Sleep -Seconds 1
        } finally {
            if ($tcp -ne $null) { $tcp.Close() }
        }
    }
    if (-not $connected) { throw "server did not accept TCP on port $port within 60 s" }
}

function Save-Screenshot($path) {
    Add-Type -AssemblyName System.Windows.Forms
    Add-Type -AssemblyName System.Drawing
    $bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    $bmp = New-Object System.Drawing.Bitmap($bounds.Width, $bounds.Height)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size)
    $g.Dispose()
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}

function Start-Clients($run) {
    $clientBin = Join-Path $run.worktree 'Chaos.Client\bin\Debug\net10.0'
    $exe = Join-Path $clientBin 'Chaos.Client.exe'
    if (-not (Test-Path $exe)) { throw "client exe not found: $exe" }
    $wait = [int]$Config.loginWaitSeconds
    foreach ($name in $Config.clients) {
        $outLog = Join-Path $run.folder ("logs\" + $name + ".out.log")
        $errLog = Join-Path $run.folder ("logs\" + $name + ".err.log")
        # File redirects (as the server does) so the client never inherits this
        # console: a piped up/rebuild call returns while clients keep running.
        # DA_* vars go on $env: for the Start-Process call, then are restored.
        $prevEnv = @{
            DA_PATH = $env:DA_PATH
            DA_LOBBY_PORT = $env:DA_LOBBY_PORT
            DA_AUTO_USERNAME = $env:DA_AUTO_USERNAME
            DA_AUTO_PASSWORD = $env:DA_AUTO_PASSWORD
            DA_TEST_PIPE = $env:DA_TEST_PIPE
        }
        $env:DA_PATH = $Config.gameDataPath
        $env:DA_LOBBY_PORT = "$($Config.portBase)"
        $env:DA_AUTO_USERNAME = $name
        $env:DA_AUTO_PASSWORD = $Config.password
        $env:DA_TEST_PIPE = $name
        try {
            $proc = Start-Process $exe -WorkingDirectory $clientBin `
                -RedirectStandardOutput $outLog -RedirectStandardError $errLog -PassThru
        } finally {
            foreach ($key in @($prevEnv.Keys)) {
                if ($null -eq $prevEnv[$key]) { Remove-Item ("env:" + $key) -ErrorAction SilentlyContinue }
                else { Set-Item ("env:" + $key) $prevEnv[$key] }
            }
        }
        $run.clientPids += @($proc.Id)
        Save-Run $run
        $deadline = (Get-Date).AddSeconds($wait)
        $ok = $false
        while ((Get-Date) -lt $deadline) {
            $reply = ''
            try { $reply = & (Join-Path $Here 'pt.ps1') $name state 2>&1 | Out-String } catch { $reply = '' }
            if (($null -ne $reply) -and ($reply -match '"loggedIn"\s*:\s*true')) { $ok = $true; break }
            Start-Sleep -Seconds 2
        }
        if (-not $ok) {
            $shot = Join-Path $run.folder ("shots\" + $name + '\login-timeout.png')
            try { Save-Screenshot $shot } catch { Write-Host ("screenshot failed: " + $_.Exception.Message) }
            throw "client $name failed to log in within $wait s"
        }
    }
    return ($Config.clients -join ' ')
}

function Stop-All($run) {
    $wt = [string]$run.worktree
    if ([string]::IsNullOrWhiteSpace($wt)) { return }
    $prefix = $wt.TrimEnd('\') + '\'
    $ids = @()
    if (($null -ne $run.serverPid) -and ([int]$run.serverPid -ne 0)) { $ids += [int]$run.serverPid }
    if ($null -ne $run.clientPids) {
        foreach ($id in $run.clientPids) { $ids += [int]$id }
    }
    foreach ($id in $ids) {
        try {
            $victim = Get-Process -Id $id -ErrorAction Stop
            $vpath = $null
            try { $vpath = $victim.Path } catch { continue }
            if (($null -ne $vpath) -and ($vpath.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase))) {
                try { Stop-Process -Id $victim.Id -Force } catch { }
            }
        } catch { }
    }
    $procs = Get-Process -Name Chaos, 'Chaos.Client' -ErrorAction SilentlyContinue
    foreach ($p in $procs) {
        $path = $null
        try { $path = $p.Path } catch { continue }
        if (($null -ne $path) -and ($path.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase))) {
            try { Stop-Process -Id $p.Id -Force } catch { }
        }
    }
}

function Show-Logs($run) {
    $serverBin = Join-Path $run.worktree 'Chaos-Server\Chaos\bin\Debug\net10.0'
    $logDir = Join-Path $serverBin 'logs'
    $logFile = Join-Path $logDir ((Get-Date -Format yyyy-MM-dd) + '.log')
    if (-not (Test-Path $logFile)) {
        $latest = Get-ChildItem $logDir -Filter '*.log' -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if ($null -eq $latest) { Write-Host ("no server log yet in " + $logDir); return }
        $logFile = $latest.FullName
    }
    $lines = @(Get-Content $logFile)
    $offset = 0
    if ($null -ne $run.logOffset) { $offset = [int]$run.logOffset }
    if ($offset -gt $lines.Count) { $offset = 0 }
    $new = @()
    if ($offset -lt $lines.Count) { $new = @($lines | Select-Object -Skip $offset) }
    $errs = @($new | Where-Object { ($_ -match '"Level"\s*:\s*"(Error|Fatal)"') -or ($_ -match 'Exception') })
    foreach ($line in $errs) { Write-Host $line }
    $rest = @($new | Where-Object { -not (($_ -match '"Level"\s*:\s*"(Error|Fatal)"') -or ($_ -match 'Exception')) })
    foreach ($line in $rest) { Write-Host $line }
    Write-Host ("{0} new lines, {1} errors" -f $new.Count, $errs.Count)
    $run.logOffset = $lines.Count
    Save-Run $run
}

switch ($Action) {
    'up' {
        if ([string]::IsNullOrWhiteSpace($Feature)) { throw "usage: playtest.ps1 up <feature> [-ServerRef <ref>]" }
        if ([string]::IsNullOrWhiteSpace($Config.gameDataPath)) {
            throw "playtest.config.json gameDataPath is empty; set it to the Dark Ages game-data folder"
        }
        $stamp = Get-Date -Format yyyyMMdd-HHmm
        $folder = Join-Path $RunRoot ($stamp + '-' + $Feature)
        New-Item (Join-Path $folder 'data') -ItemType Directory | Out-Null
        New-Item (Join-Path $folder 'logs') -ItemType Directory | Out-Null
        foreach ($name in $Config.clients) {
            New-Item (Join-Path $folder ("shots\" + $name)) -ItemType Directory | Out-Null
        }
        $start = Get-Date
        $run = [pscustomobject]@{
            feature = $Feature
            folder = $folder
            worktree = ''
            startTime = (Get-Date -Date $start -Format o)
            deadline = (Get-Date -Date $start.AddMinutes([int]$Config.runMinutes) -Format o)
            serverPid = 0
            clientPids = @()
            logOffset = 0
        }
        Save-Run $run
        [System.IO.File]::WriteAllText($CurrentFile, $folder, (New-Object Text.UTF8Encoding($false)))
        try {
            $wt = New-Worktree $Feature $ServerRef
            $run.worktree = $wt
            Save-Run $run
            Build-All $wt
            Copy-Data $run
            New-TestAccounts $run
            Start-Server $run
            $names = Start-Clients $run
            Write-Host ("ready: " + $names)
        } catch {
            Stop-All $run
            throw
        }
    }
    'logs' { Show-Logs (Get-Run) }
    'rebuild' {
        $r = Get-Run
        Stop-All $r
        $r.clientPids = @()
        $r.serverPid = 0
        Save-Run $r
        Build-All $r.worktree
        Start-Server $r
        $names = Start-Clients $r
        Write-Host ("ready: " + $names)
    }
    'down' {
        Stop-All (Get-Run)
        Write-Host "playtest processes stopped"
    }
}
