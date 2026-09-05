@echo off
setlocal

set WORKSPACE=%~dp0..
set GEN_CLIENT=%WORKSPACE%\Tools\Luban\Luban.dll
set CONF_ROOT=%~dp0

:: ใส่ "" ครอบ path ที่มีโอกาสมีช่องว่างค่ะ
dotnet "%GEN_CLIENT%" ^
    -t client ^
    -c cs-simple-json ^
    -d json ^
    --conf "%CONF_ROOT%luban.conf" ^
    -x outputCodeDir="%WORKSPACE%\Marooned\Assets\Scripts\Data\Gen" ^
    -x outputDataDir="%WORKSPACE%\Marooned\Assets\Resources\DataTables"

echo.
echo Done. Regenerated code into Marooned/Assets/Scripts/Data/Gen
echo and data into Marooned/Assets/Resources/DataTables.
pause