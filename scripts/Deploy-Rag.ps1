[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$composeFile = Join-Path $repositoryRoot "compose.rag.yml"

foreach ($secretName in @("openai_api_key.txt", "rag_service_token.txt"))
{
    $secretPath = Join-Path $repositoryRoot "secrets\$secretName"
    if (-not (Test-Path -LiteralPath $secretPath))
    {
        throw "Thiếu secrets\$secretName. Chạy scripts\Initialize-RagSecrets.ps1 trước."
    }
}

docker info | Out-Null
docker compose -f $composeFile up -d --build
if ($LASTEXITCODE -ne 0)
{
    throw "Docker Compose không thể khởi động RAG service."
}

$healthUri = "http://127.0.0.1:8000/health"
for ($attempt = 1; $attempt -le 30; $attempt++)
{
    try
    {
        $health = Invoke-RestMethod -Uri $healthUri -TimeoutSec 3
        if ($health.status -eq "ok")
        {
            Write-Host "RAG service đã sẵn sàng tại $healthUri"
            exit 0
        }
    }
    catch
    {
        Start-Sleep -Seconds 2
    }
}

docker compose -f $composeFile logs --tail 80 rag
throw "RAG service chưa vượt qua health check."
