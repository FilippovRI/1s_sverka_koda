// ПереносМелкихОбъектовУп (вариант 1, t724s2564): мелкие объекты только УП со ссылками на убранные ОМ только УП —
// из restored в extension собственными объектами с формами; прочее — todo (а/б).
Перем Ошибок;

Процедура П(У, С)
	Если Не У Тогда Сообщить("FAIL: " + С); Ошибок = Ошибок + 1; КонецЕсли;
КонецПроцедуры
Процедура Зап(Путь, Текст)
	Ф = Новый Файл(Путь);
	СоздатьКаталог(Ф.Путь);
	ПолучитьДвоичныеДанныеИзСтроки(Текст, КодировкаТекста.UTF8, Истина).Записать(Путь);
КонецПроцедуры
Функция Чит(Путь)
	Если Не (Новый Файл(Путь)).Существует() Тогда Возврат ""; КонецЕсли;
	Т = Новый ТекстовыйДокумент; Т.Прочитать(Путь, КодировкаТекста.UTF8); Возврат Т.ПолучитьТекст();
КонецФункции
Функция Конф(Объекты)
	Т = "<MetaDataObject>" + Символы.ПС + "	<Configuration>" + Символы.ПС + "		<ChildObjects>" + Символы.ПС;
	Для Каждого О Из СтрРазделить(Объекты, ",", Ложь) Цикл
		Ч = СтрРазделить(СокрЛП(О), ".", Ложь);
		Т = Т + "			<" + Ч[0] + ">" + Ч[1] + "</" + Ч[0] + ">" + Символы.ПС;
	КонецЦикла;
	Возврат Т + "		</ChildObjects>" + Символы.ПС + "	</Configuration>" + Символы.ПС + "</MetaDataObject>" + Символы.ПС;
КонецФункции

Ошибок = 0;
Ск = ОбъединитьПути(ТекущийСценарий().Каталог, "..", "src");
Вр = ОбъединитьПути(КаталогВременныхФайлов(), "t_small_" + Строка(Новый УникальныйИдентификатор));
Уп = ОбъединитьПути(Вр, "up");
Бп = ОбъединитьПути(Вр, "bp");
Осн = ОбъединитьПути(Вр, "restored");
Расш = ОбъединитьПути(Вр, "ext");
ПС = Символы.ВК + Символы.ПС;
Ид = "11111111-2222-3333-4444-555555555555";
Зап(ОбъединитьПути(Уп, "Configuration.xml"), Конф("CommonModule.Общ,CommonModule.УпОм,DataProcessor.ОбрУп,DataProcessor.ОбрЗапр,CommonForm.ФормаУп,Document.ДокУп,Catalog.Общий"));
Зап(ОбъединитьПути(Бп, "Configuration.xml"), Конф("CommonModule.Общ,Catalog.Общий"));
Зап(ОбъединитьПути(Осн, "Configuration.xml"), Конф("CommonModule.Общ,DataProcessor.ОбрУп,DataProcessor.ОбрЗапр,CommonForm.ФормаУп,Document.ДокУп,Catalog.Общий"));
Зап(ОбъединитьПути(Расш, "Configuration.xml"), Конф("CommonModule.УпОм"));
Для Каждого Кор Из СтрРазделить(Уп + "|" + Осн, "|") Цикл
	Зап(ОбъединитьПути(Кор, "DataProcessors", "ОбрУп.xml"), "<MetaDataObject><DataProcessor uuid=""" + Ид + """><Properties><Name>ОбрУп</Name></Properties></DataProcessor></MetaDataObject>");
	Зап(ОбъединитьПути(Кор, "DataProcessors", "ОбрУп/Forms/Форма.xml"), "<MetaDataObject><Form uuid=""aaaaaaaa-2222-3333-4444-555555555555""/></MetaDataObject>");
	Зап(ОбъединитьПути(Кор, "DataProcessors", "ОбрУп/Forms/Форма/Ext/Form.xml"), "<Form><Attributes><Attribute><Settings><QueryText>ВЫБРАТЬ Д.Ссылка ИЗ Документ.ДокУп КАК Д ЛЕВОЕ СОЕДИНЕНИЕ РегистрСведений.РегОбщ КАК Р ПО ИСТИНА</QueryText><MainTable>Document.ДокУп</MainTable></Settings></Attribute></Attributes>"
		+ "<v8:Type>cfg:CatalogRef.Общий</v8:Type><v8:Type>cfg:DataProcessorObject.ОбрУп</v8:Type></Form>");
	Зап(ОбъединитьПути(Кор, "DataProcessors", "ОбрУп/Forms/Форма/Ext/Form/Module.bsl"), "Процедура А()" + ПС + "	// УпОм.Нет();" + ПС + "	УпОм.Х();" + ПС + "КонецПроцедуры" + ПС);
	Зап(ОбъединитьПути(Кор, "DataProcessors", "ОбрЗапр.xml"), "<MetaDataObject><DataProcessor uuid=""eeeeeeee-2222-3333-4444-555555555555""><Properties><Name>ОбрЗапр</Name></Properties></DataProcessor></MetaDataObject>");
	Зап(ОбъединитьПути(Кор, "DataProcessors", "ОбрЗапр/Forms/Список/Ext/Form.xml"), "<Form><Settings><ManualQuery>true</ManualQuery><QueryText>ВЫБРАТЬ 1 ИЗ Документ.ДокУп</QueryText></Settings></Form>");
	Зап(ОбъединитьПути(Кор, "DataProcessors", "ОбрЗапр/Forms/Список/Ext/Form/Module.bsl"), "Процедура Д()" + ПС + "	УпОм.Х();" + ПС + "КонецПроцедуры" + ПС);
	Зап(ОбъединитьПути(Кор, "CommonForms", "ФормаУп.xml"), "<MetaDataObject><CommonForm><Properties><Name>ФормаУп</Name></Properties></CommonForm></MetaDataObject>");
	Зап(ОбъединитьПути(Кор, "CommonForms", "ФормаУп/Ext/Form/Module.bsl"), "Процедура Б()" + ПС + "	Общ.Х(); // УпОм.Х()" + ПС + "КонецПроцедуры" + ПС);
	Зап(ОбъединитьПути(Кор, "Documents", "ДокУп.xml"), "<MetaDataObject><Document><Properties><Name>ДокУп</Name></Properties></Document></MetaDataObject>");
	Зап(ОбъединитьПути(Кор, "Documents", "ДокУп/Ext/ObjectModule.bsl"), "Процедура В()" + ПС + "	упом.Х();" + ПС + "КонецПроцедуры" + ПС);
КонецЦикла;
Зап(ОбъединитьПути(Осн, "InformationRegisters", "РегОбщ.xml"), "<MetaDataObject><InformationRegister uuid=""dddddddd-2222-3333-4444-555555555555""><Properties><Name>РегОбщ</Name></Properties></InformationRegister></MetaDataObject>");
Зап(ОбъединитьПути(Осн, "Catalogs", "Общий.xml"), "<MetaDataObject><Catalog uuid=""cccccccc-2222-3333-4444-555555555555""><Properties><Name>Общий</Name></Properties></Catalog></MetaDataObject>");
Зап(ОбъединитьПути(Осн, "Catalogs", "Общий/Forms/ФормаЭлемента/Ext/Form/Module.bsl"), "Процедура Г()" + ПС + "	Общ.Х();" + ПС + "	УпОм.Ф();" + ПС + "КонецПроцедуры" + ПС);
Зап(ОбъединитьПути(Осн, "ConfigDumpInfo.xml"), "<ConfigDumpInfo>" + Символы.ПС + "		<Metadata name=""DataProcessor.ОбрУп"" id=""1""/>" + Символы.ПС
	+ "		<Metadata name=""DataProcessor.ОбрУп.Form.Форма"" id=""2""/>" + Символы.ПС + "		<Metadata name=""Document.ДокУп"" id=""3""/>" + Символы.ПС + "</ConfigDumpInfo>");
Зап(ОбъединитьПути(Осн, "Subsystems", "Подс.xml"), "<Content>" + Символы.ПС + "<xr:Item xsi:type=""xr:MDObjectRef"">DataProcessor.ОбрУп</xr:Item>" + Символы.ПС
	+ "<xr:Item xsi:type=""xr:MDObjectRef"">Document.ДокУп</xr:Item>" + Символы.ПС + "</Content>");
Зап(ОбъединитьПути(Осн, "Roles", "Роль/Ext/Rights.xml"), "<Rights>" + Символы.ПС + "	<setForNewObjects>false</setForNewObjects>" + Символы.ПС
	+ "	<object>" + Символы.ПС + "		<name>DataProcessor.ОбрУп</name>" + Символы.ПС + "		<right><name>Use</name><value>true</value></right>" + Символы.ПС + "	</object>" + Символы.ПС
	+ "	<object>" + Символы.ПС + "		<name>Document.ДокУп</name>" + Символы.ПС + "	</object>" + Символы.ПС + "</Rights>");
Зап(ОбъединитьПути(Осн, "FunctionalOptions", "Фо.xml"), "<Content>" + Символы.ПС + "	<xr:Object>DataProcessor.ОбрУп</xr:Object>" + Символы.ПС
	+ "	<xr:Object>Document.ДокУп</xr:Object>" + Символы.ПС + "</Content>");
Зап(ОбъединитьПути(Осн, "Ext", "HomePageWorkArea.xml"), "<Items>" + Символы.ПС + "	<Item>" + Символы.ПС + "		<Form>DataProcessor.ОбрУп.Form.Форма</Form>" + Символы.ПС
	+ "		<Height>10</Height>" + Символы.ПС + "	</Item>" + Символы.ПС + "	<Item>" + Символы.ПС + "		<Form>CommonForm.ФормаУп</Form>" + Символы.ПС + "	</Item>" + Символы.ПС + "</Items>");
М = ЗагрузитьСценарий(ОбъединитьПути(Ск, "ПереносМелкихОбъектовУп.os"));
Лог = Новый Массив;
Р = М.Применить(Расш, Осн, Уп, Бп, "УП_Доработки", Лог, Вр);
ТК = Чит(ОбъединитьПути(Осн, "Configuration.xml"));
ТР = Чит(ОбъединитьПути(Расш, "Configuration.xml"));
П(Р.moved.Количество() = 1 И Р.moved[0] = "DataProcessor.ОбрУп", "перенесён только ОбрУп: " + СтрСоединить(Р.moved, ","));
П(СтрНайти(ТК, "ОбрУп") = 0 И СтрНайти(ТК, "<Document>ДокУп</Document>") > 0 И СтрНайти(ТК, "<CommonForm>ФормаУп</CommonForm>") > 0, "Configuration.xml restored");
П(Не (Новый Файл(ОбъединитьПути(Осн, "DataProcessors", "ОбрУп.xml"))).Существует() И Не (Новый Файл(ОбъединитьПути(Осн, "DataProcessors", "ОбрУп"))).Существует(), "файлы restored убраны");
П(СтрНайти(ТР, "<DataProcessor>ОбрУп</DataProcessor>") > 0, "Configuration.xml extension");
ХР = Чит(ОбъединитьПути(Расш, "DataProcessors", "ОбрУп.xml"));
П(ХР <> "" И СтрНайти(ХР, Ид) = 0 И СтрНайти(ХР, "Adopted") = 0, "extension: собственный объект с новым uuid");
П(СтрНайти(Чит(ОбъединитьПути(Расш, "DataProcessors", "ОбрУп/Forms/Форма/Ext/Form/Module.bsl")), "УпОм.Х();") > 0, "extension: форма с модулем");
Д = Чит(ОбъединитьПути(Осн, "ConfigDumpInfo.xml"));
П(СтрНайти(Д, "ОбрУп") = 0 И СтрНайти(Д, "Document.ДокУп") > 0 И Р.dumpInfoLines = 2, "ConfigDumpInfo: " + Р.dumpInfoLines);
П(СтрНайти(Чит(ОбъединитьПути(Осн, "Subsystems", "Подс.xml")), "ОбрУп") = 0 И Р.subsystemLines = 1, "подсистема");
ТП = Чит(ОбъединитьПути(Осн, "Roles", "Роль/Ext/Rights.xml"));
П(СтрНайти(ТП, "ОбрУп") = 0 И СтрНайти(ТП, "Document.ДокУп") > 0 И Р.rolesCleaned = 1, "права роли");
ХД = Чит(ОбъединитьПути(Расш, "Documents", "ДокУп.xml"));
П(СтрНайти(ХД, "<ObjectBelonging>Adopted</ObjectBelonging>") > 0 И СтрНайти(ТР, "<Document>ДокУп</Document>") > 0
	И СтрНайти(Чит(ОбъединитьПути(Расш, "Catalogs", "Общий.xml")), "cccccccc-2222-3333-4444-555555555555") > 0 И (Новый Файл(ОбъединитьПути(Расш, "InformationRegisters", "РегОбщ.xml"))).Существует() И Р.adopted = 3, "заимствованы главная таблица и тип: " + Р.adopted);
П(Не (Новый Файл(ОбъединитьПути(Расш, "DataProcessors", "ОбрУп.xml"))).Существует() = Ложь, "перенесённый объект не заимствуется");
ТФо = Чит(ОбъединитьПути(Осн, "FunctionalOptions", "Фо.xml"));
П(СтрНайти(ТФо, "ОбрУп") = 0 И СтрНайти(ТФо, "Document.ДокУп") > 0 И Р.functionalOptions = 1, "функциональная опция");
ТНс = Чит(ОбъединитьПути(Осн, "Ext", "HomePageWorkArea.xml"));
П(СтрНайти(ТНс, "ОбрУп") = 0 И СтрНайти(ТНс, "<Height>") = 0 И СтрНайти(ТНс, "CommonForm.ФормаУп") > 0 И СтрЧислоВхождений(ТНс, "<Item>") = 1, "начальная страница: блок Item убран");
Csv = Чит(ОбъединитьПути(Вр, "todo_uponly_cm_refs.csv"));
П(СтрНайти(Csv, "а;Document.ДокУп;Documents/ДокУп/Ext/ObjectModule.bsl;2;упом;") > 0, "todo (а) документ");
П(СтрНайти(Csv, "б;Catalog.Общий;Catalogs/Общий/Forms/ФормаЭлемента/Ext/Form/Module.bsl;3;УпОм;") > 0, "todo (б) форма общего объекта");
П(Не (Новый Файл(ОбъединитьПути(Расш, "DataProcessors", "ОбрЗапр.xml"))).Существует() И СтрНайти(ТК, "<DataProcessor>ОбрЗапр</DataProcessor>") > 0
	И (Новый Файл(ОбъединитьПути(Осн, "DataProcessors", "ОбрЗапр.xml"))).Существует() И Р.skippedQuery.Количество() = 1, "форма с произвольным запросом — объект не переносится");
П(СтрНайти(Csv, "а;DataProcessor.ОбрЗапр;DataProcessors/ОбрЗапр/Forms/Список/Ext/Form/Module.bsl;2;УпОм;мелкий объект только УП не перенесён") > 0, "todo (а) не перенесённый объект");
П(Р.todoA = 2 И Р.todoB = 1 И СтрНайти(Csv, "ФормаУп") = 0 И СтрНайти(Csv, "ОбрУп") = 0, "todo без лишних: " + Р.todoA + "/" + Р.todoB);
П(СтрНайти(Чит(ОбъединитьПути(Вр, "todo_uponly_cm_refs.md")), "DataProcessor.ОбрУп") > 0, "todo md: перенесённые");
// повтор: extension тот же, restored не меняется
Р2 = М.Применить(Расш, Осн, Уп, Бп, "УП_Доработки", Лог, Вр);
П(Чит(ОбъединитьПути(Расш, "DataProcessors", "ОбрУп.xml")) = ХР И Чит(ОбъединитьПути(Осн, "Configuration.xml")) = ТК
	И СтрЧислоВхождений(Чит(ОбъединитьПути(Расш, "Configuration.xml")), "<DataProcessor>ОбрУп</DataProcessor>") = 1, "повтор идемпотентен");
УдалитьФайлы(Вр);
Для Каждого С Из Лог Цикл Сообщить(С); КонецЦикла;
Если Ошибок > 0 Тогда ВызватьИсключение "test_small_uponly_objects"; КонецЕсли;
Сообщить("OK: test_small_uponly_objects");
