#!/usr/bin/env bash
# Прогон на фикстурах. OSCRIPT можно переопределить: OSCRIPT=/path/oscript tests/run_tests.sh
set -uo pipefail; cd "$(dirname "$0")/.."; OS=${OSCRIPT:-oscript}; OUT=$(mktemp -d); fail=0
$OS src/Главный.os >/dev/null; [ $? -eq 1 ] || { echo "FAIL: без аргументов код должен быть 1"; fail=1; }
$OS tests/test_hooks.os || { echo "FAIL: unit hooks"; fail=1; }
$OS tests/test_parse.os || { echo "FAIL: unit parse"; fail=1; }
$OS tests/test_ast.os || { echo "FAIL: unit ast"; fail=1; }
$OS tests/test_ast_route.os || { echo "FAIL: unit ast route"; fail=1; }
$OS tests/test_transfer.os || { echo "FAIL: unit transfer"; fail=1; }
$OS tests/test_cac_multiline.os || { echo "FAIL: unit CAC multiline"; fail=1; }
$OS tests/test_token_line.os || { echo "FAIL: unit token line"; fail=1; }
$OS tests/test_cac_verbatim.os || { echo "FAIL: unit CAC verbatim"; fail=1; }
$OS tests/test_cac_applicability.os || { echo "FAIL: unit CAC applicability"; fail=1; }
OSCRIPT="$OS" bash tests/e2e_cac_applicability.sh || { echo "FAIL: e2e CAC applicability"; fail=1; }
$OS tests/test_internalinfo_uuid.os || { echo "FAIL: unit internalinfo uuid"; fail=1; }
$OS tests/test_self_check.os || { echo "FAIL: unit self check"; fail=1; }
$OS tests/test_ibcmd_extension.os || { echo "FAIL: unit ibcmd extension"; fail=1; }
$OS tests/test_extension_helpers.os || { echo "FAIL: unit extension helpers"; fail=1; }
$OS tests/test_restored_refs.os || { echo "FAIL: unit restored refs"; fail=1; }
$OS tests/test_function_hooks.os || { echo "FAIL: unit function hooks"; fail=1; }
$OS tests/test_contracts.os || { echo "FAIL: unit contracts"; fail=1; }
$OS tests/test_draft.os || { echo "FAIL: unit draft"; fail=1; }
$OS tests/test_xml.os || { echo "FAIL: unit xml"; fail=1; }
$OS tests/test_forms.os || { echo "FAIL: unit forms"; fail=1; }
$OS tests/test_forms_preview.os || { echo "FAIL: unit forms preview"; fail=1; }
$OS tests/test_forms_transfer.os || { echo "FAIL: unit forms transfer"; fail=1; }
$OS tests/test_skeleton.os || { echo "FAIL: unit skeleton"; fail=1; }
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
OUTA=$(mktemp -d)
$OS src/Главный.os -и tests/fixtures/UP -п tests/fixtures/BP -н tests/fixtures/merge.xml --parser bsparser -в "$OUTA" >/dev/null || { echo "FAIL: main --parser bsparser"; fail=1; }
diff -qr "$OUT/extension" "$OUTA/extension" >/dev/null || { echo "FAIL: --parser bsparser changed extension"; fail=1; }
grep -q '"failed":0' "$OUTA/ast_check.json" || { echo "FAIL: ast_check.json"; cat "$OUTA/ast_check.json"; fail=1; }
grep -q '"bsparser":{"astCheck"' "$OUTA/razbor_moduley.html" || { echo "FAIL: bsparser section in html"; fail=1; }
grep -q 'bsparser: модулей сравнено по дереву разбора' "$OUTA/log.txt" || { echo "FAIL: bsparser log"; fail=1; }
grep -q 'bsparser: самые медленные модули сравнения' "$OUTA/log.txt" && grep -q 'фрагментов методов разобрано' "$OUTA/log.txt" || { echo "FAIL: bsparser timings log"; fail=1; }
grep -q '"parser":"bsparser"' "$OUT/summary.json" "$OUT/razbor_moduley.html" && { echo "FAIL: old route marked bsparser"; fail=1; }
$OS src/Главный.os -и tests/fixtures/UP -п tests/fixtures/BP --parser нет -в "$OUTA/x" >/dev/null; [ $? -eq 1 ] || { echo "FAIL: --parser bad value"; fail=1; }
rm -rf "$OUTA"
OUTT=$(mktemp -d)
$OS src/Главный.os -и tests/fixtures/UP -п tests/fixtures/BP -н tests/fixtures/merge.xml --перенос-форм -в "$OUTT" || { echo "FAIL: main with transfer"; fail=1; }
diff -qr "$OUT/extension" "$OUTT/extension" >/dev/null || { echo "FAIL: transfer changed extension"; fail=1; }
diff -qr "$OUT/restored" "$OUTT/restored" >/dev/null || { echo "FAIL: transfer changed restored"; fail=1; }
[ -f "$OUTT/forms_transfer/forms_transfer.json" ] || { echo "FAIL: no forms_transfer.json"; fail=1; }
rm -rf "$OUTT"
# --без-отчета + --только-отчет == обычный прогон (meta.generated не сравниваем)
OUTN=$(mktemp -d)
$OS src/Главный.os -и tests/fixtures/UP -п tests/fixtures/BP -н tests/fixtures/merge.xml --без-отчета -в "$OUTN" >/dev/null || { echo "FAIL: main --без-отчета"; fail=1; }
[ -f "$OUTN/razbor_moduley.html" ] && { echo "FAIL: --без-отчета написал html"; fail=1; }
grep -q '"timings":{"readDumps":' "$OUTN/summary.json" || { echo "FAIL: summary timings"; fail=1; }
grep -q 'Время по шагам' "$OUTN/log.txt" && grep -q '\[шаг 3\]' "$OUTN/log.txt" || { echo "FAIL: log timings/progress"; fail=1; }
cp "$OUTN/summary.json" "$OUTN/summary.before"
$OS src/Главный.os --report-only -в "$OUTN" >/dev/null || { echo "FAIL: --только-отчет"; fail=1; }
cmp -s "$OUTN/summary.json" "$OUTN/summary.before" || { echo "FAIL: --только-отчет изменил summary.json"; fail=1; }
norm() { sed 's/"generated":"[^"]*"//' "$1"; }
diff -q <(norm "$OUT/razbor_moduley.html") <(norm "$OUTN/razbor_moduley.html") >/dev/null || { echo "FAIL: отчёт --только-отчет отличается"; fail=1; }
$OS src/Главный.os --только-отчет -в "$OUTN/нет" >/dev/null; [ $? -eq 1 ] || { echo "FAIL: --только-отчет без summary"; fail=1; }
rm -rf "$OUTN"
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
grep -q 'e1000001-' "$E/Configuration.xml" && { echo "FAIL: synthetic e1000001 in Configuration"; fail=1; }
[ -f "$OUT/self_check.json" ] || { echo "FAIL: no self_check.json"; fail=1; }
grep -q '"ok":true' "$OUT/self_check.json" || { echo "FAIL: self_check not ok"; cat "$OUT/self_check.json"; fail=1; }
for c in invalid_class_id duplicate_method_definitions syntax_errors extension_unresolved_local_calls cac_vs_main; do grep -q "\"id\":\"$c\",\"ok\":true" "$OUT/self_check.json" || { echo "FAIL: self_check $c"; fail=1; }; done
grep -q '9fcd25a0-4822-11d4-9414-008048da11f9' "$E/Configuration.xml" || { echo "FAIL: platform ClassId"; fail=1; }
grep -q '^Процедура Своя()\|^Функция НоваяУП()' "$E/Documents/Счет/Ext/ObjectModule.bsl" "$E/Catalogs/Банки/Ext/ObjectModule.bsl" && { echo "FAIL: новый метод скопирован повторно"; fail=1; }
grep -q '<NamePrefix>УП_</NamePrefix>' "$E/Configuration.xml" || { echo "FAIL: NamePrefix"; fail=1; }
grep -q '<CompatibilityMode>' "$E/Configuration.xml" && { echo "FAIL: extension must not have CompatibilityMode"; fail=1; }
grep -q '<ConfigurationExtensionCompatibilityMode>Version8_3_24</ConfigurationExtensionCompatibilityMode>' "$E/Configuration.xml" || { echo "FAIL: extension ConfigurationExtensionCompatibilityMode"; fail=1; }
# порядок как в выгрузках расширений: KeepMapping < ConfigurationExtensionCompatibilityMode < Version
ord=$(grep -n -o '<KeepMappingToExtendedConfigurationObjectsByIDs>\|<ConfigurationExtensionCompatibilityMode>\|<Version>' "$E/Configuration.xml" | cut -d: -f2 | tr -d '<>' | tr '\n' ' ')
[ "$ord" = "KeepMappingToExtendedConfigurationObjectsByIDs ConfigurationExtensionCompatibilityMode Version " ] || { echo "FAIL: порядок ConfigurationExtensionCompatibilityMode: $ord"; fail=1; }
grep -q 'ChildObjects' "$E/CommonModules/ХукНабор.xml" && { echo "FAIL: CommonModule ChildObjects"; fail=1; }
grep -q 'InternalInfo' "$E/CommonModules/ХукНабор.xml" && { echo "FAIL: CommonModule InternalInfo"; fail=1; }
grep -q 'xr:GeneratedType' "$E/Catalogs/Банки.xml" || { echo "FAIL: Catalog InternalInfo"; fail=1; }
grep -q '<ChildObjects>' "$E/Catalogs/Банки.xml" && grep -q '</ChildObjects>' "$E/Catalogs/Банки.xml" || { echo "FAIL: Catalog ChildObjects"; fail=1; }
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
