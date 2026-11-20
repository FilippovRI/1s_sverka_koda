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
	ПозБф = СтрНайти(Тело, "<BaseForm");
Проверить(СтрНайти(Лев(Тело, ПозБф - 1), "<MainAttribute>true</MainAttribute>") > 0, "main attribute hoisted before BaseForm");
Проверить(СтрНайти(Сред(Тело, ПозБф), "<MainAttribute>true</MainAttribute>") = 0, "main attribute not left in BaseForm");
Проверить(СтрДлина(Тело) > 200, "non-trivial borrowed form body");

ФБпСп = ОбъединитьПути(ОбъединитьПути(КатБ, "Documents\Тест\Forms\Список\Ext"), "Form.xml");
ФУпСп = ОбъединитьПути(ОбъединитьПути(КатУ, "Documents\Тест\Forms\Список\Ext"), "Form.xml");
СоздатьКаталог(ОбъединитьПути(КатБ, "Documents\Тест\Forms\Список\Ext"));
СоздатьКаталог(ОбъединитьПути(КатУ, "Documents\Тест\Forms\Список\Ext"));
Зап = "			<Settings xsi:type=""DynamicList""><ManualQuery>true</ManualQuery></Settings>";
ТелоСп = "<?xml version=""1.0"" encoding=""UTF-8""?>" + Символы.ПС
	+ "<Form xmlns=""http://v8.1c.ru/8.3/xcf/logform"" version=""2.17"">" + Символы.ПС
	+ "	<Attributes><Attribute name=""Список"" id=""1""><Type><v8:Type>cfg:DynamicList</v8:Type></Type><MainAttribute>true</MainAttribute>" + Зап + "</Attribute></Attributes>" + Символы.ПС
	+ "</Form>";
Записать(ФБпСп, ТелоСп);
Записать(ФУпСп, ТелоСп);
ТелоСпР = Фр.XmlТелаЗаимствованнойФормыБпУп(ФУпСп, ФБпСп, "УП_");
ПозБф2 = СтрНайти(ТелоСпР, "<BaseForm");
Проверить(ПозБф2 > 0, "ManualQuery list: BaseForm");
Проверить(СтрНайти(Лев(ТелоСпР, ПозБф2 - 1), "name=""Список""") > 0, "DynamicList main hoisted to root");
Проверить(СтрНайти(Сред(ТелоСпР, ПозБф2), "<MainAttribute>true</MainAttribute>") = 0, "main not duplicated in BaseForm");

Пост = Фр.ПостобработкаИтоговогоForm("<Form><Attributes><Attribute name=""УП_Таб""><Type><v8:Type>v8:ValueTable</v8:Type></Type></Attribute></Attributes></Form>");
Проверить(СтрНайти(Пост, "v8:ValueTable") = 0, "ValueTable normalized for УП_ attr");
ПостБ = Фр.ПостобработкаИтоговогоForm("<Form><Attributes><Attribute name=""Таб""><Type><v8:Type>v8:ValueTable</v8:Type></Type></Attribute></Attributes></Form>");
Проверить(СтрНайти(ПостБ, "v8:ValueTable") > 0, "ValueTable kept for BaseForm attr");
Пост2 = Фр.ПостобработкаИтоговогоForm("<Attribute name=""УП_Зап""><Type><v8:Type>cfg:InformationRegisterRecordManager.Рег</v8:Type></Type></Attribute>");
Проверить(СтрНайти(Пост2, "InformationRegisterRecordManager") = 0, "RecordManager normalized for УП_ attr");
Пост3 = Фр.ПостобработкаИтоговогоForm("<Form><Attributes><Attribute name=""Цвет""><Type><v8:Type>v8ui:Color</v8:Type></Type><FillCheck>ShowError</FillCheck></Attribute></Attributes><BaseForm/></Form>");
Проверить(СтрНайти(Пост3, "v8ui:Color") = 0, "v8ui:Color attribute type normalized");
Проверить(СтрНайти(Пост3, "FillCheck") = 0, "FillCheck stripped in BaseForm form");
Пост4 = Фр.ПостобработкаИтоговогоForm("<Form><BaseForm><Parameters><Parameter name=""П""><Type><v8:TypeSet>cfg:DocumentRef</v8:TypeSet></Type></Parameter></Parameters></BaseForm></Form>");
Проверить(СтрНайти(Пост4, "TypeSet") = 0, "TypeSet in parameter normalized");
Пост5 = Фр.ПостобработкаИтоговогоForm("<Column><Type><v8:TypeSet>cfg:DocumentRef</v8:TypeSet><v8:TypeSet>cfg:CatalogRef</v8:TypeSet></Type></Column>");
Проверить(СтрНайти(Пост5, "TypeSet") = 0, "TypeSet in column normalized");
Пост6 = Фр.ПостобработкаИтоговогоForm("<Parameters><Parameter name=""Вид""><Type><v8:Type>cfg:EnumRef.X</v8:Type></Type></Parameter></Parameters>");
Проверить(СтрНайти(Пост6, "cfg:EnumRef") = 0, "cfg enum parameter type normalized");
Пост7 = Фр.ПостобработкаИтоговогоForm("<Form><Attributes><Attribute name=""УП_Чис""><Type><v8:Type>xs:decimal</v8:Type><v8:NumberQualifiers><v8:Digits>10</v8:Digits></v8:NumberQualifiers></Type></Attribute></Attributes><BaseForm/></Form>");
Проверить(СтрНайти(Пост7, "xs:decimal") = 0, "root УП_ decimal normalized to string");
Пост8 = Фр.ПостобработкаИтоговогоForm("<Attribute name=""А""><Type><v8:Type>cfg:A</v8:Type><v8:Type>cfg:B</v8:Type></Type></Attribute>");
Проверить(СтрНайти(Пост8, "cfg:A") = 0, "composite cfg attribute type normalized");

Если Ошибок > 0 Тогда
	ВызватьИсключение "test_forms_restored: " + Ошибок + " failures";
КонецЕсли;
Сообщить("OK test_forms_restored");