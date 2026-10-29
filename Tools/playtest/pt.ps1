param(
    [Parameter(Mandatory, Position = 0)][string]$Client,
    [Parameter(Mandatory, Position = 1, ValueFromRemainingArguments)][string[]]$Command
)
$current = Join-Path $env:LOCALAPPDATA 'chaos-playtest\current.txt'
if (Test-Path $current) {
    $runFile = Join-Path (Get-Content $current -Raw).Trim() 'run.json'
    if (Test-Path $runFile) {
        $run = Get-Content $runFile -Raw | ConvertFrom-Json
        if ($run.deadline -and [DateTime]::Parse($run.deadline) -lt (Get-Date)) {
            Write-Output 'run time limit reached'
            exit 3
        }
    }
}

$pipe = New-Object System.IO.Pipes.NamedPipeClientStream('.', "chaos-pt-$Client", [System.IO.Pipes.PipeDirection]::InOut)
try {
    $pipe.Connect(10000)
} catch {
    Write-Output "could not reach client '$Client' (is it running with DA_TEST_PIPE=${Client}?)"
    exit 2
}

$writer = New-Object System.IO.StreamWriter($pipe)
$writer.AutoFlush = $true
$reader = New-Object System.IO.StreamReader($pipe)
$writer.WriteLine(($Command -join ' '))

$read = $reader.ReadLineAsync()
if (-not $read.Wait(15000)) {
    Write-Output "no reply from '$Client' within 15 s"
    $pipe.Dispose()
    exit 2
}

$reply = $read.Result
$pipe.Dispose()
Write-Output $reply
if ($reply -match '"ok":true') { exit 0 }
exit 1
