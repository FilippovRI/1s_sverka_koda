---
name: onescript-tests
description: Тесты typovaya-up на OneScript — test_*.os и run_tests.sh. Без миграции на OneUnit/1testrunner в обязательном порядке.
---

# Тесты typovaya-up

**Запуск (Linux/Windows):**

```bash
OSCRIPT=/path/to/oscript typovaya-up/tests/run_tests.sh
```

Отдельные модули: `oscript typovaya-up/tests/test_hooks.os` и т.д.

## Текущий стиль

- Файлы `tests/test_*.os`, при необходимости `Перем Ошибок` + процедура `П(Условие, Сообщение)`.
- Фикстуры: `tests/fixtures/UP`, `BP`, `merge.xml`.
- Подключение кода: `ЗагрузитьСценарий(ОбъединитьПути(..., "src", "Имя.os"))`.
- Каталог сценария: `ТекущийСценарий().Каталог` → `tests/`; корень утилиты — `..`.

## OneUnit / 1testrunner / 1BDD

См. [oscript.io/learn/testing](https://oscript.io/learn/testing). Допустимы для **новых** подпроектов с `packagedef`; **typovaya-up не переписывать** на фреймворк без отдельного решения и не добавлять dev-зависимости ради раннера.

## Фикстуры

- Не менять `tests/fixtures` без осознанного обновления ожиданий во всех затронутых тестах.
- Сравнение до/после на фикстурах — основной регрессионный контур (без платформы 1С).

Источник адаптации: [skills-onescript](https://github.com/yellow-hammer/skills-onescript) (MIT).
