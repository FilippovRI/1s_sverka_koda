# Быстрый старт (Windows)

1. Установите [OneScript 2.x](https://oscript.io) вручную (официальный установщик).
2. Сборка GUI: `dotnet publish typovaya-up/launcher/TypovayaUp.Launcher/TypovayaUp.Launcher.csproj -c Release -r win-x64 -p:EnableWindowsTargeting=true`
3. Запуск: `typovaya-up-gui.exe` из publish; укажите выгрузки УП/БП.
4. **ibcmd не обязателен** для сравнения. Дистрибутив 1С — в `typovaya-up/distributions/1c/` (см. README), установка только кнопкой с UAC.
5. CLI: `oscript typovaya-up/src/Главный.os -и ... -п ...` ; формы: `--анализ-форм`.

Диагностика: кнопка «Диагностика» в GUI или `dotnet test tests/TypovayaUp.Launcher.Tests`.
