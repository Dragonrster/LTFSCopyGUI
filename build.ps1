<#
.SYNOPSIS
    一键构建 LTFSCopyGUI（主程序 x64 + Tools/CmParser）。

.DESCRIPTION
    自动完成以下步骤：
      1. 定位 MSBuild / VC 工具链（Visual Studio 或 Build Tools）
      2. 缺少 .NET Framework 4.8 参考程序集时，自动从 NuGet 下载到本地缓存
      3. 缺少 .NET Framework SDK 工具（AxImp/TlbImp/al.exe）时，使用兼容方案继续构建
      4. 生成 native 工程需要的 mscoree.lib（由系统 mscoree.dll 导出）
      5. 构建主程序（LTFSCopyGUI\bin\x64\<Configuration>）与 CM 解析器（Tools\CmParser\bin\<Configuration>）
      6. 可选：调用 Installer\Build-Portable.ps1 打包便携版 zip

.PARAMETER Target
    All（默认）= 主程序 + CM 解析器；Main = 只构建主程序；CmParser = 只构建 CM 解析器。

.PARAMETER Configuration
    Release（默认）或 Debug。

.PARAMETER Clean
    构建前先清理目标工程。

.PARAMETER Package
    构建成功后调用 Installer\Build-Portable.ps1 打包到 dist\LTFSCopyGUI-portable.zip。

.PARAMETER SkipRust
    跳过 Rust 加速组件（ltfscopy_fastreader.dll / ltfscopy_schema.dll）构建。

.EXAMPLE
    .\build.ps1
    .\build.ps1 -Target CmParser -Configuration Debug
    .\build.ps1 -Package
#>
[CmdletBinding()]
param(
    [ValidateSet('All', 'Main', 'CmParser')]
    [string]$Target = 'All',

    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [switch]$Clean,

    [switch]$Package,

    [switch]$SkipRust
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:Root = $PSScriptRoot
$script:CacheDir = Join-Path $env:LOCALAPPDATA 'LTFSCopyGUI-build'
$script:Warnings = New-Object System.Collections.Generic.List[string]
$script:MsBuild = $null
$script:CommonProps = New-Object 'System.Collections.Generic.List[string]'
$script:NetFxSdkTools = $null
$script:NeedsSatelliteWorkaround = $false

# ---------------------------------------------------------------------------
# 输出辅助
# ---------------------------------------------------------------------------
function Write-Step {
    param([string]$Message)
    Write-Host ''
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Write-Detail {
    param([string]$Message)
    Write-Host "    $Message" -ForegroundColor DarkGray
}

function Write-Warn {
    param([string]$Message)
    $script:Warnings.Add($Message)
    Write-Host "    [警告] $Message" -ForegroundColor Yellow
}

function Write-Ok {
    param([string]$Message)
    Write-Host "    $Message" -ForegroundColor Green
}

# ---------------------------------------------------------------------------
# 环境准备
# ---------------------------------------------------------------------------
function Find-MSBuild {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path $vswhere) {
        $found = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' 2>$null
        if ($found) {
            return ($found | Select-Object -First 1)
        }
    }
    $cmd = Get-Command msbuild.exe -ErrorAction SilentlyContinue
    if ($cmd) {
        return $cmd.Source
    }
    throw '未找到 MSBuild.exe。请先安装 Visual Studio 2022/2026 或 VS Build Tools（含 .NET 桌面生成工具与 C++ 生成工具）。'
}

function Clear-DuplicateProxyVariables {
    # 大小写重复的 http_proxy/HTTP_PROXY 会让 MSBuild 的 ToolTask 在构造
    # ProcessStartInfo.Environment 时抛 MSB6001（CL.exe 命令行开关无效）。
    foreach ($name in @('http_proxy', 'https_proxy', 'no_proxy')) {
        for ($i = 0; $i -lt 4; $i++) {
            [Environment]::SetEnvironmentVariable($name, $null)
            [Environment]::SetEnvironmentVariable($name.ToUpperInvariant(), $null)
        }
    }
}

function Initialize-CacheDirectory {
    if (-not (Test-Path $script:CacheDir)) {
        New-Item -ItemType Directory -Path $script:CacheDir -Force | Out-Null
    }
}

function Ensure-ReferenceAssemblies {
    $standardPath = Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8'
    if (Test-Path $standardPath) {
        Write-Detail '使用系统已安装的 .NET Framework 4.8 参考程序集'
        return
    }

    $packageDirectory = Join-Path $script:CacheDir 'net48ref'
    if (-not (Test-Path (Join-Path $packageDirectory 'build\.NETFramework\v4.8\mscorlib.dll'))) {
        Write-Detail '系统缺少 .NET Framework 4.8 参考程序集，正在从 NuGet 下载到本地缓存...'
        Initialize-CacheDirectory
        $nupkg = Join-Path $script:CacheDir 'Microsoft.NETFramework.ReferenceAssemblies.net48.1.0.3.nupkg'
        if (-not (Test-Path $nupkg)) {
            $url = 'https://www.nuget.org/api/v2/package/Microsoft.NETFramework.ReferenceAssemblies.net48/1.0.3'
            try {
                Invoke-WebRequest -Uri $url -OutFile $nupkg -UseBasicParsing
            }
            catch {
                throw "下载 .NET Framework 4.8 参考程序集失败：$($_.Exception.Message)。`n请安装 .NET Framework 4.8 Developer Pack，或检查网络/代理。"
            }
        }
        if (Test-Path $packageDirectory) {
            Remove-Item -Path $packageDirectory -Recurse -Force
        }
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        [System.IO.Compression.ZipFile]::ExtractToDirectory($nupkg, $packageDirectory)
    }

    $rootPath = Join-Path $packageDirectory 'build\'
    $script:CommonProps.Add("/p:TargetFrameworkRootPath=$rootPath")
    Write-Detail "使用缓存参考程序集：$packageDirectory"
}

function Find-NetFxSdkTools {
    # 返回包含 AxImp.exe 的 .NET Framework SDK 工具目录；找不到返回 $null。
    $registryRoots = @(
        'HKLM:\SOFTWARE\Microsoft\Microsoft SDKs\NETFXSDK',
        'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Microsoft SDKs\NETFXSDK'
    )
    foreach ($root in $registryRoots) {
        if (-not (Test-Path $root)) { continue }
        $keys = Get-ChildItem -Path $root -ErrorAction SilentlyContinue
        foreach ($key in $keys) {
            $props = Get-ItemProperty -Path $key.PSPath -ErrorAction SilentlyContinue
            if ($null -eq $props) { continue }
            foreach ($name in @('WinSDK-NetFx40Tools-x86', 'WinSDK-NetFx40Tools-x64')) {
                if ($props.PSObject.Properties.Name -contains $name) {
                    $folder = $props.$name
                    if ($folder -and (Test-Path (Join-Path $folder 'bin\AxImp.exe'))) {
                        return (Join-Path $folder 'bin')
                    }
                }
            }
        }
    }
    # 兼容 NETFX 4.8 Tools 常见安装位置。
    $candidates = @(
        (Join-Path ${env:ProgramFiles(x86)} 'Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.8 Tools'),
        (Join-Path ${env:ProgramFiles(x86)} 'Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.8.1 Tools'),
        (Join-Path ${env:ProgramFiles(x86)} 'Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.7.2 Tools')
    )
    foreach ($candidate in $candidates) {
        if ($candidate -and (Test-Path (Join-Path $candidate 'AxImp.exe'))) {
            return $candidate
        }
    }
    return $null
}

function Ensure-ComReferenceTools {
    $script:NetFxSdkTools = Find-NetFxSdkTools
    if ($script:NetFxSdkTools) {
        Write-Detail "使用 .NET Framework SDK 工具：$script:NetFxSdkTools"
    }
    else {
        # COMReference（stdole）会让 MSBuild 强制查找 AxImp.exe，即使当前引用
        # 只需要 tlbimp。用占位工具目录绕过路径解析（不会真正执行这两个工具）。
        $stubDir = Join-Path $script:CacheDir 'netfxtools'
        if (-not (Test-Path $stubDir)) {
            New-Item -ItemType Directory -Path $stubDir -Force | Out-Null
        }
        foreach ($tool in @('AxImp.exe', 'TlbImp.exe')) {
            $path = Join-Path $stubDir $tool
            if (-not (Test-Path $path)) {
                New-Item -ItemType File -Path $path -Force | Out-Null
            }
        }
        $script:CommonProps.Add("/p:ResolveComReferenceToolPath=$stubDir")
        Write-Warn '本机缺少 .NET Framework SDK 工具（AxImp.exe/TlbImp.exe），已使用占位工具目录绕过 COM 引用解析。'
    }

    # 卫星程序集生成依赖 al.exe；缺失时改由 MSBuild 直接编译（不生成 en/zh 资源）。
    $alTool = if ($script:NetFxSdkTools) { Join-Path $script:NetFxSdkTools 'al.exe' } else { $null }
    if (-not ($alTool -and (Test-Path $alTool))) {
        $script:NeedsSatelliteWorkaround = $true
        $script:CommonProps.Add('/p:GenerateSatelliteAssembliesForCore=true')
        Write-Warn '本机缺少 al.exe（.NET Framework SDK 工具），en/zh 卫星资源将不生成；中文界面不受影响。'
    }
}

function Initialize-VcEnvironment {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (-not (Test-Path $vswhere)) {
        throw '未找到 vswhere.exe，无法定位 Visual C++ 工具链。请安装 Visual Studio 的「使用 C++ 的桌面开发」工作负载。'
    }
    $vsPath = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath 2>$null
    if (-not $vsPath) {
        throw '未找到 Visual C++ 工具链。请安装 Visual Studio 的「使用 C++ 的桌面开发」工作负载。'
    }
    $vsPath = ($vsPath | Select-Object -First 1).Trim()
    $vcvarsall = Join-Path $vsPath 'VC\Auxiliary\Build\vcvarsall.bat'
    if (-not (Test-Path $vcvarsall)) {
        throw "未找到 vcvarsall.bat：$vcvarsall"
    }

    Initialize-CacheDirectory
    $vcvarsCmd = Join-Path $script:CacheDir 'vcvars.cmd'
    $lines = @(
        '@echo off',
        "call `"$vcvarsall`" x64 >nul",
        'set'
    )
    Set-Content -Path $vcvarsCmd -Value $lines -Encoding ASCII

    Write-Detail "加载 VC 环境：$vcvarsall x64"
    $vcOutput = & cmd.exe /c "`"$vcvarsCmd`"" 2>&1
    $vcEnv = @{}
    foreach ($line in $vcOutput) {
        if ($line -match '^([^=]+)=(.*)$') {
            $vcEnv[$Matches[1]] = $Matches[2]
        }
    }
    if ($vcEnv.Count -eq 0) {
        throw '加载 VC 环境失败（vcvarsall.bat 没有返回环境变量）。'
    }
    foreach ($name in @('PATH', 'LIB', 'INCLUDE', 'LIBPATH', 'WindowsSdkDir', 'WindowsSDKVersion',
                        'VCToolsInstallDir', 'VCToolsVersion', 'UCRTVersion', 'VSINSTALLDIR',
                        'VCINSTALLDIR', 'VSCMD_ARG_TGT_ARCH')) {
        if ($vcEnv.ContainsKey($name)) {
            [Environment]::SetEnvironmentVariable($name, $vcEnv[$name])
        }
    }
    $script:CommonProps.Add('/p:UseEnv=true')

    # 选择与已安装 MSVC 匹配的平台工具集（vcxproj 可能请求未安装的 v142）。
    if ($env:VCToolsInstallDir) {
        $version = Split-Path $env:VCToolsInstallDir -Leaf
        $segments = $version.Split('.')
        if ($segments.Count -ge 2) {
            $minor = [int]$segments[1]
            $toolset = 'v14' + ([math]::Floor($minor / 10)).ToString()
            $script:CommonProps.Add("/p:PlatformToolset=$toolset")
            Write-Detail "平台工具集：$toolset（MSVC $version）"
        }
    }

    # native 工程（C++/CLI）链接需要 mscoree.lib；SDK 缺失时从系统 mscoree.dll 现场生成。
    $libFound = $false
    if ($env:LIB) {
        foreach ($entry in $env:LIB.Split(';')) {
            if ($entry -and (Test-Path (Join-Path $entry 'mscoree.lib'))) {
                $libFound = $true
                break
            }
        }
    }
    if (-not $libFound) {
        $mscoree = Join-Path $env:WINDIR 'System32\mscoree.dll'
        if (-not (Test-Path $mscoree)) {
            throw "找不到 $mscoree，无法生成 mscoree.lib。"
        }
        $toolBin = Join-Path $env:VCToolsInstallDir 'bin\Hostx64\x64'
        $dumpbin = Join-Path $toolBin 'dumpbin.exe'
        $libexe = Join-Path $toolBin 'lib.exe'
        if (-not ((Test-Path $dumpbin) -and (Test-Path $libexe))) {
            throw "找不到 dumpbin.exe / lib.exe（$toolBin）。"
        }
        $importDir = Join-Path $script:CacheDir 'mscoree_import'
        if (-not (Test-Path $importDir)) {
            New-Item -ItemType Directory -Path $importDir -Force | Out-Null
        }
        $exports = & $dumpbin /nologo /exports $mscoree 2>&1
        $names = New-Object System.Collections.Generic.List[string]
        foreach ($line in $exports) {
            if ($line -match '^\s*\d+\s+[0-9A-Fa-f]+\s+[0-9A-Fa-f]+\s+(\S+)\s*$') {
                $names.Add($Matches[1])
            }
        }
        if ($names.Count -eq 0) {
            throw '无法从 mscoree.dll 解析导出符号。'
        }
        $defPath = Join-Path $importDir 'mscoree.def'
        $defLines = New-Object System.Collections.Generic.List[string]
        $defLines.Add('LIBRARY mscoree.dll')
        $defLines.Add('EXPORTS')
        foreach ($name in $names) {
            $defLines.Add($name)
        }
        Set-Content -Path $defPath -Value $defLines -Encoding ASCII
        $libPath = Join-Path $importDir 'mscoree.lib'
        & $libexe /nologo /machine:x64 "/def:$defPath" "/out:$libPath" 2>&1 | Out-Null
        if (-not (Test-Path $libPath)) {
            throw '生成 mscoree.lib 失败。'
        }
        $env:LIB = "$importDir;$env:LIB"
        $script:CommonProps.Add('/p:UseEnv=true')
        Write-Warn '本机缺少 mscoree.lib，已从系统 mscoree.dll 现场生成（仅用于本地构建）。'
    }
}

function Initialize-SdkResolvers {
    # FtpServer 是 Microsoft.NET.Sdk 风格工程。VS Build Tools 自带的 SDK 解析器
    # 可能缺少版本信息（探测的是 ...\dotnet\sdk\Sdks 而不是具体版本目录），
    # 因此优先使用最新 dotnet SDK 的版本化 Sdks 目录。
    if ($env:MSBuildSDKsPath) {
        Write-Detail "MSBuildSDKsPath 已设置：$env:MSBuildSDKsPath"
        return
    }
    $dotnet = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
    if (-not (Test-Path $dotnet)) {
        Write-Warn '未找到 dotnet（FtpServer 是 Microsoft.NET.Sdk 工程，可能无法构建）。'
        return
    }
    $sdkLines = & $dotnet --list-sdks 2>$null
    if (-not $sdkLines) {
        Write-Warn 'dotnet 没有安装 SDK，FtpServer（Microsoft.NET.Sdk 工程）可能无法构建。'
        return
    }
    $candidate = $null
    foreach ($line in ($sdkLines | Sort-Object)) {
        if ($line -match '^\s*(\S+)\s*\[(.+)\]\s*$') {
            $version = $Matches[1]
            $root = $Matches[2]
            $sdkDirectory = Join-Path (Join-Path $root $version) 'Sdks'
            if (Test-Path (Join-Path $sdkDirectory 'Microsoft.NET.Sdk')) {
                $candidate = $sdkDirectory
            }
        }
    }
    if ($candidate) {
        $env:MSBuildSDKsPath = $candidate
        Write-Detail "MSBuildSDKsPath：$candidate"
    }
    else {
        Write-Warn '未找到可用的 dotnet SDK Sdks 目录，FtpServer 可能无法构建。'
    }
}

function Restore-SdkProject {
    param([string]$Project)
    $projectDirectory = Split-Path $Project -Parent
    $assets = Join-Path $projectDirectory 'obj\project.assets.json'
    if (Test-Path $assets) {
        return
    }
    Write-Detail "还原 SDK 风格工程：$(Split-Path $Project -Leaf)"
    $output = & $script:MsBuild $Project '/t:Restore' '/nologo' '/v:m' 2>&1
    if ($LASTEXITCODE -ne 0) {
        $dotnet = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
        if (Test-Path $dotnet) {
            $output = & $dotnet restore $Project 2>&1
        }
    }
    if (-not (Test-Path $assets)) {
        Write-Warn "NuGet 还原失败：$(Split-Path $Project -Leaf)（后续构建可能报 NETSDK1004）"
    }
}

function Assert-BuildArtifacts {
    param([string]$MainExe, [string]$CmExe)
    if ($MainExe -and -not (Test-Path $MainExe)) {
        throw "构建结束但没有生成主程序：$MainExe"
    }
    if ($CmExe -and -not (Test-Path $CmExe)) {
        throw "构建结束但没有生成 CM 解析器：$CmExe"
    }
}

# ---------------------------------------------------------------------------
# 构建执行
# ---------------------------------------------------------------------------
function New-MSBuildArguments {
    param([string]$Project, [string]$Targets, [string]$Platform, [string[]]$Extra)
    $arguments = New-Object System.Collections.Generic.List[string]
    $arguments.Add($Project)
    $arguments.Add("/t:$Targets")
    $arguments.Add("/p:Configuration=$Configuration")
    $arguments.Add("/p:Platform=$Platform")
    $arguments.Add('/v:m')
    $arguments.Add('/nologo')
    $arguments.Add('/m')
    foreach ($prop in $script:CommonProps) {
        $arguments.Add($prop)
    }
    if ($Extra) {
        foreach ($item in $Extra) {
            $arguments.Add($item)
        }
    }
    return $arguments
}

function Invoke-Build {
    param(
        [string]$Project,
        [string]$Targets,
        [string]$Platform = 'x64',
        [string[]]$Extra,
        [switch]$AllowKnownFailures
    )
    $arguments = New-MSBuildArguments -Project $Project -Targets $Targets -Platform $Platform -Extra $Extra
    Write-Detail "msbuild $(Split-Path $Project -Leaf) /t:$Targets /p:Platform=$Platform"
    $output = & $script:MsBuild @arguments 2>&1
    $exitCode = $LASTEXITCODE
    foreach ($line in $output) {
        Write-Host "    $line"
    }
    if ($exitCode -eq 0) {
        return $true
    }
    if ($AllowKnownFailures) {
        $errors = @($output | Where-Object { $_ -match ': error ' })
        $known = @($errors | Where-Object {
            ($_ -match 'MSB3030' -and $_ -match 'resources\.dll') -or
            ($_ -match 'MSB3086' -and $_ -match 'al\.exe')
        })
        if ($errors.Count -gt 0 -and $known.Count -eq $errors.Count) {
            Write-Warn '构建存在已知的卫星程序集错误（缺少 al.exe），主程序已生成，继续。'
            return $true
        }
    }
    throw "构建失败（$Targets）：$Project"
}

function Build-MainProject {
    Write-Step '构建 LTFSCopyGUI 主程序（x64）'
    $project = Join-Path $script:Root 'LTFSCopyGUI\LTFSCopyGUI.vbproj'
    if (-not (Test-Path $project)) {
        throw "找不到主工程：$project"
    }

    Restore-SdkProject -Project (Join-Path $script:Root 'FtpServer\Zhaobang.FtpServer\Zhaobang.FtpServer.csproj')

    if ($Clean) {
        Write-Detail '清理主程序输出...'
        $cleanArguments = New-MSBuildArguments -Project $project -Targets 'Clean' -Platform 'x64' -Extra @()
        $null = & $script:MsBuild @cleanArguments
    }

    $outputDirectory = Join-Path $script:Root "LTFSCopyGUI\bin\x64\$Configuration"
    $null = Invoke-Build -Project $project -Targets 'PrepareResources;Compile;CopyFilesToOutputDirectory' -AllowKnownFailures

    Assert-BuildArtifacts -MainExe (Join-Path $outputDirectory 'LTFSCopyGUI.exe')
    Write-Ok "主程序输出：$outputDirectory\LTFSCopyGUI.exe"

    $nativeDll = Join-Path $outputDirectory 'LTFSCopyGUI.Native.dll'
    if (-not (Test-Path $nativeDll)) {
        Write-Warn '未找到 LTFSCopyGUI.Native.dll，C++/CLI 原生组件可能构建失败。'
    }
}

function Build-RustComponents {
    if ($SkipRust) {
        Write-Step '跳过 Rust 加速组件（-SkipRust）'
        return
    }
    $outputDirectory = Join-Path $script:Root "LTFSCopyGUI\bin\x64\$Configuration"
    $existing = @(
        (Join-Path $outputDirectory 'ltfscopy_fastreader.dll'),
        (Join-Path $outputDirectory 'ltfscopy_schema.dll')
    )
    $cargo = Get-Command cargo -ErrorAction SilentlyContinue
    if (-not $cargo) {
        $missing = @($existing | Where-Object { -not (Test-Path $_) })
    if ($missing.Count -gt 0) {
            Write-Warn '未安装 cargo，跳过 Rust 加速组件；磁带串流加速与原生 schema 功能在运行时会自动回退，不影响带库/CM 解析基础功能。'
        }
        return
    }
    Write-Step '构建 Rust 加速组件（ltfscopy_fastreader / ltfscopy_schema）'
    $components = @(
        @{ Manifest = (Join-Path $script:Root 'LtfsFastReader\Cargo.toml'); Name = 'ltfscopy_fastreader.dll' },
        @{ Manifest = (Join-Path $script:Root 'LtfsSchema\Cargo.toml'); Name = 'ltfscopy_schema.dll' }
    )
    foreach ($component in $components) {
        if (-not (Test-Path $component.Manifest)) { continue }
        $manifestDirectory = Split-Path $component.Manifest -Parent
        $cargoOutput = & $cargo.Source '+nightly' 'build' '-Z' 'build-std=std,panic_abort' '--target' 'x86_64-win7-windows-msvc' '--profile' 'release' '--lib' '--manifest-path' $component.Manifest 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Warn "Rust 组件构建失败：$(Split-Path $component.Manifest -Leaf)（需要 rustup nightly 与 x86_64-win7-windows-msvc target）"
            continue
        }
        $built = Join-Path $manifestDirectory "target\x86_64-win7-windows-msvc\release\$($component.Name)"
        if (Test-Path $built) {
            Copy-Item -Path $built -Destination $outputDirectory -Force
            Write-Ok "已复制 $(Split-Path $built -Leaf)"
        }
        else {
            Write-Warn "Rust 构建完成但未找到产物：$built"
        }
    }
}

function Build-CmParser {
    Write-Step '构建 Tools/CmParser'
    $project = Join-Path $script:Root 'Tools\CmParser\CmParser.vbproj'
    if (-not (Test-Path $project)) {
        throw "找不到 CM 解析器工程：$project"
    }
    $targets = if ($Clean) { 'Rebuild' } else { 'Build' }
    $null = Invoke-Build -Project $project -Targets $targets -Platform 'AnyCPU'
    $exe = Join-Path $script:Root "Tools\CmParser\bin\$Configuration\CmParser.exe"
    Assert-BuildArtifacts -CmExe $exe
    Write-Ok "CM 解析器输出：$exe"
}

function Invoke-Package {
    Write-Step '打包便携版'
    $script = Join-Path $script:Root 'Installer\Build-Portable.ps1'
    if (-not (Test-Path $script)) {
        Write-Warn '未找到 Installer\Build-Portable.ps1，跳过打包。'
        return
    }
    $outputDirectory = Join-Path $script:Root 'dist'
    if (-not (Test-Path $outputDirectory)) {
        New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
    }
    $archivePath = Join-Path $outputDirectory 'LTFSCopyGUI-portable.zip'
    if (Test-Path $archivePath) {
        Remove-Item -Path $archivePath -Force
    }
    & $script -OutputPath $archivePath
    if (Test-Path $archivePath) {
        Write-Ok "便携版：$archivePath"
    }
    else {
        throw '打包失败：未生成 zip。'
    }
}

function Restore-BuildVersionFile {
    # PreBuildEvent 会用 BuildVersionPropagator.exe 改写 Build\Build.vb；若构建前是干净的，构建后还原。
    $git = Get-Command git -ErrorAction SilentlyContinue
    if (-not $git) { return }
    $repository = Join-Path $script:Root '.git'
    if (-not (Test-Path $repository)) { return }
    $relativePath = 'LTFSCopyGUI/Build/Build.vb'
    $before = & $git.Source -C $script:Root status --porcelain -- $relativePath 2>$null
    if ($before) { return }
    $after = & $git.Source -C $script:Root status --porcelain -- $relativePath 2>$null
    if ($after) {
        & $git.Source -C $script:Root checkout -- $relativePath 2>$null
        Write-Detail '已还原构建期改写的 LTFSCopyGUI/Build/Build.vb'
    }
}

# ---------------------------------------------------------------------------
# 主流程
# ---------------------------------------------------------------------------
try {
    Write-Host 'LTFSCopyGUI 一键构建' -ForegroundColor White
    Write-Host "  仓库：$script:Root"
    Write-Host "  目标：$Target   配置：$Configuration"

    Write-Step '定位构建工具链'
    $script:MsBuild = Find-MSBuild
    Write-Detail "MSBuild：$script:MsBuild"
    Clear-DuplicateProxyVariables
    Initialize-CacheDirectory
    Ensure-ReferenceAssemblies
    Ensure-ComReferenceTools

    if ($Target -eq 'All' -or $Target -eq 'Main') {
        Initialize-SdkResolvers
        Initialize-VcEnvironment
    }

    $mainExe = $null
    $cmExe = $null

    if ($Target -eq 'All' -or $Target -eq 'Main') {
        Build-MainProject
        Build-RustComponents
        Restore-BuildVersionFile
        $mainExe = Join-Path $script:Root "LTFSCopyGUI\bin\x64\$Configuration\LTFSCopyGUI.exe"
    }

    if ($Target -eq 'All' -or $Target -eq 'CmParser') {
        Build-CmParser
        $cmExe = Join-Path $script:Root "Tools\CmParser\bin\$Configuration\CmParser.exe"
    }

    if ($Package) {
        Invoke-Package
    }

    Write-Step '构建结果'
    if ($mainExe) { Write-Ok "主程序：$mainExe" }
    if ($cmExe) { Write-Ok "CM 解析器：$cmExe" }
    if ($script:Warnings.Count -gt 0) {
        Write-Host ''
        Write-Host '警告：' -ForegroundColor Yellow
        foreach ($warning in $script:Warnings) {
            Write-Host "  - $warning" -ForegroundColor Yellow
        }
    }
    Write-Host ''
    Write-Host '构建完成。' -ForegroundColor Green
    exit 0
}
catch {
    Write-Host ''
    Write-Host "构建失败：$($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
