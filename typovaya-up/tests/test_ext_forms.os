// Юнит-тесты задачи 9: стандартные команды заимствованных форм со списком и заимствование полей
// динамических списков собственных форм расширения.
Перем Ошибок;

Процедура Проверить(Условие, Сообщение)
	Если Не Условие Тогда
		Сообщить("FAIL: " + Сообщение);
		Ошибок = Ошибок + 1;
	КонецЕсли;
КонецПроцедуры

Процедура Записать(Путь, Текст)
	Ф = Новый Файл(Путь);
	СоздатьКаталог(Ф.Путь);
	ПолучитьДвоичныеДанныеИзСтроки(Текст, КодировкаТекста.UTF8, Ложь).Записать(Путь);
КонецПроцедуры

Функция Прочитать(Путь)
	Возврат ПолучитьСтрокуИзДвоичныхДанных(Новый ДвоичныеДанные(Путь), "UTF-8");
КонецФункции

Функция Строки(Массив)
	Возврат Символы.ПС + СтрСоединить(Массив, Символы.ПС) + Символы.ПС;
КонецФункции

Функция ФормаИсточник(ТипГлавного)
	Возврат "<?xml version=""1.0"" encoding=""UTF-8""?>
	|<Form xmlns=""http://v8.1c.ru/8.3/xcf/logform"" version=""2.17"">
	|	<CommandSet>
	|		<ExcludedCommand>Close</ExcludedCommand>
	|		<ExcludedCommand>Create</ExcludedCommand>
	|	</CommandSet>
	|	<AutoCommandBar name=""ФормаКоманднаяПанель"" id=""-1""/>
	|	<Attributes>
	|		<Attribute name=""Список"" id=""1"">
	|			<Type>
	|				<v8:Type>" + ТипГлавного + "</v8:Type>
	|			</Type>
	|			<MainAttribute>true</MainAttribute>
	|		</Attribute>
	|	</Attributes>
	|</Form>
	|";
КонецФункции

Функция ФормаЗаимствованная()
	Возврат "<?xml version=""1.0"" encoding=""UTF-8""?>
	|<Form xmlns=""http://v8.1c.ru/8.3/xcf/logform"" version=""2.17"">
	|	<CommandSet>
	|		<ExcludedCommand>Create</ExcludedCommand>
	|		<ExcludedCommand>Close</ExcludedCommand>
	|	</CommandSet>
	|	<AutoCommandBar name=""ФормаКоманднаяПанель"" id=""-1"">
	|		<Autofill>false</Autofill>
	|	</AutoCommandBar>
	|	<Attributes/>
	|	<BaseForm version=""2.17"">
	|		<CommandSet>
	|			<ExcludedCommand>Create</ExcludedCommand>
	|			<ExcludedCommand>Close</ExcludedCommand>
	|		</CommandSet>
	|		<AutoCommandBar name=""ФормаКоманднаяПанель"" id=""-1"">
	|			<Autofill>false</Autofill>
	|		</AutoCommandBar>
	|		<Attributes/>
	|	</BaseForm>
	|</Form>
	|";
КонецФункции

Функция СобственнаяФорма()
	Возврат "<?xml version=""1.0"" encoding=""UTF-8""?>
	|<Form xmlns=""http://v8.1c.ru/8.3/xcf/logform"" version=""2.17"">
	|	<ChildItems>
	|		<Table name=""Список"" id=""2"">
	|			<CommandSet><ExcludedCommand>Form.Item.Список.StandardCommand.Delete</ExcludedCommand></CommandSet>
	|			<ChildItems>
	|				<InputField name=""Дата"" id=""3""><DataPath>Список.ДатаОтправки</DataPath></InputField>
	|				<Button name=""Кн"" id=""4""><CommandName>Form.Item.Пропавший.StandardCommand.Change</CommandName></Button>
	|			</ChildItems>
	|		</Table>
	|	</ChildItems>
	|	<Attributes>
	|		<Attribute name=""Список"" id=""1"">
	|			<Type><v8:Type>cfg:DynamicList</v8:Type></Type>
	|			<MainAttribute>true</MainAttribute>
	|			<Settings xsi:type=""DynamicList"">
	|				<ManualQuery>true</ManualQuery>
	|				<QueryText>ВЫБРАТЬ
	|	Т.ДатаОтправки,
	|	Т.СтатусОтправки,
	|	Т.Строка
	|ИЗ
	|	Справочник.Отправки КАК Т
	|ГДЕ
	|	Т.СтатусОтправки = ЗНАЧЕНИЕ(Перечисление.Статусы.Сдан)</QueryText>
	|				<MainTable>Catalog.Отправки</MainTable>
	|				<ListSettings/>
	|			</Settings>
	|		</Attribute>
	|	</Attributes>
	|</Form>
	|";
КонецФункции

Функция КаталогИсточник()
	Возврат "<?xml version=""1.0"" encoding=""UTF-8""?>
	|<MetaDataObject xmlns=""http://v8.1c.ru/8.3/MDClasses"">
	|	<Catalog uuid=""11111111-1111-1111-1111-111111111111"">
	|		<Properties>
	|			<Name>Отправки</Name>
	|		</Properties>
	|		<ChildObjects>
	|			<Attribute uuid=""22222222-2222-2222-2222-222222222222"">
	|				<Properties>
	|					<Name>ДатаОтправки</Name>
	|					<Type>
	|						<v8:Type>xs:dateTime</v8:Type>
	|					</Type>
	|				</Properties>
	|			</Attribute>
	|			<Attribute uuid=""33333333-3333-3333-3333-333333333333"">
	|				<Properties>
	|					<Name>СтатусОтправки</Name>
	|					<Type>
	|						<v8:Type>cfg:EnumRef.Статусы</v8:Type>
	|					</Type>
	|				</Properties>
	|			</Attribute>
	|			<TabularSection uuid=""44444444-4444-4444-4444-444444444444"">
	|				<Properties><Name>ТЧ</Name></Properties>
	|				<ChildObjects>
	|					<Attribute uuid=""55555555-5555-5555-5555-555555555555"">
	|						<Properties><Name>Строка</Name><Type><v8:Type>xs:string</v8:Type></Type></Properties>
	|					</Attribute>
	|				</ChildObjects>
	|			</TabularSection>
	|			<Form>ФормаСписка</Form>
	|		</ChildObjects>
	|	</Catalog>
	|</MetaDataObject>
	|";
КонецФункции

Функция ПеречислениеИсточник()
	Возврат "<?xml version=""1.0"" encoding=""UTF-8""?>
	|<MetaDataObject xmlns=""http://v8.1c.ru/8.3/MDClasses"">
	|	<Enum uuid=""66666666-6666-6666-6666-666666666666"">
	|		<Properties>
	|			<Name>Статусы</Name>
	|		</Properties>
	|		<ChildObjects>
	|			<EnumValue uuid=""77777777-7777-7777-7777-777777777777"">
	|				<Properties><Name>Отправлен</Name></Properties>
	|			</EnumValue>
	|			<EnumValue uuid=""88888888-8888-8888-8888-888888888888"">
	|				<Properties><Name>Сдан</Name></Properties>
	|			</EnumValue>
	|		</ChildObjects>
	|	</Enum>
	|</MetaDataObject>
	|";
КонецФункции

Функция КаталогРасширения()
	Возврат "<?xml version=""1.0"" encoding=""UTF-8""?>
	|<MetaDataObject xmlns=""http://v8.1c.ru/8.3/MDClasses"">
	|	<Catalog uuid=""99999999-9999-9999-9999-999999999999"">
	|		<Properties>
	|			<ObjectBelonging>Adopted</ObjectBelonging>
	|			<Name>Отправки</Name>
	|			<ExtendedConfigurationObject>11111111-1111-1111-1111-111111111111</ExtendedConfigurationObject>
	|		</Properties>
	|		<ChildObjects>
	|			<Form>ФормаСписка</Form>
	|		</ChildObjects>
	|	</Catalog>
	|</MetaDataObject>
	|";
КонецФункции

Функция КонфигурацияРасширения()
	Возврат "<?xml version=""1.0"" encoding=""UTF-8""?>
	|<MetaDataObject xmlns=""http://v8.1c.ru/8.3/MDClasses"">
	|	<Configuration uuid=""aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"">
	|		<Properties>
	|			<Name>Тест</Name>
	|		</Properties>
	|		<ChildObjects>
	|			<Language>Русский</Language>
	|			<Catalog>Отправки</Catalog>
	|			<DataProcessor>Обр</DataProcessor>
	|		</ChildObjects>
	|	</Configuration>
	|</MetaDataObject>
	|";
КонецФункции

Ошибок = 0;
Корень = ОбъединитьПути(ТекущийСценарий().Каталог, "..");
Ч = ЗагрузитьСценарий(ОбъединитьПути(Корень, "src", "ЧерновикРасширения.os"));
З = ЗагрузитьСценарий(ОбъединитьПути(Корень, "src", "ЗаимствованиеПолейСписков.os"));
З.УстановитьЧерновик(Ч);

// 1. Команды списка в заимствованной форме без главного реквизита
Проверить(Ч.ТипГлавногоРеквизитаФормы(ФормаИсточник("cfg:DynamicList")) = "cfg:DynamicList", "тип главного реквизита DynamicList");
Проверить(Ч.ТипГлавногоРеквизитаФормы(ФормаЗаимствованная()) = "", "нет главного реквизита в заимствованной");
Р = Ч.ОчиститьКомандыСпискаЗаимствованной(ФормаЗаимствованная(), ФормаИсточник("cfg:DynamicList"));
Проверить(Р.Удалено = 2, "удалено Create в обеих частях: " + Р.Удалено);
Проверить(СтрНайти(Р.Текст, ">Create<") = 0, "Create удалён");
Проверить(СтрЧислоВхождений(Р.Текст, "<ExcludedCommand>Close</ExcludedCommand>") = 2, "Close сохранён в обеих частях");
Р2 = Ч.ОчиститьКомандыСпискаЗаимствованной(ФормаЗаимствованная(), ФормаИсточник("cfg:CatalogObject.Отправки"));
Проверить(Р2.Удалено = 0, "форма объекта: команды не трогаются");
Р3 = Ч.ОчиститьКомандыСпискаЗаимствованной(СтрЗаменить(ФормаЗаимствованная(), Символы.Таб + Символы.Таб + "<ExcludedCommand>Close</ExcludedCommand>" + Символы.ПС, ""), ФормаИсточник("cfg:DynamicList"));
Проверить(СтрНайти(Р3.Текст, "<CommandSet>") = 0, "пустой CommandSet удалён");

Тмп = ПолучитьИмяВременногоФайла("t9");
Исх = ОбъединитьПути(Тмп, "up");
Расш = ОбъединитьПути(Тмп, "ext");
Записать(Исх + "/Catalogs/Отправки/Forms/ФормаСписка/Ext/Form.xml", ФормаИсточник("cfg:DynamicList"));
Записать(ОбъединитьПути(Исх, "Catalogs", "Отправки.xml"), КаталогИсточник());
Записать(ОбъединитьПути(Исх, "Enums", "Статусы.xml"), ПеречислениеИсточник());

// генератор: тело заимствованной формы со списком без команд списка
Тело = Ч.XmlТелаЗаимствованнойФормы(Исх + "/Catalogs/Отправки/Forms/ФормаСписка/Ext/Form.xml");
Проверить(СтрНайти(Тело, ">Create<") = 0 И СтрЧислоВхождений(Тело, ">Close<") = 2, "генератор: Create не попал в заимствованную форму");

Записать(ОбъединитьПути(Расш, "Configuration.xml"), КонфигурацияРасширения());
Записать(ОбъединитьПути(Расш, "Catalogs", "Отправки.xml"), КаталогРасширения());
Записать(Расш + "/Catalogs/Отправки/Forms/ФормаСписка/Ext/Form.xml", ФормаЗаимствованная());
Записать(Расш + "/DataProcessors/Обр/Forms/Ф/Ext/Form.xml", СобственнаяФорма());

// 2. самопроверка команд
Н = Ч.НевалидныеКомандыЗаимствованных(Расш, Исх);
Проверить(Н.Количество() = 2, "самопроверка: Create и Form.Item.Пропавший" + Строки(Н));
Проверить(СтрНайти(СтрСоединить(Н, "|"), "Create (нет главного") > 0, "найден Create");
Проверить(СтрНайти(СтрСоединить(Н, "|"), "нет элемента Пропавший") > 0, "найден элемент Пропавший");
Проверить(СтрНайти(СтрСоединить(Н, "|"), "Список.StandardCommand") = 0, "элемент Список есть — не ошибка");

// 3. поля динсписков собственных форм: проверка без записи
До = Прочитать(ОбъединитьПути(Расш, "Catalogs", "Отправки.xml"));
П = З.Применить(Расш, Исх, "Тест", Ложь);
Все = СтрСоединить(П.missing, "|");
Проверить(П.forms.Количество() = 1, "одна собственная форма со списком");
Проверить(СтрНайти(Все, "Attribute Catalog.Отправки.ДатаОтправки") > 0, "нужен ДатаОтправки" + Строки(П.missing));
Проверить(СтрНайти(Все, "Attribute Catalog.Отправки.СтатусОтправки") > 0, "нужен СтатусОтправки");
Проверить(СтрНайти(Все, "объект Enum.Статусы") > 0, "нужно перечисление");
Проверить(СтрНайти(Все, "EnumValue Enum.Статусы.Сдан") > 0, "нужно значение Сдан");
Проверить(СтрНайти(Все, "Отправлен") = 0, "лишнее значение не нужно");
Проверить(СтрНайти(Все, "Строка") = 0, "реквизит ТЧ не заимствуется");
Проверить(СтрНайти(СтрСоединить(П.notes, "|"), "Строка") > 0, "реквизит ТЧ в заметках");
Проверить(До = Прочитать(ОбъединитьПути(Расш, "Catalogs", "Отправки.xml")), "проверка не пишет файлы");

// 4. запись
П = З.Применить(Расш, Исх, "Тест", Истина);
Кат = Прочитать(ОбъединитьПути(Расш, "Catalogs", "Отправки.xml"));
Проверить(СтрНайти(Кат, "<ExtendedConfigurationObject>22222222-2222-2222-2222-222222222222</ExtendedConfigurationObject>") > 0, "ДатаОтправки заимствован");
Проверить(СтрНайти(Кат, "<v8:Type>cfg:EnumRef.Статусы</v8:Type>") > 0, "тип реквизита скопирован");
Проверить(СтрНайти(Кат, "<Name>ДатаОтправки</Name>") < СтрНайти(Кат, "<Form>ФормаСписка</Form>"), "реквизиты перед формами");
Пер = Прочитать(ОбъединитьПути(Расш, "Enums", "Статусы.xml"));
Проверить(СтрНайти(Пер, "<ObjectBelonging>Adopted</ObjectBelonging>") > 0 И СтрНайти(Пер, "<Name>Сдан</Name>") > 0 И СтрНайти(Пер, "Отправлен") = 0, "перечисление заимствовано со значением Сдан");
Конф = Прочитать(ОбъединитьПути(Расш, "Configuration.xml"));
Проверить(СтрНайти(Конф, "<Enum>Статусы</Enum>") > СтрНайти(Конф, "<Catalog>Отправки</Catalog>")
	И СтрНайти(Конф, "<Enum>Статусы</Enum>") < СтрНайти(Конф, "<DataProcessor>Обр</DataProcessor>"), "Enum в Configuration.xml по порядку типов");
П2 = З.Применить(Расш, Исх, "Тест", Ложь);
Проверить(П2.missing.Количество() = 0, "после записи всё заимствовано" + Строки(П2.missing));
П3 = З.Применить(Расш, Исх, "Тест", Истина);
Проверить(Кат = Прочитать(ОбъединитьПути(Расш, "Catalogs", "Отправки.xml")) И П3.addedChildren.Количество() = 0, "повторный запуск ничего не меняет");

УдалитьФайлы(Тмп);
Если Ошибок > 0 Тогда
	Сообщить("test_ext_forms: ошибок " + Ошибок);
	ЗавершитьРаботу(1);
КонецЕсли;
Сообщить("test_ext_forms: OK");
