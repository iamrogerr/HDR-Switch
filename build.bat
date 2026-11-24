@echo off
chcp 65001 >nul
echo ========================================================
echo        Compilação do HDR Switch (Portátil)
echo ========================================================
echo.

set CSC="C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if not exist %CSC% (
    set CSC="C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
)

if not exist %CSC% (
    echo [ERRO] O compilador C# nativo do Windows (csc.exe) não foi encontrado.
    pause
    exit /b 1
)

echo Compilando HDRSwitch.exe...
%CSC% /nologo /target:winexe /platform:anycpu /optimize+ /win32manifest:app.manifest /win32icon:app.ico /out:HDRSwitch.exe Program.cs

if %ERRORLEVEL% EQU 0 (
    echo.
    echo [SUCESSO] HDRSwitch.exe foi gerado com sucesso!
    for %%I in (HDRSwitch.exe) do echo Arquivo: %%~nxI (%%~zI bytes)
    echo.
) else (
    echo.
    echo [ERRO] Houve um problema na compilação.
    echo.
)

pause
