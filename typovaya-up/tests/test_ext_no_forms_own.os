// ПроверкаРасширенияБезФорм: заимствованные формы запрещены, собственные формы собственных объектов разрешены.
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
Вр = ОбъединитьПути(КаталогВременныхФайлов(), "t_noforms_" + Строка(Новый УникальныйИдентификатор));
Зап(ОбъединитьПути(Вр, "Configuration.xml"), "<MetaDataObject><Configuration><ChildObjects><DataProcessor>Своя</DataProcessor><CommonForm>СвояФ</CommonForm></ChildObjects></Configuration></MetaDataObject>");
Зап(ОбъединитьПути(Вр, "DataProcessors", "Своя.xml"), "<MetaDataObject><DataProcessor><Properties><Name>Своя</Name></Properties></DataProcessor></MetaDataObject>");
Зап(ОбъединитьПути(Вр, "DataProcessors", "Своя/Forms/Форма.xml"), "<MetaDataObject/>");
Зап(ОбъединитьПути(Вр, "DataProcessors", "Своя/Forms/Форма/Ext/Form.xml"), "<Form/>");
Зап(ОбъединитьПути(Вр, "CommonForms", "СвояФ.xml"), "<MetaDataObject><CommonForm><Properties><Name>СвояФ</Name></Properties></CommonForm></MetaDataObject>");
Зап(ОбъединитьПути(Вр, "CommonForms", "СвояФ/Ext/Form.xml"), "<Form/>");
М = ЗагрузитьСценарий(ОбъединитьПути(Ск, "ПроверкаРасширенияБезФорм.os"));
Р = М.ПроверитьКаталог(Вр);
П(Р.ok И Р.formsDirs = 0 И Р.formExtXml = 0 И Р.ownForms = 3, "собственные формы разрешены: " + Р.formsDirs + "/" + Р.formExtXml + "/" + Р.ownForms);
Зап(ОбъединитьПути(Вр, "Catalogs", "Чуж.xml"), "<MetaDataObject><Catalog><Properties><ObjectBelonging>Adopted</ObjectBelonging><Name>Чуж</Name></Properties></Catalog></MetaDataObject>");
Зап(ОбъединитьПути(Вр, "Catalogs", "Чуж/Forms/ФормаЭлемента/Ext/Form.xml"), "<Form/>");
Р = М.ПроверитьКаталог(Вр);
П(Не Р.ok И Р.formsDirs = 1 И Р.formExtXml = 1, "заимствованная форма запрещена: " + Р.formsDirs + "/" + Р.formExtXml);
УдалитьФайлы(Вр);
Если Ошибок > 0 Тогда ВызватьИсключение "test_ext_no_forms_own"; КонецЕсли;
Сообщить("OK: test_ext_no_forms_own");
