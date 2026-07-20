using UnityEngine;
using UnityEditor;
using GrimArchive.Wave;

public class OffenseDebugWindow : EditorWindow
{
    private float _customWaveCooldown = 10f;
    private int _addResourceAmount = 100;
    private static Room _dummyRoom;

    [MenuItem("GrimArchive/오펜스 시스템 디버그 툴")]
    public static void ShowWindow()
    {
        GetWindow<OffenseDebugWindow>("오펜스 디버그");
    }

    private void OnGUI()
    {
        GUILayout.Label("--- 오펜스 & 시스템 테스트 UI ---", EditorStyles.boldLabel);

        // 플레이 모드에서만 동작하도록 경고 표시
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("플레이 모드(Play Mode)에서만 테스트 기능이 작동합니다.", MessageType.Warning);
        }

        EditorGUI.BeginDisabledGroup(!Application.isPlaying);

        // ----------------------------------------------------
        // 1. 오펜스 테스트
        // ----------------------------------------------------
        // 최근에 만들어진 방을 추적해 '플레이어 진입' 등에 재사용하기 위한 static 변수
        if (_dummyRoom == null) 
            _dummyRoom = new Room { RoomName = "테스트 야생 방", RoomFaction = FactionType.Wild, Type = RoomType.Normal };

        if (GUILayout.Button("1. [야생 무리형] 방 구성 (즉시 3마리 소환)"))
        {
            _dummyRoom.Type = RoomType.Normal;
            if (GameSession.Instance != null && GameSession.Instance.unitGenerate != null)
            {
                for (int i = 0; i < 3; i++)
                {
                    Vector2Int spawnPos = _dummyRoom.GetRandomPosInRoom();
                    Monster monster = GameSession.Instance.unitGenerate.GenerateUnitAtPos<Monster>(new MeleeTank(), spawnPos, 1);
                    monster.FactionBehavior = new WildMonsterBehavior();
                    monster.MovementAlgorithm = new RoomConfinedMovement();
                    
                    GameSession.Instance.units.Add(monster);
                    GameSession.Instance.RegisterUnitPos(monster, monster.position);
                    _dummyRoom.AddUnit(monster);
                }
                Debug.Log($"[Test] 무리형 방 설정 완료: MeleeTank 기반 야생 몬스터 3기 스폰 됨");
            }
        }
        
        if (GUILayout.Button("2. 야생 거점 방 구성 (물리적 거점 유닛 배치)"))
        {
            Room targetRoom = _dummyRoom;
            
            // 실제 맵에 생성된 방이 있다면, 그 중 하나(마지막 방 등)를 선택하여 거점화
            if (GameSession.Instance != null && GameSession.Instance.allRooms.Count > 0)
            {
                // 같은 층에 생성된 방들 중 랜덤으로 하나 선택
                int randomIndex = UnityEngine.Random.Range(0, GameSession.Instance.allRooms.Count);
                targetRoom = GameSession.Instance.allRooms[randomIndex];
            }
            
            targetRoom.Type = RoomType.Spawner;
            targetRoom.RoomFaction = FactionType.Wild;

            if (GameSession.Instance != null)
            {
                // 논리 스크립트 대신, 체력과 타격 판정을 지닌 거점 '유닛'을 생성하여 맵에 등록
                WildBaseUnit baseUnit = ScriptableObject.CreateInstance<WildBaseUnit>();
                baseUnit.InitializeBase(targetRoom);
                
                // 방의 정중앙에 거점 배치
                Vector2Int centerPos = new Vector2Int(
                    Mathf.RoundToInt(targetRoom.Bounds.center.x),
                    Mathf.RoundToInt(targetRoom.Bounds.center.y)
                );
                baseUnit.position = centerPos;
                baseUnit.currentFloor = 1; // F1 기준으로 설정
                
#if UNITY_EDITOR
                // 임시 시각적 표현 (화살표 이미지) 렌더링
                GameObject visualGo = new GameObject("WildSpawnerVisual");
                SpriteRenderer sr = visualGo.AddComponent<SpriteRenderer>();
                sr.sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Asset/화살표.png");
                sr.sortingOrder = 50; // 맵에 의해 가려지지 않도록 렌더링 순서 상향

                // 타일맵 렌더러 좌표계에 맞춰서 중앙에 오도록 배치 (층별 오프셋 반영)
                Vector3 floorOffset = GameSession.Instance.unitGenerate != null ? GameSession.Instance.unitGenerate.GetFloorOffset(1) : Vector3.zero;
                visualGo.transform.position = new Vector3(centerPos.x + 0.5f, centerPos.y + 0.5f, 0f) + floorOffset;
                
                // 크기가 너무 크거나 작을 수 있으므로 적절히 조정
                visualGo.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
                
                baseUnit.debugVisual = visualGo;
#endif

                GameSession.Instance.units.Add(baseUnit);
                GameSession.Instance.RegisterUnitPos(baseUnit, baseUnit.position);
            }
        }
        
        if (GUILayout.Button("3. 플레이어 유닛 방 진입 (오펜스 개시)"))
        {
            Unit dummyPlayer = ScriptableObject.CreateInstance<Unit>();
            dummyPlayer.name = "TestPlayer";
            
            if (OffenseProcessor.Instance != null)
            {
                OffenseProcessor.Instance.StartOffense(_dummyRoom, dummyPlayer);
            }
        }
        
        if (GUILayout.Button("야생 몬스터 사망 (자원 B 누적)"))
        {
            if (ResourceAccumulator.Instance != null)
            {
                ResourceAccumulator.Instance.AccumulateResourceB(50);
            }
            else
            {
                Debug.LogWarning("ResourceAccumulator 인스턴스를 찾을 수 없습니다.");
            }
        }
        
        if (GUILayout.Button("오펜스 승리 (누적 자원 정산)"))
        {
            if (ResourceAccumulator.Instance != null)
            {
                ResourceAccumulator.Instance.CommitResourceB();
            }
            else
            {
                Debug.LogWarning("ResourceAccumulator 인스턴스를 찾을 수 없습니다.");
            }
        }

        // ----------------------------------------------------
        // 2. 웨이브 쿨타임 제어
        // ----------------------------------------------------
        GUILayout.Space(20);
        GUILayout.Label("2. 웨이브 쿨타임 제어", EditorStyles.boldLabel);
        
        if (Application.isPlaying && HumanWaveManager.Instance != null)
        {
            GUILayout.Label($"현재 상태: {HumanWaveManager.Instance.currentState}");
            GUILayout.Label($"남은 쿨타임: {HumanWaveManager.Instance.cooldownTimer:F1} 초");
            
            GUILayout.BeginHorizontal();
            GUILayout.Label("설정할 쿨타임:", GUILayout.Width(100));
            _customWaveCooldown = EditorGUILayout.FloatField(_customWaveCooldown, GUILayout.Width(50));
            
            if (GUILayout.Button("적용", GUILayout.Width(60)))
            {
                HumanWaveManager.Instance.cooldownTimer = _customWaveCooldown;
                Debug.Log($"웨이브 쿨타임을 {_customWaveCooldown}초로 변경했습니다.");
            }
            GUILayout.EndHorizontal();
        }
        else
        {
            GUILayout.Label("HumanWaveManager 인스턴스를 찾을 수 없거나 실행 중이 아닙니다.");
        }

        // ----------------------------------------------------
        // 3. 건축용 자원 제어
        // ----------------------------------------------------
        GUILayout.Space(20);
        GUILayout.Label("3. 건축(ResourceManager) 자원 제어", EditorStyles.boldLabel);
        
        ResourceManager resourceManager = ResourceManager.Instance;
        
        if (Application.isPlaying && resourceManager != null)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("자원 추가량:", GUILayout.Width(100));
            _addResourceAmount = EditorGUILayout.IntField(_addResourceAmount, GUILayout.Width(100));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+ Wood")) resourceManager.AddResource(ResourceType.Wood, _addResourceAmount);
            if (GUILayout.Button("+ Stone")) resourceManager.AddResource(ResourceType.Stone, _addResourceAmount);
            if (GUILayout.Button("+ Gold")) resourceManager.AddResource(ResourceType.Gold, _addResourceAmount);
            GUILayout.EndHorizontal();
        }
        else
        {
            GUILayout.Label("ResourceManager를 찾을 수 없거나 실행 중이 아닙니다.");
        }

        EditorGUI.EndDisabledGroup();
    }
}
