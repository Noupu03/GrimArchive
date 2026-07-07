using System;
using System.Collections.Generic;

namespace Game.Encyclopedia
{
    /// <summary>
    /// 도감 시스템의 핵심 기능을 정의하는 인터페이스.
    /// 구체적인 구현체(Manager)에 의존하지 않고, 다른 모듈들이 도감 기능에 접근할 수 있게 합니다.
    /// </summary>
    public interface IEncyclopediaSystem
    {
        bool UnlockEntry(string id);
        IEncyclopediaEntry GetEntry(string id);
        List<IEncyclopediaEntry> GetAllEntries();
        List<IEncyclopediaEntry> GetUnlockedEntries();
        List<IEncyclopediaEntry> GetEntriesByCategory(string category);
        bool IsUnlocked(string id);
        
        event Action<string> OnEntryUnlocked;
    }
}
