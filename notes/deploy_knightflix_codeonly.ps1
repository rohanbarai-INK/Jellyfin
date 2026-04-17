param(
    [Parameter(Mandatory = $true)]
    [string]$PiIp,

    [Parameter(Mandatory = $true)]
    [string]$PiUser,

    [Parameter(Mandatory = $true)]
    [string]$PiPass,

    [Parameter(Mandatory = $true)]
    [string]$ImageTag,

    [string]$LocalTarPath = "jellyfin-pi-build.tar.gz",

    [string]$LocalRemoteScriptPath = "notes/deploy_knightflix_codeonly.sh"
)

$ErrorActionPreference = "Stop"

Import-Module Posh-SSH

if (!(Test-Path -LiteralPath $LocalTarPath)) {
    throw "Tar file not found: $LocalTarPath"
}

if (!(Test-Path -LiteralPath $LocalRemoteScriptPath)) {
    throw "Remote deploy script not found: $LocalRemoteScriptPath"
}

$cred = New-Object PSCredential(
    $PiUser,
    (ConvertTo-SecureString $PiPass -AsPlainText -Force)
)

    Write-Output "Connecting to Pi $PiIp as $PiUser ..."
    $sess = New-SSHSession -ComputerName $PiIp -Credential $cred -AcceptKey -ConnectionTimeout 15

    try {
        Write-Output "Uploading build tar to /root/jellyfin-pi-build.tar.gz ..."
        Set-SCPItem -ComputerName $PiIp -Credential $cred -AcceptKey -Path $LocalTarPath -Destination "/root/" -NewName "jellyfin-pi-build.tar.gz" | Out-Null

        Write-Output "Uploading deploy script to /root/deploy_knightflix_codeonly.sh ..."
        Set-SCPItem -ComputerName $PiIp -Credential $cred -AcceptKey -Path $LocalRemoteScriptPath -Destination "/root/" -NewName "deploy_knightflix_codeonly.sh" | Out-Null

    # Ensure LF line endings + executable bit, then run.
    $remoteCmd = @(
        "set -e",
        "sed -i 's/\r$//' /root/deploy_knightflix_codeonly.sh || true",
        "chmod +x /root/deploy_knightflix_codeonly.sh",
        "bash /root/deploy_knightflix_codeonly.sh '$ImageTag'"
    ) -join "`n"

    Write-Output "Running remote deploy (this can take a while) ..."
    $result = Invoke-SSHCommand -SessionId $sess.SessionId -Command $remoteCmd -TimeOut 7200

    # Emit remote output for logging/verification.
    $result.Output
}
finally {
    Remove-SSHSession -SessionId $sess.SessionId | Out-Null
}
