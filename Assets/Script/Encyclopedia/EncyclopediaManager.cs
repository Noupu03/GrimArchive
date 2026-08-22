using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Haare.Util.Logger;

namespace Game.Encyclopedia
{
    /// <summary>
    /// 도감 시스템의 실질적인 구현체. 
    /// 데이터 로드, 잠금 해제, 저장을 관리합니다.
    /// 외부에서는 IEncyclopediaSystem 인터페이스를 통해 접근합니다.
    /// </summary>
    public class EncyclopediaManager : MonoBehaviour, IEncyclopediaSystem
    {
        public static IEncyclopediaSystem Instance { get; private set; }

        [Header("Database (Inspector에서 할당)")]
        [SerializeField] private List<EncyclopediaEntryData> _entryDatabase;

        // 빠른 검색을 위한 Dictionary (Id를 Key로 사용)
        private Dictionary<string, IEncyclopediaEntry> _database = new Dictionary<string, IEncyclopediaEntry>();
        private HashSet<string> _unlockedIds = new HashSet<string>();

        public event Action<string> OnEntryUnlocked;

        private void Awake()
        {
            // 기본적인 싱글톤 구성 (필요에 따라 의존성 주입 프레임워크 활용 가능)
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                
                InitializeDatabase();
                LoadData();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void InitializeDatabase()
        {
            _database.Clear();
            foreach (var entry in _entryDatabase)
            {
                if (entry != null && !string.IsNullOrEmpty(entry.Id))
                {
                    if (!_database.ContainsKey(entry.Id))
                    {
                        _database.Add(entry.Id, entry);
                    }
                    else
                    {
                        LogHelper.Warning(LogHelper.GAME, $"[EncyclopediaManager] 중복된 도감 ID가 존재합니다: {entry.Id}");
                    }
                }
            }
        }

        public bool UnlockEntry(string id)
        {
            if (_database.TryGetValue(id, out var entry))
            {
                if (!_unlockedIds.Contains(id))
                {
                    _unlockedIds.Add(id);
                    entry.IsUnlocked = true;
                    SaveData();
                    
                    // 도감이 해제되었음을 알림 (UI 등에서 감지)
                    OnEntryUnlocked?.Invoke(id);
                    return true;
                }
            }
            else
            {
                LogHelper.Warning(LogHelper.GAME, $"[EncyclopediaManager] 잠금 해제 실패. 존재하지 않는 ID입니다: {id}");
            }
            return false;
        }

        public IEncyclopediaEntry GetEntry(string id)
        {
            if (_database.TryGetValue(id, out var entry))
            {
                return entry;
            }
            return null;
        }

        public List<IEncyclopediaEntry> GetAllEntries()
        {
            return _database.Values.ToList();
        }

        public List<IEncyclopediaEntry> GetUnlockedEntries()
        {
            return _database.Values.Where(e => _unlockedIds.Contains(e.Id)).ToList();
        }

        public List<IEncyclopediaEntry> GetEntriesByCategory(string category)
        {
            return _database.Values.Where(e => e.Category == category).ToList();
        }

        public bool IsUnlocked(string id)
        {
            return _unlockedIds.Contains(id);
        }

        private void SaveData()
        {
            // TODO: 실제 프로젝트의 SaveSystem(JSON, PlayerPrefs 등)과 연동
            // PlayerPrefs.SetString("Encyclopedia_Unlocked", string.Join(",", _unlockedIds));
            LogHelper.Log(LogHelper.GAME, "[EncyclopediaManager] 도감 데이터 저장됨.");
        }

        private void LoadData()
        {
            // TODO: 실제 프로젝트의 LoadSystem과 연동
            // 예시: string savedData = PlayerPrefs.GetString("Encyclopedia_Unlocked", "");
            LogHelper.Log(LogHelper.GAME, "[EncyclopediaManager] 도감 데이터 불러옴.");
        }
    }
}
