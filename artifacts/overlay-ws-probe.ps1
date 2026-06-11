param(
    [string]$Uri = 'ws://127.0.0.1:10501/ws',
    [string]$Message = '',
    [string]$MessageBase64 = ''
)

$ErrorActionPreference = 'Stop'

if (-not [string]::IsNullOrWhiteSpace($MessageBase64)) {
    $Message = [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($MessageBase64))
}

function Receive-OverlayMessage {
    param(
        [System.Net.WebSockets.ClientWebSocket]$WebSocket,
        [int]$TimeoutSeconds = 3
    )

    $buffer = New-Object byte[] 8192
    $segment = [ArraySegment[byte]]::new($buffer)
    $cts = [System.Threading.CancellationTokenSource]::new()
    $cts.CancelAfter([TimeSpan]::FromSeconds($TimeoutSeconds))

    try {
        $builder = [System.Text.StringBuilder]::new()

        do {
            $result = $WebSocket.ReceiveAsync($segment, $cts.Token).GetAwaiter().GetResult()
            if ($result.MessageType -eq [System.Net.WebSockets.WebSocketMessageType]::Close) {
                return '[closed by server]'
            }

            $builder.Append([System.Text.Encoding]::UTF8.GetString($buffer, 0, $result.Count)) | Out-Null
        } while (-not $result.EndOfMessage)

        return $builder.ToString()
    }
    finally {
        $cts.Dispose()
    }
}

$uriObject = [Uri]$Uri

Write-Output "Target URI : $Uri"
Write-Output "Host       : $($uriObject.Host)"
Write-Output "Port       : $($uriObject.Port)"

try {
    $tcp = Test-NetConnection -ComputerName $uriObject.Host -Port $uriObject.Port -WarningAction SilentlyContinue
    Write-Output "TCP Open   : $($tcp.TcpTestSucceeded)"
}
catch {
    Write-Output "TCP Open   : error - $($_.Exception.Message)"
}

$webSocket = [System.Net.WebSockets.ClientWebSocket]::new()
$connectCts = [System.Threading.CancellationTokenSource]::new()
$connectCts.CancelAfter([TimeSpan]::FromSeconds(5))

try {
    $webSocket.ConnectAsync($uriObject, $connectCts.Token).GetAwaiter().GetResult()
    Write-Output "WS State   : $($webSocket.State)"

    if (-not [string]::IsNullOrWhiteSpace($Message)) {
        Write-Output "Sending    : $Message"
        $messageBytes = [System.Text.Encoding]::UTF8.GetBytes($Message)
        $messageSegment = [ArraySegment[byte]]::new($messageBytes)
        $webSocket.SendAsync(
            $messageSegment,
            [System.Net.WebSockets.WebSocketMessageType]::Text,
            $true,
            [System.Threading.CancellationToken]::None).GetAwaiter().GetResult()

        $response = Receive-OverlayMessage -WebSocket $webSocket
        Write-Output "Response   : $response"
    }
}
catch {
    Write-Output "WS Result  : failed - $($_.Exception.Message)"
}
finally {
    if ($webSocket.State -eq [System.Net.WebSockets.WebSocketState]::Open) {
        $closeCts = [System.Threading.CancellationTokenSource]::new()
        $closeCts.CancelAfter([TimeSpan]::FromSeconds(2))

        try {
            $webSocket.CloseAsync(
                [System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure,
                'probe complete',
                $closeCts.Token).GetAwaiter().GetResult()
        }
        catch {
        }
        finally {
            $closeCts.Dispose()
        }
    }

    $webSocket.Dispose()
    $connectCts.Dispose()
}
