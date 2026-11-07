---
name: onescript
description: OneScript в typovaya-up — сценарии src/*.os, ЗагрузитьСценарий, без opm/packagedef и без платформы 1С. Использовать при правке .os в typovaya-up/.
---

# OneScript (typovaya-up)

Утилита — набор сценариев, не opm-пакет. Точка входа: `typovaya-up/src/Главный.os`. Модули подключаются через `ЗагрузитьСценарий` / `Загрузить` из `Главный.os`, **статическое отсутствие ссылки в одном файле не означает, что модуль мёртв**.

```
typovaya-up/
  src/*.os          рабочий код
  tests/test_*.os   проверки (run_tests.sh)
  tests/fixtures/   выгрузки UP/BP
```

Исходники: UTF-8. Отступ — таб. Ключевые слова русские.

Подробнее: [pitfalls.md](pitfalls.md) (адаптировано из [skills-onescript](https://github.com/yellow-hammer/skills-onescript), MIT).

**Не применять здесь:** Autumn, packagedef, opm, HTTP/Winow.
