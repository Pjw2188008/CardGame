@echo off
setlocal
set "UNITY_DATA=D:\Unity\6000.0.74f1\Editor\Data"
if not "%~1"=="" set "UNITY_DATA=%~1"
set "TEST_OUT=%TEMP%\UNKNOWN-BattleRulesTests"
if not exist "%TEST_OUT%" mkdir "%TEST_OUT%"
"%UNITY_DATA%\NetCoreRuntime\dotnet.exe" "%UNITY_DATA%\DotNetSdkRoslyn\csc.dll" /nologo /noconfig /nostdlib+ /langversion:9.0 /target:exe /out:"%TEST_OUT%\BattleRulesTests.exe" /reference:"%UNITY_DATA%\MonoBleedingEdge\lib\mono\4.7.2-api\mscorlib.dll" /reference:"%UNITY_DATA%\MonoBleedingEdge\lib\mono\4.7.2-api\System.dll" /reference:"%UNITY_DATA%\MonoBleedingEdge\lib\mono\4.7.2-api\System.Core.dll" "%~dp0..\..\Assets\UNKNOWN\Battle\Runtime\CardData.cs" "%~dp0..\..\Assets\UNKNOWN\Battle\Runtime\BattleEngine.cs" "%~dp0..\..\Assets\UNKNOWN\Battle\Runtime\BattleEvents.cs" "%~dp0..\..\Assets\UNKNOWN\Battle\Runtime\TurnClock.cs" "%~dp0..\..\Assets\UNKNOWN\Battle\Runtime\BattleProtocol.cs" "%~dp0..\..\Assets\UNKNOWN\Battle\Runtime\JokerMapRules.cs" "%~dp0..\..\Assets\UNKNOWN\Battle\Runtime\MapEventView.cs" "%~dp0..\..\Assets\UNKNOWN\Battle\Runtime\RandomMapSelection.cs" "%~dp0..\..\Assets\UNKNOWN\Battle\Runtime\MapDefinition.cs" "%~dp0..\..\Assets\UNKNOWN\Battle\Runtime\SuitMapRules.cs" "%~dp0..\..\Assets\UNKNOWN\Battle\Runtime\PokerSynergy.cs" "%~dp0PokerTests.cs" "%~dp0HandTests.cs" "%~dp0BattleRulesTests.cs" "%~dp0JokerMapTests.cs" "%~dp0SuitMapTests.cs"
if errorlevel 1 exit /b 1
"%UNITY_DATA%\MonoBleedingEdge\bin\mono.exe" "%TEST_OUT%\BattleRulesTests.exe"
exit /b %ERRORLEVEL%
