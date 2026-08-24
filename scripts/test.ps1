param(
    [ValidateSet("fast", "clickhouse", "postgres", "sqlserver", "oracle", "hive", "all")]
    [string] $Category = "fast",

    [string] $Configuration = "Debug",

    [switch] $NoRestore,

    [switch] $NoBuild,

    [string] $Timeout = "120s"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$projects = @{
    Core = Join-Path $root "tests/Loader.Core.Tests/Loader.Core.Tests.csproj"
    Lang = Join-Path $root "tests/Loader.Lang.Tests/Loader.Lang.Tests.csproj"
    Query = Join-Path $root "tests/Loader.Query.Tests/Loader.Query.Tests.csproj"
    Script = Join-Path $root "tests/Loader.Script.Tests/Loader.Script.Tests.csproj"
}

function Invoke-TestProject {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Project,

        [string[]] $TreeNodeFilters = @()
    )

    if ($TreeNodeFilters.Count -eq 0) {
        Invoke-TestProjectCore -Project $Project
        return
    }

    foreach ($filter in $TreeNodeFilters) {
        Invoke-TestProjectCore -Project $Project -TreeNodeFilter $filter
    }
}

function Invoke-TestProjectCore {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Project,

        [string] $TreeNodeFilter = ""
    )

    $dotnetArgs = @(
        "test",
        "--project", $Project,
        "--configuration", $Configuration
    )

    if ($NoRestore) {
        $dotnetArgs += "--no-restore"
    }

    if ($NoBuild) {
        $dotnetArgs += "--no-build"
    }

    $dotnetArgs += "--"

    if ($TreeNodeFilter) {
        $dotnetArgs += "--treenode-filter"
        $dotnetArgs += $TreeNodeFilter
    }

    $dotnetArgs += "--timeout"
    $dotnetArgs += $Timeout

    Write-Host "dotnet $($dotnetArgs -join ' ')"
    & dotnet @dotnetArgs
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

switch ($Category) {
    "fast" {
        $previousExternalDatabases = $env:LOADER_TEST_EXTERNAL_DATABASES
        $env:LOADER_TEST_EXTERNAL_DATABASES = "0"

        try {
            Invoke-TestProject -Project $projects.Core
            Invoke-TestProject -Project $projects.Lang
            Invoke-TestProject -Project $projects.Query
            Invoke-TestProject -Project $projects.Script
        }
        finally {
            if ($null -eq $previousExternalDatabases) {
                Remove-Item Env:\LOADER_TEST_EXTERNAL_DATABASES -ErrorAction SilentlyContinue
            }
            else {
                $env:LOADER_TEST_EXTERNAL_DATABASES = $previousExternalDatabases
            }
        }
    }
    "clickhouse" {
        Invoke-TestProject -Project $projects.Core -TreeNodeFilters @(
            "/*/*/ClickHouseProviderTests/*",
            "/*/*/ClickHouseWriterTests/*"
        )
        Invoke-TestProject -Project $projects.Query
        Invoke-TestProject -Project $projects.Script -TreeNodeFilters @(
            "/*/*/LoadJoinStatementTests/*",
            "/*/*/LoadStatementClickHouseTests/*",
            "/*/*/LoadStatementCsvTests/*",
            "/*/*/LoadStatementJsonTests/*",
            "/*/*/LoadStatementMixedTests/*",
            "/*/*/LoadStatementXmlTests/*",
            "/*/*/LoadUnionStatementTests/*",
            "/*/*/MappedLoadStatementTests/*"
        )
    }
    "postgres" {
        Invoke-TestProject -Project $projects.Core -TreeNodeFilters @(
            "/*/*/AutoCastPostgresIntegrationTests/*",
            "/*/*/PostgresProviderTests/*"
        )
        Invoke-TestProject -Project $projects.Script -TreeNodeFilters @(
            "/*/*/LoadStatementPostgresTests/*"
        )
    }
    "sqlserver" {
        Invoke-TestProject -Project $projects.Core -TreeNodeFilters @(
            "/*/*/SqlServerProviderTests/*"
        )
        Invoke-TestProject -Project $projects.Script -TreeNodeFilters @(
            "/*/*/LoadStatementSqlServerTests/*"
        )
    }
    "oracle" {
        Invoke-TestProject -Project $projects.Core -TreeNodeFilters @(
            "/*/*/OracleProviderTests/*"
        )
        Invoke-TestProject -Project $projects.Script -TreeNodeFilters @(
            "/*/*/LoadStatementOracleTests/*"
        )
    }
    "hive" {
        Invoke-TestProject -Project $projects.Script -TreeNodeFilters @(
            "/*/*/LoadProviderResolverTests/*"
        )
    }
    "all" {
        $env:LOADER_TEST_EXTERNAL_DATABASES = "1"
        Invoke-TestProject -Project $projects.Core
        Invoke-TestProject -Project $projects.Lang
        Invoke-TestProject -Project $projects.Query
        Invoke-TestProject -Project $projects.Script
    }
}
