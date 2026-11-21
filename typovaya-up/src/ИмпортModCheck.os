// restored + расширение в InfoBase_ModCheck. oscript ИмпортModCheck.os [выход] [--only-main|--only-ext|--allow-fail]
КореньСк = ТекущийСценарий().Каталог;
Shell = ЗагрузитьСценарий(ОбъединитьПути(КореньСк, "ЗапускShell.os"));
Oscript = "C:\Users\rst_filippov\OneScript\bin\oscript.exe";
Иб = "C:\Program Files\1cv8\8.5.1.1150\bin\ibcmd.exe";
ИбБаза = "C:\Users\rst_filippov\Documents\InfoBase_ModCheck";
ИмяРасш = "УП_Доработки";
Выход = "D:\typovaya-up-out-20261118u";
ТолькоОсн = Ложь;
ТолькоРасш = Ложь;
РазрешитьСбой = Ложь;
Для Каждого Арг Из АргументыКоманднойСтроки Цикл
	А = СокрЛП(Арг);
	Если А = "--only-ext" Тогда
		ТолькоРасш = Истина;
	ИначеЕсли А = "--only-main" Тогда
		ТолькоОсн = Истина;
	ИначеЕсли А = "--allow-fail" Тогда
		РазрешитьСбой = Истина;
	ИначеЕсли Не ПустаяСтрока(А) И Лев(А, 2) <> "--" Тогда
		Выход = А;
	КонецЕсли;
КонецЦикла;
КореньРасш = ОбъединитьПути(Выход, "extension");
КореньВосст = ОбъединитьПути(Выход, "restored");
ЛогОсн = ОбъединитьПути(Выход, "modcheck-import-main.log");
ЛогРасш = ОбъединитьПути(Выход, "modcheck-import-ext.log");
Db = "--db-path=" + ИбБаза;
КодОсн = 0;
КодРасш = 0;
КодCm = 0;
Если Не ТолькоРасш Тогда
	Если Не (Новый Файл(ОбъединитьПути(КореньВосст, "Configuration.xml"))).Существует() Тогда
		ВызватьИсключение "нет restored/Configuration.xml";
	КонецЕсли;
	Сообщить("import main…");
	Рм = Shell.ИсполнитьIbcmd(Иб, Shell.ArgvИзСтрок("infobase|config|import|" + Db + "|" + КореньВосст), ЛогОсн);
	КодОсн = Рм.exitCode;
	Сообщить("import main exit " + КодОсн);
	Shell.ИсполнитьCmd("""" + Oscript + """ """ + ОбъединитьПути(КореньСк, "СводкаОшибокImport.os") + """ """ + ЛогОсн + """");
	Если КодОсн <> 0 Тогда
		Если РазрешитьСбой Тогда
			ЗавершитьРаботу(КодОсн);
		КонецЕсли;
		ВызватьИсключение "import main failed";
	КонецЕсли;
	Рап = Shell.ИсполнитьIbcmd(Иб, Shell.ArgvИзСтрок("infobase|config|apply|" + Db + "|--force"),
		ОбъединитьПути(Выход, "modcheck-apply-main.log"));
	Сообщить("apply main exit " + Рап.exitCode);
	Если Рап.exitCode <> 0 Тогда
		ВызватьИсключение "apply main failed";
	КонецЕсли;
	ВремЭксп = ОбъединитьПути(Выход, "_modcheck_export_tmp");
	Рэ = Shell.ИсполнитьIbcmd(Иб, Shell.ArgvИзСтрок("infobase|config|export|" + Db + "|" + ВремЭксп),
		ОбъединитьПути(Выход, "modcheck-export-verify.log"));
	Сообщить("export exit " + Рэ.exitCode);
	Shell.ИсполнитьIbcmd(Иб, Shell.ArgvИзСтрок("infobase|config|extension|delete|" + Db + "|--name=" + ИмяРасш),
		ОбъединитьПути(Выход, "modcheck-ext-delete.log"));
КонецЕсли;
Если ТолькоОсн Тогда
	ЗавершитьРаботу(КодОсн);
КонецЕсли;
Рг2 = Shell.ИсполнитьCmd("""" + Oscript + """ """ + ОбъединитьПути(КореньСк, "ГейтПередИмпортом.os") + """ """ + КореньРасш + """");
Если Рг2.exitCode <> 0 Тогда
	ВызватьИсключение "pre-import gate failed (ext)";
КонецЕсли;
Рр = Shell.ИсполнитьIbcmd(Иб, Shell.ArgvИзСтрок("infobase|config|import|" + Db + "|--extension=" + ИмяРасш + "|" + КореньРасш), ЛогРасш);
КодРасш = Рр.exitCode;
Сообщить("import extension exit " + КодРасш);
Shell.ИсполнитьCmd("""" + Oscript + """ """ + ОбъединитьПути(КореньСк, "СводкаОшибокImport.os") + """ """ + ЛогРасш + """");
Если КодРасш <> 0 Тогда
	Если РазрешитьСбой Тогда
		ЗавершитьРаботу(КодРасш);
	КонецЕсли;
	ВызватьИсключение "import extension failed";
КонецЕсли;
Ра = Shell.ИсполнитьIbcmd(Иб, Shell.ArgvИзСтрок("infobase|config|apply|" + Db + "|--extension=" + ИмяРасш),
	ОбъединитьПути(Выход, "modcheck-apply.log"));
Сообщить("apply extension exit " + Ра.exitCode);
Рч = Shell.ИсполнитьIbcmd(Иб, Shell.ArgvИзСтрок("infobase|config|checkmodules|" + Db + "|--extension=" + ИмяРасш
	+ "|-ThinClient|-Server|-ExternalConnection"), ОбъединитьПути(Выход, "modcheck-checkmodules.log"));
КодCm = Рч.exitCode;
Сообщить("checkmodules exit " + КодCm);
ЗавершитьРаботу(КодCm);