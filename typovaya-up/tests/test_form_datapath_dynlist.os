// DataPath Список.* должен резолвиться по метаданным УП (поля основной таблицы списка).
Перем Ошибок;

Процедура П(У, С)
	Если Не У Тогда
		Сообщить("FAIL: " + С);
		Ошибок = Ошибок + 1;
	КонецЕсли;
КонецПроцедуры

Процедура ЗаписатьBom(Путь, Текст)
	СоздатьКаталог((Новый Файл(Путь)).Путь);
	ПолучитьДвоичныеДанныеИзСтроки(СтрЗаменить(Текст, Символы.ВК, ""), КодировкаТекста.UTF8NoBOM).Записать(Путь);
КонецПроцедуры

Корень = ОбъединитьПути(ТекущийСценарий().Каталог, "..");
Фикст = ОбъединитьПути(ТекущийСценарий().Каталог, "_datapath_dl_tmp");
Ошибок = 0;
Если (Новый Файл(Фикст)).Существует() Тогда
	УдалитьФайлы(Фикст);
КонецЕсли;
Ку = ОбъединитьПути(Фикст, "up");
Кб = ОбъединитьПути(Фикст, "bp");
Вс = ОбъединитьПути(Фикст, "restored");
СоздатьКаталог(Ку);
СоздатьКаталог(Кб);
СоздатьКаталог(Вс);

ЗаписатьBom(ОбъединитьПути(Ку, "Configuration.xml"), "<?xml version=""1.0""?><MetaDataObject/>");
ЗаписатьBom(ОбъединитьПути(Кб, "Configuration.xml"), "<?xml version=""1.0""?><MetaDataObject/>");
ЗаписатьBom(ОбъединитьПути(Вс, "Configuration.xml"), "<?xml version=""1.0""?><MetaDataObject/>");
Рег = "<MetaDataObject><InformationRegister><ChildObjects>"
	+ "<Dimension><Properties><Name>Повтор</Name></Properties></Dimension>"
	+ "<Resource><Properties><Name>ПовторПриАварийномЗавершении</Name></Properties></Resource>"
	+ "</ChildObjects></InformationRegister></MetaDataObject>";
ЗаписатьBom(ОбъединитьПути(Ку, "InformationRegisters", "ОбластиДанных.xml"), Рег);
ЗаписатьBom(ОбъединитьПути(Вс, "InformationRegisters", "ОбластиДанных.xml"), Рег);

Отн = "InformationRegisters\ОбластиДанных\Forms\ФормаСписка\Ext\Form.xml";
Форма = "<?xml version=""1.0""?><Form>"
	+ "<Attributes><Attribute name=""Список"" id=""1""><Type><v8:Type>cfg:DynamicList</v8:Type></Type>"
	+ "<Settings xsi:type=""DynamicList""><ManualQuery>false</ManualQuery><MainTable>InformationRegister.ОбластиДанных</MainTable></Settings></Attribute></Attributes>"
	+ "<ChildItems><InputField><DataPath>Список.КодАбонента</DataPath></InputField>"
	+ "<InputField><DataPath>Список.ПовторПриАварийномЗавершении</DataPath></InputField></ChildItems></Form>";
ЗаписатьBom(ОбъединитьПути(Кб, Отн), Форма);
ЗаписатьBom(ОбъединитьПути(Вс, Отн), "<up/>");

Пров = ЗагрузитьСценарий(ОбъединитьПути(Корень, "src", "ПроверкаЗамкнутости.os"));
Пров.ПодготовитьИндексRestored(Вс, Кб, Ку);
ТекстБп = ПолучитьСтрокуИзДвоичныхДанных(Новый ДвоичныеДанные(ОбъединитьПути(Кб, Отн)), КодировкаТекста.UTF8);
Пр = Пров.ПроблемыТекстаФормы(ТекстБп, Отн);
НашлиКод = Ложь;
Для Каждого Э Из Пр Цикл
	Если Э.class = "DataPath" И СтрНайти(Э.ref, "КодАбонента") > 0 Тогда
		НашлиКод = Истина;
	КонецЕсли;
КонецЦикла;
П(НашлиКод, "DataPath Список.КодАбонента по метаданным УП");

Если Ошибок > 0 Тогда
	ВызватьИсключение "test_form_datapath_dynlist";
КонецЕсли;
Сообщить("OK: test_form_datapath_dynlist");