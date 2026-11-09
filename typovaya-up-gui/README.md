# typovaya-up-gui

WinForms-оболочка для запуска **OneScript CLI** `typovaya-up` (сравнение выгрузок, отчёт, черновик расширения). Логика сравнения и переноса только в `../typovaya-up/src/`; GUI собирает аргументы и вызывает `oscript …/Главный.os`.

- Сборка: `dotnet build typovaya-up-gui/TypovayaUp.Gui.sln -c Release -p:EnableWindowsTargeting=true`
- Тесты: `bash typovaya-up-gui/tests/run_tests.sh`
- Дистрибутив платформы 1С (опционально): `distributions/1c/` (см. README там).
