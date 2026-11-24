// УбратьОбщиеМодулиТолькоУпИзRestored: ОМ «только УП» (есть в УП, нет в БП) убираются из restored
// (файлы, Configuration.xml, ConfigDumpInfo.xml, подсистемы) и остаются собственными объектами extension с кодом УП.
// Ссылки кода restored на убранные модули не правятся — только список мест.
Перем Ошибок;

Процедура П(У, С)
	Если Не У Тогда Сообщить("FAIL: " + С); Ошибок = Ошибок + 1; КонецЕсли;
КонецПроцедуры
Процедура Зап(Путь, Текст)
	Ф = Новый Файл(Путь);
	СоздатьКаталог(Ф.Путь);
	ПолучитьДвоичныеДанныеИзСтроки(Текст, КодировкаТекста.UTF8, Ложь).Записать(Путь);
КонецПроцедуры
Функция Есть(Путь)
	Возврат (Новый Файл(Путь)).Существует();
КонецФункции
Функция Конф(Объекты)
	Т = "<MetaDataObject>" + Символы.ПС + "	<Configuration>" + Символы.ПС + "		<ChildObjects>" + Символы.ПС;
	Для Каждого О Из СтрРазделить(Объекты, ",", Ложь) Цикл
		Ч = СтрРазделить(СокрЛП(О), ".", Ложь);
		Т = Т + "			<" + Ч[0] + ">" + Ч[1] + "</" + Ч[0] + ">" + Символы.ПС;
	КонецЦикла;
	Возврат Т + "		</ChildObjects>" + Символы.ПС + "	</Configuration>" + Символы.ПС + "</MetaDataObject>" + Символы.ПС;
КонецФункции
Функция XmlМодуля(Уид, Заим = Ложь)
	Возврат "<MetaDataObject><CommonModule uuid=""" + Уид + """><Properties>"
		+ ?(Заим, "<ObjectBelonging>Adopted</ObjectBelonging>", "")
		+ "<ClientManagedApplication>false</ClientManagedApplication><Server>true</Server></Properties></CommonModule></MetaDataObject>";
КонецФункции

Ошибок = 0;
Ск = ОбъединитьПути(ТекущийСценарий().Каталог, "..", "src");
Вр = ОбъединитьПути(КаталогВременныхФайлов(), "t_upcm_rest_" + Строка(Новый УникальныйИдентификатор));
Уп = ОбъединитьПути(Вр, "up");
Бп = ОбъединитьПути(Вр, "bp");
Осн = ОбъединитьПути(Вр, "restored");
Расш = ОбъединитьПути(Вр, "ext");
Зап(ОбъединитьПути(Уп, "Configuration.xml"), Конф("CommonModule.Общий,CommonModule.НСКТест,CommonModule.НСКЗаим"));
Зап(ОбъединитьПути(Бп, "Configuration.xml"), Конф("CommonModule.Общий"));
Зап(ОбъединитьПути(Осн, "Configuration.xml"), Конф("CommonModule.Общий,CommonModule.НСКТест,CommonModule.НСКЗаим"));
Зап(ОбъединитьПути(Расш, "Configuration.xml"), Конф("CommonModule.НСКЗаим"));
Для Каждого Имя Из СтрРазделить("Общий,НСКТест,НСКЗаим", ",") Цикл
	Уид = ?(Имя = "Общий", "11111111-1111-1111-1111-111111111111", ?(Имя = "НСКТест", "22222222-2222-2222-2222-222222222222", "33333333-3333-3333-3333-333333333333"));
	Для Каждого Кор Из СтрРазделить(Уп + "|" + Осн, "|") Цикл
		Зап(ОбъединитьПути(Кор, "CommonModules", Имя + ".xml"), XmlМодуля(Уид));
		Зап(ОбъединитьПути(Кор, "CommonModules", Имя, "Ext/Module.bsl"), "// УП " + Имя + Символы.ПС + "Процедура М() Экспорт" + Символы.ПС + "КонецПроцедуры" + Символы.ПС);
	КонецЦикла;
КонецЦикла;
Зап(ОбъединитьПути(Расш, "CommonModules", "НСКЗаим.xml"), XmlМодуля("44444444-4444-4444-4444-444444444444", Истина));
Зап(ОбъединитьПути(Осн, "ConfigDumpInfo.xml"), "<ConfigDumpInfo>" + Символы.ПС + "	<ConfigVersions>" + Символы.ПС
	+ "		<Metadata name=""CommonModule.Общий"" id=""1"" configVersion=""a""/>" + Символы.ПС
	+ "		<Metadata name=""CommonModule.НСКТест"" id=""2"" configVersion=""b""/>" + Символы.ПС
	+ "		<Metadata name=""CommonModule.НСКТест.Module"" id=""2.0"" configVersion=""c""/>" + Символы.ПС
	+ "		<Metadata name=""CommonModule.НСКТестДругой"" id=""5"" configVersion=""d""/>" + Символы.ПС
	+ "		<Metadata name=""EventSubscription.Подп"" id=""6"" configVersion=""e""/>" + Символы.ПС
	+ "		<Metadata name=""EventSubscription.ПодпОбщ"" id=""7"" configVersion=""f""/>" + Символы.ПС
	+ "	</ConfigVersions>" + Символы.ПС + "</ConfigDumpInfo>" + Символы.ПС);
Зап(ОбъединитьПути(Осн, "Subsystems", "Подс.xml"), "<Subsystem><Content>" + Символы.ПС
	+ "				<xr:Item xsi:type=""xr:MDObjectRef"">CommonModule.Общий</xr:Item>" + Символы.ПС
	+ "				<xr:Item xsi:type=""xr:MDObjectRef"">CommonModule.НСКТест</xr:Item>" + Символы.ПС
	+ "				<xr:Item xsi:type=""xr:MDObjectRef"">ScheduledJob.Задание</xr:Item>" + Символы.ПС
	+ "</Content></Subsystem>" + Символы.ПС);
Зап(ОбъединитьПути(Осн, "Catalogs", "Спр", "Ext/ObjectModule.bsl"),
	"// НСКТест.М();" + Символы.ПС + "НСКТест.М();" + Символы.ПС + "Общий.М();" + Символы.ПС);
Зап(ОбъединитьПути(Осн, "EventSubscriptions", "Подп.xml"), "<EventSubscription uuid=""55555555-5555-5555-5555-555555555555""><Handler>CommonModule.НСКЗаим.М</Handler></EventSubscription>");
Зап(ОбъединитьПути(Уп, "EventSubscriptions", "Подп.xml"), "<EventSubscription uuid=""55555555-5555-5555-5555-555555555555""><Handler>CommonModule.НСКЗаим.М</Handler><!-- УП --></EventSubscription>");
Зап(ОбъединитьПути(Осн, "EventSubscriptions", "ПодпОбщ.xml"), "<EventSubscription><Handler>CommonModule.Общий.М</Handler></EventSubscription>");
Зап(ОбъединитьПути(Осн, "ScheduledJobs", "Задание.xml"), "<ScheduledJob uuid=""66666666-6666-6666-6666-666666666666""><MethodName>CommonModule.НСКТест.М</MethodName></ScheduledJob>");
Зап(ОбъединитьПути(Осн, "Configuration.xml"), Конф("CommonModule.Общий,CommonModule.НСКТест,CommonModule.НСКЗаим,EventSubscription.Подп,EventSubscription.ПодпОбщ,ScheduledJob.Задание"));

Пер = ЗагрузитьСценарий(ОбъединитьПути(Ск, "ПереносОбъектовУп.os"));
ОТУ = ЗагрузитьСценарий(ОбъединитьПути(Ск, "ОбъектыТолькоУп.os"));
Пер.Инициализировать(ЗагрузитьСценарий(ОбъединитьПути(Ск, "ЧерновикРасширения.os")), ОТУ);
Лог = Новый Массив;
Р = Пер.УбратьОбщиеМодулиТолькоУпИзRestored(Расш, Осн, Уп, Бп, Новый Соответствие, "УП_Тест", Лог, Вр);

П(Р.removed = 2, "removed=2: " + Р.removed);
П(Не Есть(ОбъединитьПути(Осн, "CommonModules", "НСКТест.xml")), "restored без НСКТест.xml");
П(Не Есть(ОбъединитьПути(Осн, "CommonModules", "НСКТест")), "restored без папки НСКТест");
П(Не Есть(ОбъединитьПути(Осн, "CommonModules", "НСКЗаим.xml")), "restored без НСКЗаим.xml");
П(Есть(ОбъединитьПути(Осн, "CommonModules", "Общий.xml")), "Общий (есть в БП) остаётся в restored");
Кф = ОТУ.ПрочитатьТекст(ОбъединитьПути(Осн, "Configuration.xml"));
П(СтрНайти(Кф, "<CommonModule>НСКТест</CommonModule>") = 0 И СтрНайти(Кф, "<CommonModule>НСКЗаим</CommonModule>") = 0, "Configuration.xml restored");
П(СтрНайти(Кф, "<CommonModule>Общий</CommonModule>") > 0, "Общий в Configuration.xml restored");
Ди = ОТУ.ПрочитатьТекст(ОбъединитьПути(Осн, "ConfigDumpInfo.xml"));
П(СтрНайти(Ди, """CommonModule.НСКТест""") = 0 И СтрНайти(Ди, "CommonModule.НСКТест.Module") = 0, "ConfigDumpInfo без НСКТест");
П(СтрНайти(Ди, "CommonModule.НСКТестДругой") > 0 И СтрНайти(Ди, "CommonModule.Общий") > 0, "ConfigDumpInfo: прочие строки целы");
Пд = ОТУ.ПрочитатьТекст(ОбъединитьПути(Осн, "Subsystems", "Подс.xml"));
П(СтрНайти(Пд, "CommonModule.НСКТест") = 0 И СтрНайти(Пд, "CommonModule.Общий") > 0, "подсистема restored");
Для Каждого Имя Из СтрРазделить("НСКТест,НСКЗаим", ",") Цикл
	Тх = ОТУ.ПрочитатьТекст(ОбъединитьПути(Расш, "CommonModules", Имя + ".xml"));
	П(Тх <> "" И СтрНайти(Тх, "Adopted") = 0, Имя + ": собственный объект extension");
	П(СтрНайти(ОТУ.ПрочитатьТекст(ОбъединитьПути(Расш, "CommonModules", Имя, "Ext/Module.bsl")), "// УП " + Имя) > 0, Имя + ": код из УП");
КонецЦикла;
Ке = ОТУ.ПрочитатьТекст(ОбъединитьПути(Расш, "Configuration.xml"));
П(СтрНайти(Ке, "<CommonModule>НСКТест</CommonModule>") > 0, "Configuration.xml extension");
П(Не Есть(ОбъединитьПути(Расш, "CommonModules", "Общий.xml")), "Общий не в extension");
П(Р.extAdded = 2 И Р.extReplacedAdopted = 1, "extAdded/replaced: " + Р.extAdded + "/" + Р.extReplacedAdopted);
П(Р.refsLines = 1 И Р.refsFiles = 1, "ссылки кода restored (без комментариев): " + Р.refsLines);
П(Р.xmlRefs = 0, "ссылки XML restored после переноса подписок/заданий: " + Р.xmlRefs);
П(Р.handlers.Количество() = 2, "перенесено подписок/заданий: " + Р.handlers.Количество());
П(Не Есть(ОбъединитьПути(Осн, "EventSubscriptions", "Подп.xml")) И Не Есть(ОбъединитьПути(Осн, "ScheduledJobs", "Задание.xml")), "restored без подписки/задания");
П(Есть(ОбъединитьПути(Осн, "EventSubscriptions", "ПодпОбщ.xml")), "подписка на ОМ БП остаётся в restored");
Тп = ОТУ.ПрочитатьТекст(ОбъединитьПути(Расш, "EventSubscriptions", "Подп.xml"));
П(СтрНайти(Тп, "<!-- УП -->") > 0 И СтрНайти(Тп, "55555555-5555") = 0, "подписка в extension: XML из УП, новый uuid");
П(Есть(ОбъединитьПути(Расш, "ScheduledJobs", "Задание.xml")), "задание в extension (из restored, в УП нет)");
Кф2 = ОТУ.ПрочитатьТекст(ОбъединитьПути(Осн, "Configuration.xml"));
П(СтрНайти(Кф2, "<EventSubscription>Подп</EventSubscription>") = 0 И СтрНайти(Кф2, "<ScheduledJob>Задание</ScheduledJob>") = 0
	И СтрНайти(Кф2, "<EventSubscription>ПодпОбщ</EventSubscription>") > 0, "Configuration.xml restored: подписка/задание");
Ке2 = ОТУ.ПрочитатьТекст(ОбъединитьПути(Расш, "Configuration.xml"));
П(СтрНайти(Ке2, "<EventSubscription>Подп</EventSubscription>") > 0 И СтрНайти(Ке2, "<ScheduledJob>Задание</ScheduledJob>") > 0, "Configuration.xml extension: подписка/задание");
Ди2 = ОТУ.ПрочитатьТекст(ОбъединитьПути(Осн, "ConfigDumpInfo.xml"));
П(СтрНайти(Ди2, """EventSubscription.Подп""") = 0 И СтрНайти(Ди2, """EventSubscription.ПодпОбщ""") > 0, "ConfigDumpInfo: подписка");
П(СтрНайти(ОТУ.ПрочитатьТекст(ОбъединитьПути(Осн, "Subsystems", "Подс.xml")), "ScheduledJob.Задание") = 0, "подсистема: задание");
П(Есть(ОбъединитьПути(Вр, "uponly_cm_restored_refs.txt")), "uponly_cm_restored_refs.txt");
// Повтор идемпотентен.
Р2 = Пер.УбратьОбщиеМодулиТолькоУпИзRestored(Расш, Осн, Уп, Бп, Новый Соответствие, "УП_Тест", Лог, Вр);
П(Р2.removed = 0, "повтор: removed=0");
// Повтор после пересборки extension (restored уже очищен): ОМ снова попадает в extension.
УдалитьФайлы(ОбъединитьПути(Расш, "CommonModules", "НСКТест.xml"));
УдалитьФайлы(ОбъединитьПути(Расш, "CommonModules", "НСКТест"));
Р3 = Пер.УбратьОбщиеМодулиТолькоУпИзRestored(Расш, Осн, Уп, Бп, Новый Соответствие, "УП_Тест", Лог, Вр);
П(Р3.removed = 0 И Р3.extAdded = 1 И Есть(ОбъединитьПути(Расш, "CommonModules", "НСКТест.xml")), "повтор после пересборки extension");
УдалитьФайлы(Вр);
Если Ошибок > 0 Тогда ВызватьИсключение "test_uponly_cm_restored"; КонецЕсли;
Сообщить("OK: test_uponly_cm_restored");
