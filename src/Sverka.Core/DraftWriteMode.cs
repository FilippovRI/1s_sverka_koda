namespace Sverka;

/// <summary>Что писать в extension/ и forms/ при сохранении черновика.</summary>
public enum DraftWriteMode
{
    /// <summary>Все пункты Auto (+ Форма).</summary>
    AllAuto,
    /// <summary>Только строки со статусом «Готово».</summary>
    DoneOnly,
    /// <summary>«Готово» и все Auto/Форма (merge).</summary>
    DoneAndAuto,
}
