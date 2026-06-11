using System;
using Cysharp.Threading.Tasks;

namespace ProjectII.Gameplay.Domain
{
    /// <summary>
    /// 執行表格中的效果指令串（KahaGameCore EffectProcessor 語法）。
    /// 可省略時機區塊：寫「AddValue(Satiety,30);AdvanceTime()」會自動包成 Execute{...}。
    /// </summary>
    public interface ICommandExecutor
    {
        void Execute(string rawCommands, Action onCompleted);
        UniTask ExecuteAsync(string rawCommands);
    }
}
