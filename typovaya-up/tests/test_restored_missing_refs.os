// ПроверкаСсылокRestored: ссылки на отсутствующие в Configuration.xml объекты до ibcmd import.
Перем Ошибок;
Процедура П(У, С)
	Если Не У Тогда Сообщить("FAIL: " + С); Ошибок = Ошибок + 1; КонецЕсли;
КонецПроцедуры
Процедура Зап(Путь, Текст)
	Ф = Новый Файл(Путь);
	СоздатьКаталог(Ф.Путь);
	ПолучитьДвоичныеДанныеИзСтроки(Текст, КодировкаТекста.UTF8, Истина).Записать(Путь);
КонецПроцедуры
Ошибок = 0;
Ск = ОбъединитьПути(ТекущийСценарий().Каталог, "..", "src");
Вр = ОбъединитьПути(КаталогВременныхФайлов(), "t_chkref_" + Строка(Новый УникальныйИдентификатор));
Зап(ОбъединитьПути(Вр, "Configuration.xml"), "<MetaDataObject><Configuration><Properties><DefaultRoles><xr:Item>Role.Р</xr:Item></DefaultRoles></Properties><ChildObjects>"
	+ Символы.ПС + "			<Catalog>А</Catalog>" + Символы.ПС + "			<Role>Р</Role>" + Символы.ПС + "			<Subsystem>П</Subsystem>" + Символы.ПС + "</ChildObjects></Configuration></MetaDataObject>");
Зап(ОбъединитьПути(Вр, "FunctionalOptions", "Фо.xml"), "<Content><xr:Object>Catalog.А</xr:Object><xr:Object>Report.Нет</xr:Object></Content>");
Зап(ОбъединитьПути(Вр, "Roles", "Р/Ext/Rights.xml"), "<Rights><object><name>Document.Нет2.Attribute.Х</name></object><object><name>Catalog.А</name></object></Rights>");
Зап(ОбъединитьПути(Вр, "Subsystems", "П.xml"), "<Content><xr:Item>Catalog.А</xr:Item></Content><ChildObjects><Subsystem>Вложенная</Subsystem></ChildObjects>");
Зап(ОбъединитьПути(Вр, "Subsystems", "П/Subsystems/Вложенная.xml"), "<Ref>Subsystem.П.Subsystem.Вложенная</Ref>");
Зап(ОбъединитьПути(Вр, "DefinedTypes", "Т.xml"), "<Type><v8:Type>cfg:CatalogRef.А</v8:Type><v8:Type>cfg:EnumRef.Нет3</v8:Type></Type>");
Зап(ОбъединитьПути(Вр, "Ext", "HomePageWorkArea.xml"), "<Item><Form>CommonForm.НетФ</Form><Url>http://report.ru/catalog.x</Url></Item>");
М = ЗагрузитьСценарий(ОбъединитьПути(Ск, "ПроверкаСсылокRestored.os"));
Лог = Новый Массив;
Р = М.Проверить(Вр, Лог, Вр);
Т = СтрСоединить(Р.missing, Символы.ПС);
П(Р.missing.Количество() = 4, "найдено " + Р.missing.Количество() + ": " + Т);
П(СтрНайти(Т, "FunctionalOptions/Фо.xml: Report.Нет") > 0 И СтрНайти(Т, "Roles/Р/Ext/Rights.xml: Document.Нет2") > 0
	И СтрНайти(Т, "DefinedTypes/Т.xml: Enum.Нет3") > 0 И СтрНайти(Т, "Ext/HomePageWorkArea.xml: CommonForm.НетФ") > 0, "места");
П(СтрНайти(Т, "Вложенная") = 0 И СтрНайти(Т, "Catalog.А") = 0 И СтрНайти(Т, "Role.Р") = 0, "без ложных");
П((Новый Файл(ОбъединитьПути(Вр, "restored_missing_refs.txt"))).Существует() И Лог.Количество() = 1, "отчёт и лог");
УдалитьФайлы(Вр);
Если Ошибок > 0 Тогда ВызватьИсключение "test_restored_missing_refs"; КонецЕсли;
Сообщить("OK: test_restored_missing_refs");
