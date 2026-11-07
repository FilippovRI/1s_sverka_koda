#!/usr/bin/env python3
"""
Эвристика «typovaya-up cognitive load» (НЕ SonarQube Cognitive Complexity).

Для каждой Процедура/Функция в .os:
  score = 1
        + число ветвлений (Если|ИначеЕсли|Иначе|Для|Пока|Попытка|Исключение)
        + сумма (глубина_вложенности) на каждой ветке

Вложенность: +1 при входе в Если/Для/Пока/Попытка, -1 на КонецЕсли/КонецЦикла/КонецПопытки.
Процедура/Функция/КонецПроцедура/КонецФункция не учитываются в счётчике ветвей.
"""
from __future__ import annotations

import json
import re
import sys
from pathlib import Path

BRANCH = re.compile(
    r"^\s*(Если|ИначеЕсли|Иначе|Для|Пока|Попытка)\b", re.IGNORECASE
)
END = re.compile(
    r"^\s*(КонецЕсли|КонецЦикла|КонецПопытки)\b", re.IGNORECASE
)
START_FN = re.compile(r"^\s*(Процедура|Функция)\s+(\w+)", re.IGNORECASE)
END_FN = re.compile(r"^\s*(КонецПроцедуры|КонецФункции)\b", re.IGNORECASE)


def measure_function(body_lines: list[str]) -> int:
    score = 1
    depth = 0
    for line in body_lines:
        if BRANCH.search(line):
            score += 1 + depth
            depth += 1
        elif END.search(line):
            depth = max(0, depth - 1)
    return score


def measure_file(path: Path) -> dict[str, int]:
    text = path.read_text(encoding="utf-8")
    lines = text.splitlines()
    out: dict[str, int] = {}
    i = 0
    while i < len(lines):
        m = START_FN.match(lines[i])
        if m:
            name = m.group(2)
            body: list[str] = []
            i += 1
            while i < len(lines) and not END_FN.match(lines[i]):
                body.append(lines[i])
                i += 1
            out[f"{path.name}:{name}"] = measure_function(body)
        i += 1
    return out


def main() -> int:
    root = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).resolve().parents[1] / "src"
    totals: dict[str, int] = {}
    for f in sorted(root.glob("*.os")):
        totals.update(measure_file(f))
    top = sorted(totals.items(), key=lambda x: -x[1])[:15]
    report = {
        "method": "typovaya-up-cognitive-load-heuristic-v1",
        "note": "Not Sonar cognitive complexity; for trend only.",
        "functions": totals,
        "sum": sum(totals.values()),
        "top15": dict(top),
    }
    json.dump(report, sys.stdout, ensure_ascii=False, indent=2)
    sys.stdout.write("\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
