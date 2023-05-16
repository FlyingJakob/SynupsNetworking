@echo off
setlocal enabledelayedexpansion
set /a counter=0

:start
start /B "" "NetTest.exe"
timeout /t 8 /nobreak > NUL
set /a counter+=1
if !counter! lss 50 goto start

echo All 50 instances have been started. Press any key to close them...
pause > NUL

taskkill /IM "NetTest.exe" /F > NUL
echo All instances of the program have been closed.