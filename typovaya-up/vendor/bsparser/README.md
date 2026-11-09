# bsparser (vendored)

Источник: https://github.com/lead-tools/bsparser, коммит `82704fe03f0ed1b9138a61c216e77f42cbffb735`, лицензия MIT (см. LICENSE).

Взяты без изменений:
- `src/ПарсерВстроенногоЯзыка/Ext/ObjectModule.bsl` — парсер (подключается через `ПодключитьСценарий`);
- `scope/global/src/ГлобальноеОкружение/Ext/ObjectModule.bsl` — описание глобального контекста (список имён для сканера вызовов).

Используется в `src/СинтаксисBsl.os` (self_check: `syntax_errors`) и `src/ДозаполнениеМодуля.os` (whitelist глобальных методов).
Обновление: заменить файлы и коммит в этом README.
