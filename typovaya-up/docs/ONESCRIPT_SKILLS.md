# OneScript: навыки и правила (typovaya-up)

Для агентов и разработчиков подпроекта `typovaya-up/` подключены адаптированные материалы из репозитория [yellow-hammer/skills-onescript](https://github.com/yellow-hammer/skills-onescript) (MIT, см. `docs/third-party/skills-onescript-LICENSE.txt`).

| Компонент | Путь в репозитории |
|-----------|-------------------|
| Навык OneScript | `.cursor/skills/onescript/` |
| Навык тестирования | `.cursor/skills/onescript-tests/` |
| Правила стиля / грабли / тесты / layout | `.cursor/rules/typovaya-up-os-*.mdc` |

**Ограничение области:** правила действуют на `typovaya-up/**/*.os`, `typovaya-up/tests/**` (см. `globs` в `.mdc`). Autumn, DI, HTTP (Winow) не используются.

**Тесты:** официальный раннер — `tests/run_tests.sh` (скрипты `test_*.os` + `ЗагрузитьСценарий`). OneUnit / 1BDD / 1testrunner описаны в [документации OneScript](https://oscript.io/learn/testing); **переход на фреймворк не обязателен** и в этом этапе не выполнялся.

**Стиль:** русские ключевые слова и имена, табы, UTF-8; у экспортных функций — назначение, параметры, возвращаемое значение (без пересказа кода). Это договорённости репозитория, не формальная сертификация «как в 1С».

Незавершённая ветка этапа «формы» (срез 2) сохранена отдельно: `cursor/wip-forms-slice2-665d` (не для merge).
