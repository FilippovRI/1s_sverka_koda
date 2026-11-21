// Регрессия: type-soft не должен портить cfg:DynamicList + Settings.
Перем Ошибок;

Процедура Проверить(Условие, Сообщение)
	Если Не Условие Тогда
		Сообщить("FAIL: " + Сообщение);
		Ошибок = Ошибок + 1;
	КонецЕсли;
КонецПроцедуры

Ошибок = 0;
Фр = ЗагрузитьСценарий(ОбъединитьПути(ТекущийСценарий().Каталог, "..", "src", "ФормыRestored.os"));
Тип = "<Form xmlns=""http://v8.1c.ru/8.3/xcf/logform"" version=""2.17"">" + Символы.ПС
	+ "	<Attributes>" + Символы.ПС
	+ "		<Attribute name=""Список"" id=""1"">" + Символы.ПС
	+ "			<Type><v8:Type>cfg:DynamicList</v8:Type></Type>" + Символы.ПС
	+ "			<Settings xsi:type=""DynamicList"">" + Символы.ПС
	+ "				<ManualQuery>true</ManualQuery>" + Символы.ПС
	+ "			</Settings>" + Символы.ПС
	+ "		</Attribute>" + Символы.ПС
	+ "	</Attributes>" + Символы.ПС
	+ "	<ChildItems>" + Символы.ПС
	+ "		<Table name=""Список""><DataPath>Список.Поле</DataPath></Table>" + Символы.ПС
	+ "	</ChildItems>" + Символы.ПС
	+ "</Form>";
Нов = Фр.ДопостобработкаТиповIbcmd(Тип);
Проверить(СтрНайти(Нов, "cfg:DynamicList") > 0, "тип DynamicList сохранён");
Проверить(СтрНайти(Нов, "v8:ValueTable") = 0, "ValueTable не подставлен");
Проверить(СтрНайти(Нов, "xsi:type=""DynamicList""") > 0, "Settings DynamicList на месте");
Если Ошибок > 0 Тогда
	ВызватьИсключение "test_form_dynamiclist_guard: " + Ошибок;
КонецЕсли;
Сообщить("OK test_form_dynamiclist_guard");