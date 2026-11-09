# Быстрый старт (Windows)

1. Установите [OneScript 2.x](https://oscript.io).
2. Сборка GUI: `dotnet publish typovaya-up-gui/launcher/TypovayaUp.Launcher/TypovayaUp.Launcher.csproj -c Release -r win-x64 -p:EnableWindowsTargeting=true`
3. Запуск exe из publish; укажите выгрузки УП/БП (внутри — вызов `typovaya-up/src/Главный.os`).
4. **ibcmd не обязателен** для сравнения. Дистрибутив 1С — в `typovaya-up-gui/distributions/1c/` (см. README), установка только кнопкой с UAC.
5. CLI без GUI: `oscript typovaya-up/src/Главный.os -и ... -п ...`

Диагностика: кнопка «Диагностика» в GUI или `bash typovaya-up-gui/tests/run_tests.sh`.
