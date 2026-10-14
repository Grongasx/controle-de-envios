@echo off
title Controle de Envios Correios
cd /d "%~dp0"
echo Iniciando Controle de Envios...
start "" "bin\Debug\net8.0-windows\ControleEnvios.exe"
exit
