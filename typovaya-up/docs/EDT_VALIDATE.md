# Проверка выгрузок typovaya-up через 1C:EDT CLI

Вместо загрузки в ИБ и `CheckModules` можно импортировать XML-выгрузку в workspace EDT и снять ошибки модулей командой `validate` (минуты после импорта, без базы).

Калибровка сверяется с эталоном CheckModules **211/43** (`D:\typovaya-up-tmp-forms-cm-classa\check_modules_main_classa2.txt` и `check_modules_ext_classa2.txt`): уникальные «Переменная не определена».

## Что установлено на DESKTOP-3O1F3QJ (25.11.2026)

| Что | Значение |
|---|---|
| Продукт | 1C:EDT **(Lite) 2026.2.2.21** (`epp.package.1cedt.light`) |
| CLI | `%LOCALAPPDATA%\1C\1cedtstart\installations\1C_EDT (Lite) 2026.2\1cedt\1cedtcli.exe` |
| ring | **нет** |
| Java | Axiom JDK **25.0.4.1** (`C:\Program Files\1C\1CE\components\axiom-jdk-full-25.0.4+5-x86_64`) |
| Поддерживаемые платформы EDT Lite | **8.3.26, 8.3.27, 8.5.1** |
| Выгрузка typovaya | формат XML `version="2.21"`, `ConfigurationExtensionCompatibilityMode=Version8_5_1`, `CompatibilityMode=Version8_3_24`, платформа загрузки **8.5.1.1150** |

Совместимость CLI и выгрузки по версии платформы **есть** (8.5.1).

`1cedt.ini` установки **не содержит `-vm`**. GUI его подставляет через 1cedtstart. Без `-vm` на Java 25 `1cedtcli` не поднимает JVM (в PATH есть JDK 21, EDT требует 25) и молча висит. Скрипт пишет свой ini в `D:\typovaya-up-edt-tmp\1cedt-cli.ini` и **не меняет** ini установки.

`version` / `platform-versions` занимают около **40–50 с** каждый. Для `version` кучи 1024m уже не хватало («Memory 1001 MB used of 1024 MB») — для импорта по умолчанию скрипт ставит **-Xmx4096m**. На typovaya-up `validate` при 4096m был OOM (`com._1c.g5.v8.derived`, 25.11.2026). На машине с ~14 ГБ RAM ставить **-Xmx6144m** (параметр `-Xmx 6144m`): цель 8g, но системе нужно оставить ≥2–3 ГБ (плюс накладные расходы JVM сверх кучи). Параметр пишется и в свой ini, и в `-vmargs`.

## Workspace

Рабочий workspace агента: `D:\typovaya-up-edt-ws` (проекты `typovaya-up` и `UP_Dorabotki`).

Проект пользователя `D:\EDT-projects\УП` **не использовать и не открывать CLI-ом**, пока он занят EDT (есть `.metadata\.lock`). Скрипт откажется, если `-Workspace` указывает на эту папку.

## Скрипты

```text
typovaya-up/scripts/run_edt_validate.ps1
typovaya-up/scripts/compare_edt_checkmodules.ps1
```

```powershell
# только версия EDT и список платформ
powershell -NoProfile -File typovaya-up\scripts\run_edt_validate.ps1 -Mode Version

# калибровка: импорт эталона load\restored + extension, validate, сверка с 211/43
powershell -NoProfile -File typovaya-up\scripts\run_edt_validate.ps1 -Mode Calibrate

# только validate уже импортированных проектов (по одному имени) + сверка
powershell -NoProfile -File typovaya-up\scripts\run_edt_validate.ps1 -Mode ValidateOnly -Xmx 6144m

# пакет оставшихся УП-ошибок: повторный import среза в уже существующие проекты
powershell -NoProfile -File typovaya-up\scripts\run_edt_validate.ps1 -Mode OverlayPackage -Xmx 6144m
```

Эталон `D:\typovaya-up-forms-cm-classa\load` скрипт только читает. Срез пакета `D:\typovaya-up-forms-cm-uprest` накладывается **повторным `import --configuration-files` в тот же `--project`**, без копии всей 5 ГБ выгрузки и без правки load-папок.

Порог свободного места на D: **15 ГБ**. Параллельный watchdog каждые 30 с смотрит **только свободное место на D** (без обхода размера workspace) и при нехватке останавливает **только свой** `1cedtcli`/`1cedtc`/`javaw` с `-data typovaya-up-edt-ws`. Чужие процессы 1С/EDT не трогает.

Логи и TSV: `D:\typovaya-up-edt-tmp\`.

## Команды CLI (2026.2)

`--project-name-list` в 2026.2 принимает **одно имя**. Два имени подряд дают «не найден вариант вызова команды» (код 204). Скрипт вызывает `validate` отдельно для основной и для расширения.

`--build` принимает значение `true`/`false`. Нельзя одновременно передавать `--project` и `--project-name`: CLI тогда отвечает «не найден вариант вызова команды».

```text
1cedtcli -data <ws> -timeout <сек> -nl ru_RU -ini-file <ini-с-vm> -vmargs -Xmx6144m -command version
1cedtcli ... -command platform-versions
1cedtcli ... -command import --configuration-files <xml> --project-name <имя> --version 8.5.1 --build true
1cedtcli ... -command import --configuration-files <xml-расширения> --project-name <имя> --base-project-name <основная> --version 8.5.1 --build true
1cedtcli ... -command validate --file <отчёт.tsv> --project-name-list <имя-проекта>
```

Импорт полной restored (~5,2 ГБ, ~65 тыс. файлов) по месту: к 15 ГБ нужно держать запас; watchdog остановит работу, если свободное место опустится ниже порога.

Финальную проверку загрузкой в 1С скрипт не заменяет: EDT — быстрый контур между пакетами.
