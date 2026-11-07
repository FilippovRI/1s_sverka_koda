# Метаданные черновика расширения

## Configuration_InternalInfo.xml

Фрагмент `<InternalInfo>` для корневого `Configuration.xml` черновика расширения.

**Рекомендуемый рабочий цикл (Windows, ibcmd 8.5+):** создать пустое расширение в выделенной ИБ (`ibcmd extension create … --purpose=customization`), выгрузить скелет (`ibcmd config export --extension=…`), скопировать блок `InternalInfo` из `Configuration.xml` сюда. UUID и семь `xr:ContainedObject` должны совпадать с реальным расширением в базе — иначе импорт может пройти, но идентичность метаданных будет расходиться.

В репозитории лежит **тестовый** фрагмент с детерминированными UUID (не привязан к вашей ИБ). Для продакшн-черновика замените файл содержимым из экспорта.

`typovaya-up` подставляет этот фрагмент при генерации `extension/Configuration.xml`, если файл существует. `KeepMappingToExtendedConfigurationObjectsByIDs` остаётся `true`.

**Не коммитьте:** выгрузки УП/БП, отчёты, содержимое рабочих ИБ.
