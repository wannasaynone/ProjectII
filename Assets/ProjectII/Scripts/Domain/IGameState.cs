using KahaGameCore.ValueContainer;

namespace ProjectII.Gameplay.Domain
{
    /// <summary>
    /// 遊戲進行中的所有可變數值的唯一入口。
    /// 寫入時依 GameValueData 表自動鉗制上下限，並發佈 GameValueChangedEvent。
    /// </summary>
    public interface IGameState
    {
        /// <summary>供 KahaGameCore.Calculator 公式以 Caster.Tag 取值。</summary>
        IValueContainer Container { get; }

        int Get(string tag);
        void Add(string tag, int amount);
        void Set(string tag, int value);

        /// <summary>依 GameValueData 表的 InitialValue 重設所有數值（開新遊戲）。</summary>
        void ResetToInitial();
    }
}
