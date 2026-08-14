[CmdletBinding()]
param(
    [int]$Port = 8000
)

$ErrorActionPreference = "Stop"

$health = Invoke-RestMethod -Uri "http://127.0.0.1:$Port/health" -TimeoutSec 5
if ($health.status -ne "ok")
{
    throw "RAG service at localhost:$Port is not ready."
}

tailscale status | Out-Null
if ($LASTEXITCODE -ne 0)
{
    throw "Tailscale is not running or signed in."
}

tailscale serve --bg --yes $Port
if ($LASTEXITCODE -ne 0)
{
    throw "Tailscale Serve configuration failed."
}

tailscale serve status
