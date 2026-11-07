#!/usr/bin/env bash
# Прогон на фикстурах. OSCRIPT можно переопределить: OSCRIPT=/path/oscript tests/run_tests.sh
set -u; cd "$(dirname "$0")/.."; OS=${OSCRIPT:-oscript}; OUT=$(mktemp -d); fail=0
$OS src/Главный.os >/dev/null; [ $? -eq 1 ] || { echo "FAIL: без аргументов код должен быть 1"; fail=1; }
$OS tests/test_hooks.os || { echo "FAIL: unit hooks"; fail=1; }
$OS tests/test_parse.os || { echo "FAIL: unit parse"; fail=1; }
$OS tests/test_transfer.os || { echo "FAIL: unit transfer"; fail=1; }
$OS tests/test_contracts.os || { echo "FAIL: unit contracts"; fail=1; }
$OS tests/test_draft.os || { echo "FAIL: unit draft"; fail=1; }
$OS tests/test_xml.os || { echo "FAIL: unit xml"; fail=1; }
$OS tests/test_forms.os || { echo "FAIL: unit forms"; fail=1; }
$OS tests/test_forms_preview.os || { echo "FAIL: unit forms preview"; fail=1; }
$OS tests/test_meta.os || { echo "FAIL: unit meta"; fail=1; }
$OS tests/test_settings.os || { echo "FAIL: unit settings"; fail=1; }
$OS tests/test_summary.os || { echo "FAIL: unit summary"; fail=1; }
H0=$(find tests/fixtures -type f -exec md5sum {} + | sort | md5sum)
$OS src/Главный.os -и tests/fixtures/UP -п tests/fixtures/BP -н tests/fixtures/merge.xml -в "$OUT" || { echo "FAIL: main run"; fail=1; }
OUTP=$(mktemp -d)
$OS src/Главный.os -и tests/fixtures/UP -п tests/fixtures/BP -н tests/fixtures/merge.xml --превью-форм -в "$OUTP" || { echo "FAIL: main with preview"; fail=1; }
diff -qr "$OUT/extension" "$OUTP/extension" >/dev/null || { echo "FAIL: preview changed extension"; fail=1; }
diff -qr "$OUT/restored" "$OUTP/restored" >/dev/null || { echo "FAIL: preview changed restored"; fail=1; }
rm -rf "$OUTP"
exp='"counts":{"identical":2,"cosmetic":1,"auto":3,"manual":1,"only_source":1,"only_vendor":0}'
grep -q "$exp" "$OUT/summary.json" || { echo "FAIL: корзины"; cat "$OUT/summary.json"; fail=1; }
grep -q '"after":1' "$OUT/summary.json" || { echo "FAIL: hooks.after"; cat "$OUT/summary.json"; fail=1; }
grep -q '"before":1' "$OUT/summary.json" || { echo "FAIL: hooks.before"; fail=1; }
grep -q 'changeAndControl' "$OUT/summary.json" || { echo "FAIL: hooks.changeAndControl"; fail=1; }
grep -q 'Forms' "$OUT/summary.json" && { echo "FAIL: формы не исключены"; fail=1; }
grep -q '"name":"Старая","kind":"Процедура","status":"removed"' "$OUT/razbor_moduley.html" || { echo "FAIL: удалённый метод"; fail=1; }
grep -q '&После\|\\u0026После\|after' "$OUT/razbor_moduley.html" || { echo "FAIL: badge after in html"; fail=1; }
$OS src/Главный.os --source tests/fixtures/UP --vendor tests/fixtures/BP --out "$OUT/all" || { echo "FAIL: no settings"; fail=1; }
grep -q '"only_vendor":1' "$OUT/all/summary.json" || { echo "FAIL: режим без настроек"; fail=1; }
# Банки — только added → auto; Счет с removed → manual; ХукНабор — safe hooks → auto
grep -q 'ХукНабор' "$OUT/summary.json" || { echo "FAIL: ХукНабор path"; fail=1; }
E="$OUT/extension"
grep -q 'version="2.21"' "$E/Configuration.xml" || { echo "FAIL: version 2.21"; fail=1; }
grep -q 'xr:ContainedObject' "$E/Configuration.xml" || { echo "FAIL: Configuration InternalInfo"; fail=1; }
grep -q '<NamePrefix>УП_</NamePrefix>' "$E/Configuration.xml" || { echo "FAIL: NamePrefix"; fail=1; }
grep -q 'ChildObjects' "$E/CommonModules/ХукНабор.xml" && { echo "FAIL: CommonModule ChildObjects"; fail=1; }
grep -q 'InternalInfo' "$E/CommonModules/ХукНабор.xml" && { echo "FAIL: CommonModule InternalInfo"; fail=1; }
grep -q 'xr:GeneratedType' "$E/Catalogs/Банки.xml" || { echo "FAIL: Catalog InternalInfo"; fail=1; }
grep -q 'ChildObjects' "$E/Catalogs/Банки.xml" && { echo "FAIL: Catalog без ChildObjects"; fail=1; }
grep -q '<CommonModule>ХукНабор</CommonModule>' "$E/Configuration.xml" || { echo "FAIL: ChildObjects"; fail=1; }
grep -q '11111111-1111-1111-1111-111111111111' "$E/Catalogs/Банки.xml" || { echo "FAIL: uuid из УП"; fail=1; }
M="$E/CommonModules/ХукНабор/Ext/Module.bsl"
grep -q '^&После("ПослеТест")' "$M" && grep -q '^Процедура УП_ПослеТест()' "$M" && grep -q '^&Перед("ПередТест")' "$M" && grep -q '^&ИзменениеИКонтроль("ВставкаТест")' "$M" || { echo "FAIL: хуки в модуле"; fail=1; }
grep -q '^Функция УП_НоваяУП() Экспорт' "$E/Catalogs/Банки/Ext/ObjectModule.bsl" || { echo "FAIL: новый метод"; fail=1; }
grep -q 'Старая' "$E/Documents/Счет/Ext/ObjectModule.bsl" 2>/dev/null && { echo "FAIL: удалённый метод в расширении"; fail=1; }
C="$E/Documents/Счет/Commands/Печать/Ext/CommandModule.bsl"
grep -q '^Процедура УП_ДопПечатьУП()' "$C" || { echo "FAIL: модуль команды"; fail=1; }
grep -q '<Name>Печать</Name>' "$E/Documents/Счет.xml" && grep -q '<Command uuid=' "$E/Documents/Счет.xml" || { echo "FAIL: команда в ChildObjects"; fail=1; }
grep -q '<ExtendedConfigurationObject>22222222-2222-2222-2222-222222222222<' "$E/Documents/Счет.xml" || { echo "FAIL: uuid команды"; fail=1; }
grep -q '44444444' "$E/Documents/Счет.xml" && { echo "FAIL: чужая команда"; fail=1; }
grep -q '33333333-3333-3333-3333-333333333333' "$E/Documents/Счет.xml" || { echo "FAIL: uuid документа"; fail=1; }
grep -q 'ДопПечатьУП' "$OUT/restored/Documents/Счет/Commands/Печать/Ext/CommandModule.bsl" && { echo "FAIL: restored команды"; fail=1; }
[ -e "$E/CommonModules/ТолькоУП" ] && { echo "FAIL: only_source в черновике"; fail=1; }
[ -e "$E/CommonModules/Косметика" ] && { echo "FAIL: cosmetic в черновике"; fail=1; }
R="$OUT/restored/Catalogs/Банки/Ext/ObjectModule.bsl"
grep -q 'НоваяУП' "$R" && { echo "FAIL: restored содержит новый метод"; fail=1; }
grep -q 'НовоеПосле' "$OUT/restored/CommonModules/ХукНабор/Ext/Module.bsl" && { echo "FAIL: restored не типовой"; fail=1; }
grep -q 'ВНИМАНИЕ\|ВАЖНО' "$E/README.txt" || { echo "FAIL: README"; fail=1; }
head -c3 "$M" | od -c | grep -q '357 273 277' && { echo "FAIL: BOM"; fail=1; }
[ "$H0" = "$(find tests/fixtures -type f -exec md5sum {} + | sort | md5sum)" ] || { echo "FAIL: фикстуры изменены"; fail=1; }
$OS src/Главный.os -и tests/fixtures/UP -п tests/fixtures/BP -в tests/fixtures/UP/out >/dev/null; [ $? -eq 2 ] || { echo "FAIL: выход внутри выгрузки"; rm -rf tests/fixtures/UP/out; fail=1; }
[ $fail -eq 0 ] && echo "OK: все проверки пройдены ($OUT)"; exit $fail
