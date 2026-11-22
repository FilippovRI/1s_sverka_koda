# typovaya-up-v2 (каркас)

Пакет OneScript для поэтапной миграции утилиты typovaya-up. План: [docs/PLAN_V2.md](../docs/PLAN_V2.md).

Эталонная реализация: `../src/` (сценарии, без packagedef).

## Сборка и тесты

```bash
cd typovaya-up/v2
opm run install   # tasks/install.os
opm run test      # tasks/test.os (oneunit)
oscript src/main.os --help
```

Сквозной CI: `../tests/run_tests.sh` (включая smoke v2).