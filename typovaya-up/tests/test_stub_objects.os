Перем Ошибок;

Процедура П(У, С)
	Если Не У Тогда
		Сообщить("FAIL: " + С);
		Ошибок = Ошибок + 1;
	КонецЕсли;
КонецПроцедуры

Функция Зап(Путь, Текст)
	СоздатьКаталог((Новый Файл(Путь)).Путь);
	ПолучитьДвоичныеДанныеИзСтроки(Текст, КодировкаТекста.UTF8, Ложь).Записать(Путь);
КонецФункции

Корень = ОбъединитьПути(ТекущийСценарий().Каталог, "_stub_tmp");
Если (Новый Файл(Корень)).Существует() Тогда
	УдалитьФайлы(Корень);
КонецЕсли;
Бп = ОбъединитьПути(Корень, "bp");
Осн = ОбъединитьПути(Корень, "main");
СоздатьКаталог(Бп);
СоздатьКаталог(Осн);

Конф = "<?xml version=""1.0"" encoding=""UTF-8""?>" + Символы.ПС
	+ "<Configuration><ChildObjects>" + Символы.ПС + Символы.Таб + Символы.Таб + Символы.Таб + "</ChildObjects></Configuration>";
Зап(ОбъединитьПути(Бп, "Configuration.xml"), Конф);
Зап(ОбъединитьПути(Осн, "Configuration.xml"), Конф);

КатУ = "<?xml version=""1.0"" encoding=""UTF-8""?>" + Символы.ПС + "<MetaDataObject version=""2.21"">" + Символы.ПС
	+ "<Catalog uuid=""11111111-1111-1111-1111-111111111111"">" + Символы.ПС
	+ "	<InternalInfo><xr:GeneratedType name=""CatalogRef.ТестСправ"" category=""Ref""><xr:TypeId>a</xr:TypeId><xr:ValueId>b</xr:ValueId></xr:GeneratedType></InternalInfo>" + Символы.ПС
	+ "	<Properties><Name>ТестСправ</Name><Synonym><v8:item><v8:lang>ru</v8:lang><v8:content>Т</v8:content></v8:item></Synonym>" + Символы.ПС
	+ "	<DefaultListForm>Catalog.ТестСправ.Form.Ф</DefaultListForm><Owners><xr:Item><xr:Ref>Catalog.Влад</xr:Ref></xr:Item></Owners></Properties>" + Символы.ПС
	+ "	<ChildObjects><Attribute uuid=""x""><Properties><Name>X</Name></Properties></Attribute></ChildObjects>" + Символы.ПС
	+ "</Catalog></MetaDataObject>";
Зап(ОбъединитьПути(Бп, "Catalogs\ТестСправ.xml"), КатУ);

Форм = ОбъединитьПути(Осн, "Catalogs\Ном\Forms\Ф\Ext\Form.xml");
СоздатьКаталог((Новый Файл(Форм)).Путь);
Зап(Форм, "<Form><Attributes><Attribute name=""A""><Type><v8:Type>cfg:CatalogRef.ТестСправ</v8:Type></Type></Attribute></Attributes></Form>");

Ошибок = 0;
Заг = ЗагрузитьСценарий(ОбъединитьПути(ТекущийСценарий().Каталог, "..", "src", "ЗаготовкиОбъектовБп.os"));
Лог = Новый Массив;
Р = Заг.Применить(Осн, Бп, Лог);
П = ОбъединитьПути(Осн, "Catalogs\ТестСправ.xml");
П((Новый Файл(П)).Существует(), "stub file");
Вб = ЗагрузитьСценарий(ОбъединитьПути(ТекущийСценарий().Каталог, "..", "src", "ВозвратОбщихМодулейБп.os"));
Т = Вб.ПрочитатьТекст(П);
П(СтрНайти(Т, "<Attribute") = 0, "no attributes in stub");
П(СтрНайти(Т, "DefaultListForm") = 0, "forms refs stripped");
П(Р.stubs >= 1, "stub count");

Если Ошибок > 0 Тогда
	ВызватьИсключение "test_stub_objects";
КонецЕсли;
Сообщить("OK test_stub_objects");