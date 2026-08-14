[CmdletBinding()]
param(
    [int]$Port = 8000
)

$ErrorActionPreference = "Stop"

$health = Invoke-RestMethod -Uri "http://127.0.0.1:$Port/health" -TimeoutSec 5
if ($health.status -ne "ok")
{
    throw "RAG service tại localhost:$Port chưa sẵn sàng."
}

tailscale status | Out-Null
if ($LASTEXITCODE -ne 0)
{
    throw "Tailscale chưa đăng nhập hoặc chưa chạy."
}

tailscale serve --bg --yes $Port
if ($LASTEXITCODE -ne 0)
{
    throw "Không thể cấu hình Tailscale Serve."
}

tailscale serve status
