// DefinedType в restored: полная копия из УП (состав УП), при отсутствии в УП — из БП.
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

Корень = ОбъединитьПути(ТекущийСценарий().Каталог, "_defined_type_tmp");
Если (Новый Файл(Корень)).Существует() Тогда
	УдалитьФайлы(Корень);
КонецЕсли;
Бп = ОбъединитьПути(Корень, "bp");
Осн = ОбъединитьПути(Корень, "main");
Уп = ОбъединитьПути(Корень, "up");
СоздатьКаталог(Бп);
СоздатьКаталог(Осн);
СоздатьКаталог(Уп);

КонфШаб = "<?xml version=""1.0"" encoding=""UTF-8""?>" + Символы.ПС
	+ "<Configuration><ChildObjects>" + Символы.ПС + Символы.Таб + Символы.Таб + Символы.Таб + "</ChildObjects></Configuration>";
Зап(ОбъединитьПути(Осн, "Configuration.xml"), КонфШаб);
Зап(ОбъединитьПути(Уп, "Configuration.xml"), КонфШаб);

ТипБп = "<?xml version=""1.0"" encoding=""UTF-8""?>" + Символы.ПС
	+ "<MetaDataObject xmlns=""http://v8.1c.ru/8.3/MDClasses""><DefinedType uuid=""dt-1111-1111-1111-111111111111"">" + Символы.ПС
	+ "	<Properties><Name>ТерриторияТест</Name>" + Символы.ПС
	+ "		<Type><v8:Type>xs:string</v8:Type><v8:StringQualifiers><v8:Length>10</v8:Length></v8:StringQualifiers></Type>" + Символы.ПС
	+ "	</Properties></DefinedType></MetaDataObject>";
ТипУп = "<?xml version=""1.0"" encoding=""UTF-8""?>" + Символы.ПС
	+ "<MetaDataObject xmlns=""http://v8.1c.ru/8.3/MDClasses""><DefinedType uuid=""dt-1111-1111-1111-111111111111"">" + Символы.ПС
	+ "	<Properties><Name>ТерриторияТест</Name>" + Символы.ПС
	+ "		<Type><v8:Type>xs:string</v8:Type><v8:StringQualifiers><v8:Length>50</v8:Length><v8:AllowedLength>Variable</v8:AllowedLength></v8:StringQualifiers></Type>" + Символы.ПС
	+ "	</Properties></DefinedType></MetaDataObject>";
Зап(ОбъединитьПути(Бп, "Configuration.xml"), СтрЗаменить(КонфШаб, Символы.Таб + Символы.Таб + Символы.Таб + "</ChildObjects>",
	Символы.Таб + Символы.Таб + Символы.Таб + "<DefinedType>ТерриторияТест</DefinedType>" + Символы.ПС + Символы.Таб + Символы.Таб + Символы.Таб + "</ChildObjects>"));
Зап(ОбъединитьПути(Бп, "DefinedTypes/ТерриторияТест.xml"), ТипБп);
Зап(ОбъединитьПути(Уп, "DefinedTypes/ТерриторияТест.xml"), ТипУп);

ФормИР = ОбъединитьПути(Осн, "InformationRegisters/РегТест/Forms/Редактирование/Ext/Form.xml");
СоздатьКаталог((Новый Файл(ФормИР)).Путь);
Зап(ФормИР, "<?xml version=""1.0""?><Form xmlns:v8=""http://v8.1c.ru/8.1/data/core"">"
	+ "<Attributes><Attribute name=""Территория""><Type><v8:Type>cfg:DefinedType.ТерриторияТест</v8:Type></Type></Attribute></Attributes></Form>");

Ошибок = 0;
Заг = ЗагрузитьСценарий(ОбъединитьПути(ТекущийСценарий().Каталог, "..", "src", "ЗаготовкиОбъектовБп.os"));
Заг.Инициализировать();
П(Заг.ПолныйВозвратDefinedType("DefinedType"), "DefinedType — отдельный полный возврат");
П(Не Заг.ПолныйВозвратТег("DefinedType"), "DefinedType не в списке БП-полного возврата");
Лог = Новый Массив;
Р = Заг.Применить(Осн, Бп, Лог, "", Уп);
ПутьТипа = ОбъединитьПути(Осн, "DefinedTypes/ТерриторияТест.xml");
П((Новый Файл(ПутьТипа)).Существует(), "DefinedType файл в restored");
Т = Новый ЧтениеТекста(ПутьТипа, КодировкаТекста.UTF8);
Тело = Т.Прочитать();
Т.Закрыть();
П(СтрНайти(Тело, "<v8:Length>50</v8:Length>") > 0, "полное тело типа из УП, не БП");
П(СтрНайти(Тело, "<v8:Length>10</v8:Length>") = 0, "нет усечённого БП");
П(Р.full >= 1, "full >= 1");

// только в БП
Зап(ОбъединитьПути(Бп, "DefinedTypes/ТолькоБП.xml"), СтрЗаменить(СтрЗаменить(ТипБп, "ТерриторияТест", "ТолькоБП"), "dt-1111-1111-1111-111111111111", "dt-2222-2222-2222-222222222222"));
ПутьФ = ОбъединитьПути(Осн, "CommonForms/ФСсылка/Ext/Form.xml");
СоздатьКаталог((Новый Файл(ПутьФ)).Путь);
Зап(ПутьФ, "<?xml version=""1.0""?><Form>"
	+ "<Attributes><Attribute name=""X""><Type><v8:Type>cfg:DefinedType.ТолькоБП</v8:Type></Type></Attribute></Attributes></Form>");
Р2 = Заг.Применить(Осн, Бп, Лог, "", Уп);
П((Новый Файл(ОбъединитьПути(Осн, "DefinedTypes/ТолькоБП.xml"))).Существует(), "тип только БП — файл из БП");
Т2 = Новый ЧтениеТекста(ОбъединитьПути(Осн, "DefinedTypes/ТолькоБП.xml"), КодировкаТекста.UTF8);
Тб = Т2.Прочитать();
Т2.Закрыть();
П(СтрНайти(Тб, "<v8:Length>10</v8:Length>") > 0, "тип только БП — состав БП");

// УП/БП: одно uuid, разные имена файла — один DefinedType с именем БП и составом УП
AliasБп = "<?xml version=""1.0"" encoding=""UTF-8""?>" + Символы.ПС
	+ "<MetaDataObject xmlns=""http://v8.1c.ru/8.3/MDClasses"">" + Символы.ПС
	+ "	<DefinedType uuid=""al-0000-0000-0000-000000000001"">" + Символы.ПС
	+ "	<InternalInfo><xr:GeneratedType name=""DefinedType.ТерриторияAlias"" category=""DefinedType"">"
	+ "<xr:TypeId>al-tid</xr:TypeId><xr:ValueId>al-vid</xr:ValueId></xr:GeneratedType></InternalInfo>" + Символы.ПС
	+ "	<Properties><Name>ТерриторияAlias</Name>"
	+ "		<Type><v8:Type>xs:string</v8:Type><v8:StringQualifiers><v8:Length>3</v8:Length></v8:StringQualifiers></Type>" + Символы.ПС
	+ "	</Properties></DefinedType></MetaDataObject>";
AliasУп = СтрЗаменить(СтрЗаменить(СтрЗаменить(СтрЗаменить(AliasБп,
	"ТерриторияAlias", "ТерриторииAlias"), "al-tid", "al-tid-up"), "al-vid", "al-vid-up"), "<v8:Length>3</v8:Length>", "<v8:Length>99</v8:Length>");
Зап(ОбъединитьПути(Бп, "DefinedTypes/ТерриторияAlias.xml"), AliasБп);
Зап(ОбъединитьПути(Уп, "DefinedTypes/ТерриторииAlias.xml"), AliasУп);
Зап(ОбъединитьПути(Осн, "DefinedTypes/ТерриторииAlias.xml"), AliasУп);
// старый дубликат по имени УП в restored
ФормА = ОбъединитьПути(Осн, "InformationRegisters/РегAlias/Forms/Форма/Ext/Form.xml");
СоздатьКаталог((Новый Файл(ФормА)).Путь);
Зап(ФормА, "<?xml version=""1.0""?><Form xmlns:v8=""http://v8.1c.ru/8.1/data/core"">"
	+ "<Attributes><Attribute name=""T""><Type><v8:Type>cfg:DefinedType.ТерриторияAlias</v8:Type></Type></Attribute></Attributes></Form>");
СпД = ОбъединитьПути(Осн, "dog_alias.txt");
Зап(СпД, "DefinedType.ТерриторияAlias");
Заг.ДополнитьЗаготовки(Осн, Бп, Лог, Уп, СпД, Истина);
ПутьА = ОбъединитьПути(Осн, "DefinedTypes/ТерриторияAlias.xml");
П((Новый Файл(ПутьА)).Существует(), "файл псевдонима БП создан");
П(Не (Новый Файл(ОбъединитьПути(Осн, "DefinedTypes/ТерриторииAlias.xml"))).Существует(), "дубликат УП-имени удалён");
Та = ПолучитьСтрокуИзДвоичныхДанных(Новый ДвоичныеДанные(ПутьА), "UTF-8");
П(СтрНайти(Та, "<v8:Length>99</v8:Length>") > 0, "псевдоним: состав УП");
П(СтрНайти(Та, "DefinedType.ТерриторияAlias") > 0, "псевдоним: имя БП в GeneratedType");
П(СтрНайти(Та, "DefinedType.ТерриторииAlias") = 0, "псевдоним: нет имени УП в GeneratedType");
Пров = ЗагрузитьСценарий(ОбъединитьПути(ТекущийСценарий().Каталог, "..", "src", "ПроверкаЗамкнутости.os"));
РИнф = Пров.ВыполнитьПроверку(Осн);
Дубль = 0;
Для Каждого Э Из РИнф.unresolved Цикл
	Если Э.class = "InternalInfoDuplicateTypeId" Или Э.class = "InternalInfoDuplicateValueId" Тогда
		Дубль = Дубль + 1;
	КонецЕсли;
КонецЦикла;
П(Дубль = 0, "после merge DefinedType: нет дубля InternalInfo");

Если Ошибок > 0 Тогда
	ВызватьИсключение "test_defined_type_from_up: " + Ошибок;
КонецЕсли;
Сообщить("OK test_defined_type_from_up");