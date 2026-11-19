#!/usr/bin/env bash
# E2E: кэш шага compare (ключ — выгрузки, настройки, код) и кэш bsparser по хешам модулей (syntax, astCheck).
# Второй прогон берёт compare из кэша и не перепроверяет неизменённые модули; результат идентичен первому;
# --без-кэша/--полная-проверка отключают кэши; изменение модуля выгрузки сбрасывает ключ compare.
set -uo pipefail; cd "$(dirname "$0")/.."; OS=${OSCRIPT:-oscript}; T=$(mktemp -d); fail=0
cp -r tests/fixtures/UP tests/fixtures/BP "$T/"
A=(-и "$T/UP" -п "$T/BP" -н tests/fixtures/merge.xml --без-отчета --парсер bsparser)
O="$T/out"
$OS src/Главный.os "${A[@]}" -в "$O" >/dev/null || { echo "FAIL: cache run1"; fail=1; }
grep -q 'compare: результат взят из кэша' "$O/log.txt" && { echo "FAIL: cache run1 из кэша"; fail=1; }
[ -f "$O/cache/compare.json" ] && [ -f "$O/cache/syntax.json" ] && [ -f "$O/cache/ast.json" ] || { echo "FAIL: cache файлы кэша"; fail=1; }
cp -r "$O/extension" "$T/ext1"; cp -r "$O/restored" "$T/rest1"
$OS src/Главный.os "${A[@]}" -в "$O" >/dev/null || { echo "FAIL: cache run2"; fail=1; }
grep -q 'compare: результат взят из кэша' "$O/log.txt" || { echo "FAIL: cache run2 compare не из кэша"; fail=1; }
grep -q '"id":"syntax_errors"[^}]*проверено \([0-9]*\) (из кэша \1)' "$O/self_check.json" || { echo "FAIL: cache run2 syntax не из кэша"; fail=1; }
grep -Eq 'хуков ИиК из кэша [1-9]' "$O/log.txt" || { echo "FAIL: cache run2 astCheck не из кэша"; fail=1; }
diff -r "$T/ext1" "$O/extension" >/dev/null && diff -r "$T/rest1" "$O/restored" >/dev/null || { echo "FAIL: cache результат отличается"; fail=1; }
$OS src/Главный.os "${A[@]}" -в "$O" --без-кэша --полная-проверка >/dev/null || { echo "FAIL: cache run3"; fail=1; }
grep -q 'compare: результат взят из кэша' "$O/log.txt" && { echo "FAIL: --без-кэша"; fail=1; }
grep -q '(из кэша 0)' "$O/self_check.json" || { echo "FAIL: --полная-проверка syntax"; fail=1; }
grep -q 'хуков ИиК из кэша 0' "$O/log.txt" || { echo "FAIL: --полная-проверка astCheck"; fail=1; }
diff -r "$T/ext1" "$O/extension" >/dev/null || { echo "FAIL: cache run3 результат отличается"; fail=1; }
F=$(ls "$T"/UP/CommonModules/*/Ext/Module.bsl | head -1); printf '\n// изменение\n' >> "$F"
$OS src/Главный.os "${A[@]}" -в "$O" >/dev/null || { echo "FAIL: cache run4"; fail=1; }
grep -q 'compare: результат взят из кэша' "$O/log.txt" && { echo "FAIL: cache изменение выгрузки не сбросило ключ"; fail=1; }
rm -rf "$T"
[ $fail -eq 0 ] && echo "OK: e2e cache"; exit $fail
