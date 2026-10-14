@echo off
title Compilando Executavel - Controle de Envios
cd /d "%~dp0"
echo ========================================================
echo Compilando Controle de Envios (.NET 8 WPF Windows x64)
echo ========================================================
echo.

"C:\Program Files\dotnet\dotnet.exe" publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o ".\dist"

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ========================================================
    echo SUCESSO! Executavel gerado na pasta dist\ControleEnvios.exe
    echo ========================================================
) else (
    echo.
    echo Ocorreu um erro durante a compilacao.
)

pause
