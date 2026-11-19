// Form.xml restored = БП; заимствованная форма — BaseForm БП + added УП с префиксом.
Перем Ошибок;

Процедура Проверить(У, С)
	Если Не У Тогда
		Сообщить("FAIL: " + С);
		Ошибок = Ошибок + 1;
	КонецЕсли;
КонецПроцедуры

Функция Записать(Путь, Текст)
	Ф = Новый Файл(Путь);
	СоздатьКаталог(Ф.Путь);
	ПолучитьДвоичныеДанныеИзСтроки(Текст, КодировкаТекста.UTF8, Ложь).Записать(Путь);
КонецФункции

Корень = ОбъединитьПути(ТекущийСценарий().Каталог, "_forms_rest_tmp");
СоздатьКаталог(Корень);
КатУ = ОбъединитьПути(Корень, "up");
КатБ = ОбъединитьПути(Корень, "bp");
КатВ = ОбъединитьПути(Корень, "restored");
СоздатьКаталог(КатУ);
СоздатьКаталог(КатБ);
СоздатьКаталог(КатВ);

ФУп = ОбъединитьПути(ОбъединитьПути(КатУ, "Catalogs\Тест\Forms\Форма\Ext"), "Form.xml");
ФБп = ОбъединитьПути(ОбъединитьПути(КатБ, "Catalogs\Тест\Forms\Форма\Ext"), "Form.xml");
Записать(ФБп, "<?xml version=""1.0"" encoding=""UTF-8""?>" + Символы.ПС
	+ "<Form xmlns=""http://v8.1c.ru/8.3/xcf/logform"" version=""2.17"">" + Символы.ПС
	+ "	<Attributes>" + Символы.ПС
	+ "		<Attribute name=""Объект"" id=""1"">" + Символы.ПС
	+ "			<Name>Объект</Name>" + Символы.ПС
	+ "			<Type><v8:Type>cfg:CatalogObject.Тест</v8:Type></Type>" + Символы.ПС
	+ "			<MainAttribute>true</MainAttribute>" + Символы.ПС
	+ "		</Attribute>" + Символы.ПС
	+ "	</Attributes>" + Символы.ПС
	+ "</Form>");

Записать(ФУп, "<?xml version=""1.0"" encoding=""UTF-8""?>" + Символы.ПС
	+ "<Form xmlns=""http://v8.1c.ru/8.3/xcf/logform"" version=""2.17"">" + Символы.ПС
	+ "	<Attributes>" + Символы.ПС
	+ "		<Attribute name=""Объект"" id=""1"">" + Символы.ПС
	+ "			<Name>Объект</Name>" + Символы.ПС
	+ "			<Type><v8:Type>cfg:CatalogObject.Тест</v8:Type></Type>" + Символы.ПС
	+ "			<MainAttribute>true</MainAttribute>" + Символы.ПС
	+ "		</Attribute>" + Символы.ПС
	+ "		<Attribute name=""ДопПоле"" id=""2"">" + Символы.ПС
	+ "			<Name>ДопПоле</Name>" + Символы.ПС
	+ "			<Type><v8:Type>xs:string</v8:Type>" + Символы.ПС
	+ "			<v8:StringQualifiers><v8:Length>10</v8:Length><v8:AllowedLength>Variable</v8:AllowedLength></v8:StringQualifiers>" + Символы.ПС
	+ "		</Attribute>" + Символы.ПС
	+ "	</Attributes>" + Символы.ПС
	+ "</Form>");

Фр = ЗагрузитьСценарий(ОбъединитьПути(ТекущийСценарий().Каталог, "..", "src", "ФормыRestored.os"));
Ошибок = 0;
Лог = Новый Массив;
Ст = Фр.ЗаписатьFormXmlБпВRestored(КатУ, КатБ, КатВ, Неопределено, Лог);
Проверить(Ст.written = 1, "restored Form.xml written");
Проверить((Новый Файл(ОбъединитьПути(ОбъединитьПути(КатВ, "Catalogs\Тест\Forms\Форма\Ext"), "Form.xml"))).Существует(), "restored file exists");

Тело = Фр.XmlТелаЗаимствованнойФормыБпУп(ФУп, ФБп, "УП_");
Проверить(СтрНайти(Тело, "<BaseForm") > 0, "BaseForm present");
Проверить(СтрНайти(Тело, "name=""УП_ДопПоле""") > 0, "prefixed attribute in extension layer");
Проверить(СтрНайти(Тело, "<MainAttribute>true</MainAttribute>") > 0, "BP main attribute in BaseForm");
Проверить(СтрДлина(Тело) > 200, "non-trivial borrowed form body");

Если Ошибок > 0 Тогда
	ВызватьИсключение "test_forms_restored: " + Ошибок + " failures";
КонецЕсли;
Сообщить("OK test_forms_restored");