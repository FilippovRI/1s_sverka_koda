#!/usr/bin/env bash
# E2E (синтетика): реальная генерация с #Если в типовом и доработкой, зависящей от модуля только БП.
# Проверяет: &Вместо в extension нет; тело с #Если не режется CAC и уходит на ручной разбор (доработка остаётся в restored); keptSourceBody —
# доработка осталась в restored; cac_vs_main сверяет с restored, а не со старым up-typical.
set -uo pipefail; cd "$(dirname "$0")/.."; OS=${OSCRIPT:-oscript}; T=$(mktemp -d); fail=0
cp -r tests/fixtures/UP tests/fixtures/BP "$T/"
cat >> "$T/UP/CommonModules/ХукНабор/Ext/Module.bsl" <<'B'

Функция ЭтоВеб() Экспорт
	Возврат Ложь;
КонецФункции

Функция ПериодОжидания() Экспорт
	П = 0;
	Если ЭтоВеб() Тогда
		П = 1;
	Иначе
		П = 0.1;
	КонецЕсли;
	Возврат П;
КонецФункции

Процедура СМодулемБп() Экспорт
	А = 1;
	// УП_начало
	Б = 2;
	// УП_конец
	В = 3;
КонецПроцедуры
B
cat >> "$T/BP/CommonModules/ХукНабор/Ext/Module.bsl" <<'B'

Функция ЭтоВеб() Экспорт
	Возврат Ложь;
КонецФункции

Функция ПериодОжидания() Экспорт
	П = 0;
	#Если ВебКлиент Тогда
		П = 1;
	#Иначе
		П = 0.1;
	#КонецЕсли
	Возврат П;
КонецФункции

Процедура СМодулемБп() Экспорт
	А = 1;
	ТолькоБП.Y();
	В = 3;
КонецПроцедуры
B
cp "$T/UP/CommonModules/ХукНабор.xml" "$T/BP/CommonModules/ТолькоБП.xml"
O="$T/out"; mkdir -p "$O/up-typical/CommonModules/ХукНабор/Ext"
echo "// устаревший up-typical прошлого прогона" > "$O/up-typical/CommonModules/ХукНабор/Ext/Module.bsl"
$OS src/Главный.os -и "$T/UP" -п "$T/BP" -н tests/fixtures/merge.xml --без-отчета -в "$O" >/dev/null || { echo "FAIL: e2e run"; fail=1; }
X="$O/extension/CommonModules/ХукНабор/Ext/Module.bsl"; R="$O/restored/CommonModules/ХукНабор/Ext/Module.bsl"
grep -q '&ИзменениеИКонтроль("ПериодОжидания")' "$X" && { echo "FAIL: e2e #Если разрезан CAC"; fail=1; }
grep -q '&Вместо(' "$X" && { echo "FAIL: e2e &Вместо в extension"; fail=1; }
grep -q 'ПериодОжидания' "$X" && { echo "FAIL: e2e хук для тела с #Если (должен быть ручной разбор)"; fail=1; }
grep -A8 'Функция ПериодОжидания' "$R" | grep -q 'Если ЭтоВеб() Тогда' || { echo "FAIL: e2e доработка #Если не осталась в restored"; fail=1; }
grep -q 'СМодулемБп' "$X" && { echo "FAIL: e2e хук при теле УП в restored"; fail=1; }
grep -A4 'Процедура СМодулемБп' "$R" | grep -q 'Б = 2;' || { echo "FAIL: e2e доработка СМодулемБп не осталась в restored"; fail=1; }
grep -q 'СМодулемБп — доработка УП ОСТАЛАСЬ в основной' "$O/log.txt" || { echo "FAIL: e2e лог keptSourceBody"; fail=1; }
grep -q '"keptSourceBody":1' "$O/log.txt" || { echo "FAIL: e2e счётчик keptSourceBody"; fail=1; }
grep -q '"id":"cac_vs_main","ok":true' "$O/self_check.json" || { echo "FAIL: e2e cac_vs_main"; grep -o '"id":"cac_vs_main"[^}]*' "$O/self_check.json"; fail=1; }
grep -o '"id":"cac_vs_main"[^}]*' "$O/self_check.json" | grep -q '(restored), проверено [1-9]' || { echo "FAIL: e2e cac_vs_main не по restored"; fail=1; }
rm -rf "$T"
[ $fail -eq 0 ] && echo "OK: e2e CAC applicability"; exit $fail
