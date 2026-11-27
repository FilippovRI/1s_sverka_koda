#!/usr/bin/env bash
# E2E (синтетика): реальная генерация с #Если в типовом и доработкой, зависящей от модуля только БП.
# Проверяет: &Вместо в extension нет; тело с #Если не режется CAC и уходит на ручной разбор (в restored — тело БП дословно);
# метод, чьё тело БП ссылается на модуль только БП: в restored тело БП, доработка УП — хук ИиК в extension (keptSourceBody 0);
# cac_vs_main сверяет с restored, а не со старым up-typical; --лог-проверки глушит метод БП и переписывает хук (stubbed_methods.json).
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
grep -A8 'Функция ПериодОжидания' "$R" | grep -q '#Если ВебКлиент' || { echo "FAIL: e2e в restored нет типового #Если"; fail=1; }
grep -A8 'Функция ПериодОжидания' "$R" | grep -q 'Если ЭтоВеб() Тогда' && { echo "FAIL: e2e тело УП не должно быть в restored"; fail=1; }
grep -q '&ИзменениеИКонтроль("СМодулемБп")' "$X" || { echo "FAIL: e2e нет хука ИиК для метода с модулем только БП"; fail=1; }
grep -A4 'Процедура СМодулемБп' "$R" | grep -q 'ТолькоБП.Y();' || { echo "FAIL: e2e в restored не тело БП"; fail=1; }
grep -A4 'Процедура СМодулемБп' "$R" | grep -q 'Б = 2;' && { echo "FAIL: e2e доработка УП осталась в restored"; fail=1; }
grep -q '"keptSourceBody":0' "$O/log.txt" || { echo "FAIL: e2e счётчик keptSourceBody"; fail=1; }
grep -q '"id":"cac_vs_main","ok":true' "$O/self_check.json" || { echo "FAIL: e2e cac_vs_main"; grep -o '"id":"cac_vs_main"[^}]*' "$O/self_check.json"; fail=1; }
grep -o '"id":"cac_vs_main"[^}]*' "$O/self_check.json" | grep -q '(restored), проверено [1-9]' || { echo "FAIL: e2e cac_vs_main не по restored"; fail=1; }
# заглушки методов БП по логу проверки основной
cp -r "$O/restored" "$T/load"
N=$(grep -n 'ТолькоБП.Y();' "$T/load/CommonModules/ХукНабор/Ext/Module.bsl" | head -1 | cut -d: -f1)
printf '{ОбщийМодуль.ХукНабор.Модуль(%s,2)}: Переменная не определена (ТолькоБП)\n{УП_Доработки ОбщийМодуль.ХукНабор.Модуль(3,2)}: Переменная не определена (Х)\n' "$N" > "$T/check.log"
$OS src/Главный.os -и "$T/UP" -п "$T/BP" -н tests/fixtures/merge.xml --без-отчета -в "$O" --лог-проверки "$T/check.log" --корень-проверки "$T/load" >/dev/null || { echo "FAIL: e2e run --лог-проверки"; fail=1; }
grep -A3 'Процедура СМодулемБп' "$R" | grep -q 'Заглушка typovaya-up: метод БП обращается' || { echo "FAIL: e2e метод БП не заглушен"; fail=1; }
grep -A3 'Процедура СМодулемБп' "$R" | grep -q 'ТолькоБП.Y();' && { echo "FAIL: e2e в заглушке осталось тело БП"; fail=1; }
grep -A12 '&ИзменениеИКонтроль("СМодулемБп")' "$X" | grep -q 'Заглушка typovaya-up' || { echo "FAIL: e2e хук не переписан под заглушку"; fail=1; }
grep -q '"count": *1' "$O/stubbed_methods.json" || { echo "FAIL: e2e stubbed_methods.json"; fail=1; }
rm -rf "$T"
[ $fail -eq 0 ] && echo "OK: e2e CAC applicability"; exit $fail
