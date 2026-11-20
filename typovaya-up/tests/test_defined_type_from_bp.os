// DefinedType из БП: полная копия в restored при ссылке cfg: из формы/реквизита (XDTO Type).
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
	+ "		<Type><v8:Type>xs:string</v8:Type><v8:StringQualifiers><v8:Length>50</v8:Length><v8:AllowedLength>Variable</v8:AllowedLength></v8:StringQualifiers></Type>" + Символы.ПС
	+ "	</Properties></DefinedType></MetaDataObject>";
Зап(ОбъединитьПути(Бп, "Configuration.xml"), СтрЗаменить(КонфШаб, Символы.Таб + Символы.Таб + Символы.Таб + "</ChildObjects>",
	Символы.Таб + Символы.Таб + Символы.Таб + "<DefinedType>ТерриторияТест</DefinedType>" + Символы.ПС + Символы.Таб + Символы.Таб + Символы.Таб + "</ChildObjects>"));
Зап(ОбъединитьПути(Бп, "DefinedTypes\ТерриторияТест.xml"), ТипБп);

// форма регистра (как РедактированиеИстории)
ФормИР = ОбъединитьПути(Осн, "InformationRegisters\РегТест\Forms\Редактирование\Ext\Form.xml");
СоздатьКаталог((Новый Файл(ФормИР)).Путь);
Зап(ФормИР, "<?xml version=""1.0""?><Form xmlns:v8=""http://v8.1c.ru/8.1/data/core"">"
	+ "<Attributes><Attribute name=""Территория""><Type><v8:Type>cfg:DefinedType.ТерриторияТест</v8:Type></Type></Attribute></Attributes></Form>");

// общая форма с тем же типом
ФормОб = ОбъединитьПути(Осн, "CommonForms\ПодробнееТест\Ext\Form.xml");
СоздатьКаталог((Новый Файл(ФормОб)).Путь);
Зап(ФормОб, "<?xml version=""1.0""?><Form xmlns:v8=""http://v8.1c.ru/8.1/data/core"">"
	+ "<Attributes><Attribute name=""Поле""><Type><v8:Type>cfg:DefinedType.ТерриторияТест</v8:Type></Type></Attribute></Attributes></Form>");

// реквизит объекта без формы
Зап(ОбъединитьПути(Осн, "Catalogs\СпрТест.xml"), "<?xml version=""1.0""?><MetaDataObject><Catalog uuid=""c1"">"
	+ "<ChildObjects><Attribute uuid=""a1""><Properties><Name>РекТер</Name>"
	+ "<Type><v8:Type>cfg:DefinedType.ТерриторияТест</v8:Type></Type></Properties></Attribute></ChildObjects></Catalog></MetaDataObject>");

Ошибок = 0;
Заг = ЗагрузитьСценарий(ОбъединитьПути(ТекущийСценарий().Каталог, "..", "src", "ЗаготовкиОбъектовБп.os"));
Заг.Инициализировать();
П(Заг.ПолныйВозвратТег("DefinedType"), "DefinedType — полный возврат");
Лог = Новый Массив;
Р = Заг.Применить(Осн, Бп, Лог, "", Уп);
ПутьТипа = ОбъединитьПути(Осн, "DefinedTypes\ТерриторияТест.xml");
П((Новый Файл(ПутьТипа)).Существует(), "DefinedType файл в restored");
Т = Новый ЧтениеТекста(ПутьТипа, КодировкаТекста.UTF8);
Тело = Т.Прочитать();
Т.Закрыть();
П(СтрНайти(Тело, "<v8:Type>xs:string</v8:Type>") > 0, "полное тело типа из БП");
П(Р.full >= 1, "full >= 1");
ТК = Новый ЧтениеТекста(ОбъединитьПути(Осн, "Configuration.xml"), КодировкаТекста.UTF8);
Конф = ТК.Прочитать();
ТК.Закрыть();
П(СтрНайти(Конф, "<DefinedType>ТерриторияТест</DefinedType>") > 0, "DefinedType в Configuration.xml");

Если Ошибок > 0 Тогда
	ВызватьИсключение "test_defined_type_from_bp: " + Ошибок;
КонецЕсли;
Сообщить("OK test_defined_type_from_bp");