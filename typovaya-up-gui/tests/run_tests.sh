#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
dotnet test TypovayaUp.Gui.sln -c Release -p:EnableWindowsTargeting=true --nologo
echo "OK: typovaya-up-gui"
