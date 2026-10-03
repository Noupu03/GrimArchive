// 코드가 불러오는 Addressables 주소 모음 — Assets/AddressableAssetsData/AssetGroups의 m_Address와 반드시
// 같아야 한다(주소는 파일 위치와 무관해서 파일을 옮겨도 그대로다). 새 에셋을 코드에서 불러오려면 에셋을
// 종류별 폴더에 두고 → Addressables 그룹에 주소를 등록하고 → 여기 상수를 추가한 뒤 GameAssets로 불러온다.
// 주소 문자열은 Resources 시절 경로를 그대로 이어받았다(2026-10-02 전환).
public static class AssetKeys
{
    // ── 유닛 프리팹 (Prefabs/Units) — 주소 "Units/{유닛 타입 이름}", 전부 "Units" 라벨 ──
    public const string UnitsLabel = "Units";
    public static string Unit(string unitTypeName) => "Units/" + unitTypeName;

    // ── 데이터 (Data) ──
    public const string Map = "Data/map";
    public const string WaveData = "WaveData";
    public const string FloorColorThemes = "MapColorTheme_FloorAssignments";
    // Tools(new)/유닛/FSM+BT 설정 에셋 생성으로 만든다 — 없으면 각 FSMState가 내부 기본값을 쓴다.
    public const string AIBehaviorConfig = "FSM+BT/AIBehaviorConfig";
    public const string TacticalPriority = "FSM+BT/TacticalPriority";

    // ── 타일 (Art/Sprites/Tiles) ──
    public const string WallSprite = "Tile_StoneWall";
    public const string FloorSprite = "FloorTexture";
    public const string TileSpriteLibrary = "Tile/TileSpriteLibrary";

    // ── 오브젝트 스프라이트 (Art/Sprites/Objects) ──
    public const string CoreSprite = "obj/core";
    public const string TrapSprite = "obj/trap";
    public const string CorpseSprite = "obj/colapse";
    public const string LootSprite = "obj/obj1";
    public const string DoorOpenSprite = "obj/door_open";
    public const string DoorClosedSprite = "obj/door_closed";
    public const string FogSprite = "obj/fog";
    // 파일 이름과 실제 그림이 반대다(stair_up2 = 내려가는 계단 그림) — MapRandering이 바꿔 배정한다.
    public const string StairUp2Sprite = "obj/stair_up2";
    public const string StairDown2Sprite = "obj/stair_down2";
    public const string ResourceBuildingSprite = "obj/GothicClocktower";
    public const string UnitBuildingSprite = "obj/GothicDollhouse";
    // debug 더미 건물(예전 유닛/자원 생산 건물 그림).
    public const string DummyUnitBuildingSprite = "obj/building";
    public const string DummyResourceBuildingSprite = "obj/resource_building";

    // ── VFX (Prefabs/VFX, Art/Sprites/VFX) ──
    public const string TorchPrefab = "Prefabs/VFX/Torch";
    public const string BlockBreakingVfx = "Prefabs/VFX/VFX_BlockBreaking";
    public const string CoreBoomHumanVfx = "Prefabs/VFX/VFX_CoreBoomHuman Variant";
    public const string CoreBoomMonsterVfx = "Prefabs/VFX/VFX_CoreBoomMonster Variant";
    public const string AttackZoneLibrary = "attackZone";

    // ── UI (Art/Sprites/UI) ──
    public const string WaveGaugeSprite = "UI/KtoDUI_1";
}
