# Прототип: формы БП в restored + заимствованные формы в extension

Дата: 2026-11-24. Opt-in шаг, **по умолчанию выключен**. Текущий рабочий режим генератора (`--формы-из-уп`) не меняется: в `Главный.os` флаг пока не подключён (параллельная работа в той же ветке). Запуск — `scripts/run_forms_bp_proto.os`.

## Зачем

Сейчас формы restored целиком из УП, поэтому 330 ссылок на убранные общие модули только УП остаются в основной конфигурации. Подход прототипа:

- в `restored` — `Form.xml` и `Module.bsl` из БП (типовые);
- в `extension` — заимствованная форма: тело `Form.xml` = БП; реквизиты, элементы и команды только УП создаются программно в модуле (`ФормыПрограммныйСлойУп`, `&После("ПриСозданииНаСервере")`); отличия методов — `&Перед` / `&После` / `&Вместо` / `&ИзменениеИКонтроль`.

Формы только УП у объектов, общих с БП, этим срезом **не переносятся** (нет пары в БП): они остаются в todo / как собственные формы.

## Запуск

```
oscript typovaya-up/scripts/run_forms_bp_proto.os ^
  -и "D:\Выгрузка УП" -п "D:\выгрузка БП" -в "D:\typovaya-up-forms-proto" ^
  --classify ^
  --forms "Catalogs\Организации\Forms\ФормаВыбора\Ext\Form.xml,Catalogs\Организации\Forms\ФормаОрганизации\Ext\Form.xml"
```

Классификация пишет `forms_bp_proto_summary.json` и `forms_bp_proto_items.csv`. Срез пишет `restored/`, `extension/`, `proto_stat.json`, при необходимости `proto_todo.txt`.

Исходные выгрузки и чужие каталоги сборки не изменяются.

## Тест

`tests/test_forms_bp_proto.os` (фикстуры во временном каталоге).

## Дальше

Подключить флаг `--формы-бп-прототип` в `Главный.os`, когда рабочее дерево параллельного агента будет чистым. Полный перевод всех изменённых форм — только после проверки загрузки среза и оценки manual-хуков.


## Срез «только модуль» (`module_only`)

Формы, где Form.xml УП и БП совпадают, а отличается только Module.bsl. В restored — модуль (и тело) из БП; в extension — заимствованная форма с хуками, без программного слоя XML.

```
oscript typovaya-up/scripts/run_forms_bp_proto.os -и "D:\Выгрузка УП" -п "D:\выгрузка БП" -в "D:\typovaya-up-forms-proto" --forms-file "D:\typovaya-up-forms-proto\module_only_forms.txt"
```

Наложение среза на полные выгрузки: `scripts/overlay_forms_bp_proto.os`. Загрузка во временную базу `D:\typovaya-up-tmp-forms`: `scripts/load_forms_module_only.ps1` (копирует restored/extension из `typovaya-up-out-mf2` только чтением в `proto\load\`, затем overlay). Не запускать, пока идёт tu_rebuild9.

## Прогон 109 форм module_only (2026-11-24)

Каталог: `D:\typovaya-up-forms-proto`. Список: `module_only_forms.txt`. Генерация 16:03:56–16:07:09 Europe/Moscow, exit=0.

| показатель | значение |
|---|---|
| forms / restoredXml / restoredModules / borrowed | 109 |
| programmatic | 0 |
| hooksAuto (изменённые методы, автоматические хуки) | 219 |
| hooksAdded (новые методы только УП) | 74 |
| hooksManual / todo | 9 |
| hooksSkip / skipped | 0 |

Девять ручных методов — `proto_todo.txt` (все `status=changed`, `hook=none`, `decision=manual`). У 101 формы хуки записаны в модуль расширения; у 8 (5 без диффа методов к хукам и 3 только manual) модуля в срезе нет.

Загрузку `scripts/load_forms_module_only.ps1` запускать только после строки `check done` в `D:\typovaya-up-out-mf2\rebuild9.log`. Источник mf2 — только чтение, копия в `proto\load\`.