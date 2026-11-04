param([string]$UnityEditorData = 'D:/Unity/6000.0.74f1/Editor/Data')
$ErrorActionPreference = 'Stop'
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$outputDir = Join-Path ([System.IO.Path]::GetTempPath()) 'UNKNOWN-BattleRulesTests'
New-Item -ItemType Directory -Force $outputDir | Out-Null
$monoRefs = Join-Path $UnityEditorData 'MonoBleedingEdge/lib/mono/4.7.2-api'
$testExe = Join-Path $outputDir 'BattleRulesTests.exe'
$arguments = @('/nologo', '/noconfig', '/nostdlib+', '/langversion:9.0', '/target:exe', ('/out:' + $testExe))
foreach ($assembly in @('mscorlib.dll', 'System.dll', 'System.Core.dll')) { $arguments += '/reference:' + (Join-Path $monoRefs $assembly) }
$arguments += Join-Path $projectRoot 'Assets/UNKNOWN/Battle/Runtime/CardData.cs'
$arguments += Join-Path $projectRoot 'Assets/UNKNOWN/Battle/Runtime/BattleEngine.cs'
$arguments += Join-Path $projectRoot 'Assets/UNKNOWN/Battle/Runtime/BattleEvents.cs'
$arguments += Join-Path $projectRoot 'Assets/UNKNOWN/Battle/Runtime/TurnClock.cs'
$arguments += Join-Path $projectRoot 'Assets/UNKNOWN/Battle/Runtime/BattleProtocol.cs'
$arguments += Join-Path $projectRoot 'Assets/UNKNOWN/Battle/Runtime/JokerMapRules.cs'
$arguments += Join-Path $projectRoot 'Assets/UNKNOWN/Battle/Runtime/MapEventView.cs'
$arguments += Join-Path $projectRoot 'Assets/UNKNOWN/Battle/Runtime/RandomMapSelection.cs'
$arguments += Join-Path $PSScriptRoot 'BattleRulesTests.cs'
$arguments += Join-Path $PSScriptRoot 'JokerMapTests.cs'
& (Join-Path $UnityEditorData 'NetCoreRuntime/dotnet.exe') (Join-Path $UnityEditorData 'DotNetSdkRoslyn/csc.dll') @arguments
if ($LASTEXITCODE -ne 0) { throw 'Battle rules test compilation failed.' }
& (Join-Path $UnityEditorData 'MonoBleedingEdge/bin/mono.exe') $testExe
if ($LASTEXITCODE -ne 0) { throw 'Battle rules tests failed.' }
