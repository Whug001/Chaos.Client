# Drives a kart race through the Debug playtest hook by its real position.
#
# Pure pursuit over the track centre line: each tick reads `state kart.`, aims at
# a look-ahead point on the centre line (offset sideways with -Lateral), and holds
# the steer key that shrinks the signed angle between the kart heading and the aim.
# Only sends key down/up when the wanted state changes.
#
# Units (see Tools/playtest/README.md): x/y in tiles, heading in radians with
# 0 = east (+x) and positive angles turning right (clockwise, +y is south).
# A positive steer error means the aim is to the right, so hold Right.
#
# Exit codes: 0 finished (terminal stage or lap target reached), 1 timeout, 2 pipe error.
param(
    [string]$Client = '',
    [int]$Seconds = 300,
    [double]$Lateral = 0,
    [switch]$UseItems,
    [int]$Laps = 0
)

function Get-WrappedAngle([double]$a) {
    while ($a -gt [Math]::PI) { $a -= 2 * [Math]::PI }
    while ($a -lt -[Math]::PI) { $a += 2 * [Math]::PI }
    return $a
}

# Nearest centre-line point, then walk ahead along the line until $lookahead
# tiles have been covered. The aim shifts sideways by -$lateral along the left
# normal, i.e. a positive $lateral aims right of the direction of travel.
function Get-AimPoint([double]$x, [double]$y, $centre, [double]$lookahead, [double]$lateral) {
    $n = $centre.Count
    $best = 0
    $bestD = [double]::MaxValue
    for ($i = 0; $i -lt $n; $i++) {
        $dx = [double]$centre[$i][0] - $x
        $dy = [double]$centre[$i][1] - $y
        $d = $dx * $dx + $dy * $dy
        if ($d -lt $bestD) { $bestD = $d; $best = $i }
    }
    $acc = 0.0
    $idx = $best
    $ax = [double]$centre[$best][0]
    $ay = [double]$centre[$best][1]
    $tx = 1.0
    $ty = 0.0
    for ($s = 0; $s -lt $n; $s++) {
        $nxt = ($idx + 1) % $n
        $sx = [double]$centre[$nxt][0] - [double]$centre[$idx][0]
        $sy = [double]$centre[$nxt][1] - [double]$centre[$idx][1]
        $len = [Math]::Sqrt($sx * $sx + $sy * $sy)
        if ($len -gt 1e-9) {
            $tx = $sx / $len
            $ty = $sy / $len
            if ($acc + $len -ge $lookahead) {
                $f = ($lookahead - $acc) / $len
                $ax = [double]$centre[$idx][0] + $sx * $f
                $ay = [double]$centre[$idx][1] + $sy * $f
                break
            }
        }
        $acc += $len
        $idx = $nxt
        $ax = [double]$centre[$idx][0]
        $ay = [double]$centre[$idx][1]
    }
    $ax -= $ty * $lateral
    $ay += $tx * $lateral
    return New-Object PSObject -Property @{ X = $ax; Y = $ay; Index = $idx }
}

# Signed angle from the kart heading to the aim direction, in [-pi, pi].
# Positive means the aim is to the right (headings grow clockwise).
function Get-SteerError([double]$heading, [double]$x, [double]$y, [double]$aimX, [double]$aimY) {
    return Get-WrappedAngle (([Math]::Atan2($aimY - $y, $aimX - $x)) - $heading)
}

function Get-WantedSteer([double]$err, [double]$dead) {
    if ($err -gt $dead) { return 'Right' }
    if ($err -lt -$dead) { return 'Left' }
    return 'None'
}

function Send-Command($writer, $reader, [string]$line) {
    $writer.WriteLine($line)
    return $reader.ReadLine()
}

function Get-Data($reply) {
    $parsed = $reply | ConvertFrom-Json
    if ($parsed.ok -ne $true) { throw ('hook error: ' + $reply) }
    return $parsed.data
}

# Sends key down/up only when the wanted state changes. Throws on pipe failure.
function Set-Key($writer, $reader, $held, [string]$name, [bool]$down) {
    if ($held[$name] -eq $down) { return }
    [void](Send-Command $writer $reader ("key $name " + $(if ($down) { 'down' } else { 'up' })))
    $held[$name] = $down
}

function Release-Keys($writer, $reader, $held) {
    foreach ($name in @('Up', 'Down', 'Left', 'Right')) {
        try { Set-Key $writer $reader $held $name $false } catch { }
    }
}

function Main {
    if ([string]::IsNullOrWhiteSpace($Client)) {
        Write-Output 'missing -Client'
        exit 2
    }

    $pipe = New-Object System.IO.Pipes.NamedPipeClientStream('.', "chaos-pt-$Client", [System.IO.Pipes.PipeDirection]::InOut)
    try {
        $pipe.Connect(10000)
    } catch {
        Write-Output "could not reach client '$Client' (is it running with DA_TEST_PIPE=${Client}?)"
        exit 2
    }
    $pipe.ReadTimeout = 15000
    $writer = New-Object System.IO.StreamWriter($pipe)
    $writer.AutoFlush = $true
    $reader = New-Object System.IO.StreamReader($pipe)
    $held = @{ Up = $false; Down = $false; Left = $false; Right = $false }
    $clock = [System.Diagnostics.Stopwatch]::StartNew()

    try {
        try {
            $track = (Get-Data (Send-Command $writer $reader 'state kart.track')).'kart.track'
        } catch {
            Write-Output "pipe error reading track: $_"
            exit 2
        }
        if ($track -eq $null -or $track.gates.Count -eq 0 -or $track.centre.Count -lt 2) {
            Write-Output 'no track data (is a race running?)'
            exit 2
        }
        $centre = $track.centre
        $lapTarget = $Laps
        if ($lapTarget -le 0) {
            try {
                $first = Get-Data (Send-Command $writer $reader 'state kart.')
                if ($first.'kart.laps' -ne $null) { $lapTarget = [int]$first.'kart.laps' }
            } catch {
                Write-Output "pipe error reading state: $_"
                exit 2
            }
        }

        $result = 'timeout'
        $exitCode = 1
        $lap = 0
        $gates = 0
        $place = 0
        $speed = 0.0
        $err = 0.0
        $stuckSince = $null
        $lastPrintSecond = -1
        while ($clock.Elapsed.TotalSeconds -lt $Seconds) {
            try {
                $data = Get-Data (Send-Command $writer $reader 'state kart.')
            } catch {
                $result = 'pipe-error'
                $exitCode = 2
                Write-Output "pipe error reading state: $_"
                break
            }
            $stage = $data.'kart.stage'
            if ($data.'kart.lap' -ne $null) { $lap = [int]$data.'kart.lap' }
            if ($data.'kart.gates' -ne $null) { $gates = [int]$data.'kart.gates' }
            if ($data.'kart.place' -ne $null) { $place = [int]$data.'kart.place' }
            if ($data.'kart.speed' -ne $null) { $speed = [double]$data.'kart.speed' }

            if ($stage -eq 'Results' -or $stage -eq 'Done' -or $stage -eq 'Finished') {
                $result = "finished ($stage)"
                $exitCode = 0
                break
            }
            if ($lapTarget -gt 0 -and $lap -ge $lapTarget) {
                $result = "laps done ($lap/$lapTarget)"
                $exitCode = 0
                break
            }

            if ($stage -ne 'Racing' -or $data.'kart.x' -eq $null) {
                Release-Keys $writer $reader $held
                $stuckSince = $null
            } else {
                $x = [double]$data.'kart.x'
                $y = [double]$data.'kart.y'
                $heading = [double]$data.'kart.heading'
                $lookahead = 3 + $speed * 0.5
                $aim = Get-AimPoint $x $y $centre $lookahead $Lateral
                $err = Get-SteerError $heading $x $y $aim.X $aim.Y
                try {
                    Set-Key $writer $reader $held 'Up' $true
                    Set-Key $writer $reader $held 'Down' $false
                    $want = Get-WantedSteer $err 0.07
                    Set-Key $writer $reader $held 'Left' ($want -eq 'Left')
                    Set-Key $writer $reader $held 'Right' ($want -eq 'Right')
                    if ($UseItems -and $data.'kart.item' -ne $null -and $data.'kart.item' -ne 'None') {
                        [void](Send-Command $writer $reader 'key Space')
                    }
                } catch {
                    $result = 'pipe-error'
                    $exitCode = 2
                    Write-Output "pipe error sending keys: $_"
                    break
                }

                if ($speed -lt 0.5) {
                    if ($stuckSince -eq $null) {
                        $stuckSince = $clock.Elapsed
                    } elseif (($clock.Elapsed - $stuckSince).TotalSeconds -ge 2) {
                        try {
                            Set-Key $writer $reader $held 'Up' $false
                            Set-Key $writer $reader $held 'Left' $false
                            Set-Key $writer $reader $held 'Right' $false
                            Set-Key $writer $reader $held 'Down' $true
                            Start-Sleep -Milliseconds 1000
                            Set-Key $writer $reader $held 'Down' $false
                        } catch {
                            $result = 'pipe-error'
                            $exitCode = 2
                            Write-Output "pipe error in stuck recovery: $_"
                            break
                        }
                        $stuckSince = $null
                    }
                } else {
                    $stuckSince = $null
                }
            }

            $second = [int]$clock.Elapsed.TotalSeconds
            if ($second -ne $lastPrintSecond) {
                $lastPrintSecond = $second
                Write-Output ('t={0} lap={1}/{2} gates={3} place={4} speed={5:0.0} err={6:+0.00;-0.00}' -f $second, $lap, $lapTarget, $gates, $place, $speed, $err)
            }
            Start-Sleep -Milliseconds 100
        }

        Write-Output ('summary result={0} t={1:0} lap={2}/{3} gates={4} place={5}' -f $result, $clock.Elapsed.TotalSeconds, $lap, $lapTarget, $gates, $place)
        exit $exitCode
    } finally {
        Release-Keys $writer $reader $held
        $pipe.Dispose()
    }
}

if ($MyInvocation.InvocationName -ne '.') { Main }
