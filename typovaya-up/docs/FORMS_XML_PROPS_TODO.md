# Todo: 239 форм, где отличаются только свойства Form.xml

Дата: 2026-11-24. Источник классификации: общие объекты УП/БП, `xmlDiff=1`, `xmlNameDiff=0`, статус `xml_only` (177) или `both_differ` (62).

## Решение

Форму БП оставляем в restored. Автоматически заимствовать форму (и тем более класть полное тело Form.xml в extension) **не** делаем. Отличия фиксируем здесь. Обоснование — в `FORMS_BP_PROTO.md`, раздел «Пункт 2».

Визуальные и ресурсные отличия (140) — низкий приоритет: typovaya сохраняет UI БП.
Функциональные привязки (58+4) — высокий приоритет: разобрать вручную, не копировать тело формы целиком.
Прочие и неразобранные (31+6) — средний: сначала понять, что именно меняется.

Списки путей с тегами отличий: `docs/forms-proto/props_*.txt`. Полный перечень 239: `docs/forms-proto/xml_props_only.txt`.

## Визуальные / ресурсные (140) — форма БП, todo

- picture_binding — 96: `props_picture_binding.txt`
- titles_only — 17: `props_titles_only.txt`
- visual_props — 16: `props_visual_props.txt`
- uuid_only — 11: `props_uuid_only.txt`

## Функциональные (62) — высокий приоритет

- functional — 58: `props_functional.txt` (часто `CommandName`, `DataPath`, `Field`, `QueryText`, `MainTable`, `Action`, типы)
- mixed_func — 4: `props_mixed_func.txt`

## Ручной разбор (37)

- other_props — 31: `props_other_props.txt` (часто тег `Command` или `Item` без добавления/удаления имени)
- unparsed_structural — 6: `props_unparsed_structural.txt` (листовые текстовые свойства не найдены, отличия в структуре/пустых узлах)

## 20 форм xml_only с различием имён элементов

Не из этой группы 239. Их режем отдельно (`docs/forms-proto/xml_only_name_diff.txt`): БП в restored, программный слой на элементы только УП, без тела Form.xml в extension. Срез: `D:\typovaya-up-forms-xmlonly`.
