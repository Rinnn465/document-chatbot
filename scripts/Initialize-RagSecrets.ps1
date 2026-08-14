[CmdletBinding()]
param(
    [switch]$ImportExistingDotEnv
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$secretDirectory = Join-Path $repositoryRoot "secrets"
$openAiSecretPath = Join-Path $secretDirectory "openai_api_key.txt"
$serviceTokenPath = Join-Path $secretDirectory "rag_service_token.txt"

New-Item -ItemType Directory -Force -Path $secretDirectory | Out-Null

if ($ImportExistingDotEnv)
{
    $dotEnvPath = Join-Path $repositoryRoot "rag-service\.env"
    if (-not (Test-Path -LiteralPath $dotEnvPath))
    {
        throw "Cannot find rag-service\.env to import."
    }

    $keyLine = Get-Content -LiteralPath $dotEnvPath |
        Where-Object { $_ -match '^\s*OPENAI_API_KEY\s*=' } |
        Select-Object -First 1
    if (-not $keyLine)
    {
        throw "OPENAI_API_KEY was not found in rag-service\.env."
    }

    $openAiApiKey = ($keyLine -split '=', 2)[1].Trim().Trim('"').Trim("'")
}
else
{
    $secureKey = Read-Host "Enter OpenAI API key (input is hidden)" -AsSecureString
    $keyPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureKey)
    try
    {
        $openAiApiKey = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($keyPointer)
    }
    finally
    {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($keyPointer)
    }
}

if ([string]::IsNullOrWhiteSpace($openAiApiKey))
{
    throw "OpenAI API key cannot be empty."
}

$serviceTokenBytes = [byte[]]::new(32)
$randomNumberGenerator = [Security.Cryptography.RandomNumberGenerator]::Create()
try
{
    $randomNumberGenerator.GetBytes($serviceTokenBytes)
}
finally
{
    $randomNumberGenerator.Dispose()
}
$serviceToken = ([BitConverter]::ToString($serviceTokenBytes) -replace '-', '').ToLowerInvariant()
$utf8WithoutBom = [Text.UTF8Encoding]::new($false)
[IO.File]::WriteAllText($openAiSecretPath, $openAiApiKey.Trim(), $utf8WithoutBom)
[IO.File]::WriteAllText($serviceTokenPath, $serviceToken, $utf8WithoutBom)

Write-Host "Docker secrets created in secrets/ (ignored by Git)."
Write-Host "RAG service token: secrets\rag_service_token.txt"
