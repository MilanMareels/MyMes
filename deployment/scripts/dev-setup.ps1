<#
.SYNOPSIS
    Prepares a developer machine: restores tools, sets the development secrets and applies the
    migrations.

.DESCRIPTION
    Secrets go into the .NET user-secrets store, never into a file in the repository. Running
    this twice is safe; it overwrites the same keys.

.EXAMPLE
    .\deployment\scripts\dev-setup.ps1 -AdminUserName admin -AdminEmail admin@example.com

.EXAMPLE
    .\deployment\scripts\dev-setup.ps1 -SkipMigrations
#>
[CmdletBinding()]
param(
    [string]$ConnectionString = "Server=localhost;Database=MyMES;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True",
    [string]$AdminUserName,
    [string]$AdminEmail,
    [securestring]$AdminPassword,
    [switch]$SkipMigrations
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..' '..')
$api = Join-Path $root 'src\Host\MyMES.Api'

Push-Location $root
try {
    Write-Host "Restoring tools..." -ForegroundColor Cyan
    dotnet tool restore

    Write-Host "Setting the connection string..." -ForegroundColor Cyan
    dotnet user-secrets --project $api set "ConnectionStrings:MyMes" $ConnectionString | Out-Null

    # A random key per developer machine, so nobody shares a signing secret and no default
    # ever ends up in a deployed environment.
    Write-Host "Generating a signing key..." -ForegroundColor Cyan
    $bytes = New-Object 'System.Byte[]' 48
    [System.Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
    dotnet user-secrets --project $api set "Jwt:Key" ([Convert]::ToBase64String($bytes)) | Out-Null

    if ($AdminUserName -and $AdminEmail -and $AdminPassword) {
        Write-Host "Setting the bootstrap administrator..." -ForegroundColor Cyan
        $plain = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto(
            [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($AdminPassword))

        dotnet user-secrets --project $api set "Identity:Bootstrap:AdministratorUserName" $AdminUserName | Out-Null
        dotnet user-secrets --project $api set "Identity:Bootstrap:AdministratorEmail" $AdminEmail | Out-Null
        dotnet user-secrets --project $api set "Identity:Bootstrap:AdministratorPassword" $plain | Out-Null
    }
    else {
        Write-Host "No administrator supplied; none will be created. Pass -AdminUserName, -AdminEmail and -AdminPassword to create one." -ForegroundColor Yellow
    }

    if (-not $SkipMigrations) {
        Write-Host "Applying migrations..." -ForegroundColor Cyan
        dotnet ef database update `
            --project src/Infrastructure/MyMES.Persistence `
            --startup-project src/Host/MyMES.Api
    }

    Write-Host ""
    Write-Host "Ready. Start the hosts with:" -ForegroundColor Green
    Write-Host "  dotnet run --project src/Host/MyMES.Api"
    Write-Host "  dotnet run --project src/Host/MyMES.Web"
    Write-Host "  dotnet run --project src/Host/MyMES.MachineService"
}
finally {
    Pop-Location
}
