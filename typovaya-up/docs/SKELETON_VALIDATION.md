# Проверка скелета расширения (этап 6, validator-only)

`--проверить-скелет <каталог>` — каталог **внешней** выгрузки скелета (пользователь / ibcmd), не `extension_meta`.

- Разбор `Configuration.xml` через `ЧтениеXML`, элементы `ContainedObject` / `ObjectId` / `ClassId` только в namespace `http://v8.1c.ru/8.3/xcf/readable` (префикс `xr` или `foo` — без разницы).
- UUID: строгий hex + дефисы, уникальность `ObjectId`.
- `ConfigDumpInfo.xml`: если есть — не пустышка, валидный XML.
- Результат: `skeleton_validation.json`. Каталог скелета **не изменяется**.

**Не доказывается:** что каталог получен из реальной ИБ; семь `ContainedObject` в CI-фикстуре `skeleton_synthetic` — **синтетический шаблон** (`e1000001-…`), не образец реального export.

**Не входит в срез:** команды `ibcmd`/import, `v8find`, runtime-пайплайн.
