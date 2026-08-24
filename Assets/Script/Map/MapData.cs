// ============================================================================
// MapData.cs — 맵 데이터 구조체 · 열거형 · 팩토리 정의
// ----------------------------------------------------------------------------
// 역할: 던전 생성 시스템의 모든 데이터 타입을 정의.
//       열거형: FloorId, RoomRole, OccupationState, TileEffect, Footprint
//       구조체: Tile, Chunks, Floor, Map, Gate, FloorConfig, MapData
//       팩토리: TileFactory, FloorConfigFactory, RoomIdGenerator
//       CreateMap 및 관련 시스템이 참조하는 순수 데이터 레이어.
// ============================================================================
using System;

// ── 층 식별자 ──
[Serializable]
public enum FloorId
{
	Floor_0 = 0,   // 0층: 입구/로비
	Floor_1 = 1,   // 1층: 7×7
	Floor_2 = 2,   // 2층: 8×8
	Floor_3 = 3    // 3층: 9×9
}

// ── 방 역할 ──
[Serializable]
public enum RoomRole
{
	None,
	StartRoom,
	NormalRoom,
	SubPurposeRoom,
	BossRoom
}

// ── 점령 상태 ──
[Serializable]
public enum OccupationState
{
	Neutral,            // 중립 (미점령) — 야생 방
	PlayerControlled,   // 플레이어(몬스터 진영) 점령
	Occupied,           // 적 점령
	Outpost,            // 전초기지
	HumanControlled     // 인류 소유(2026-07-27 신규) — 0층 로비 전체가 시작값으로 가짐
}

[Serializable]
public enum TileEffect
{
	None
	// 효과 종류는 필요에 따라 확장하세요.
}

// ── 청크 경계 벽면 방향 ── (2026-08-21, 횃불 벽걸이 배치 신규) — 원래 FogOfWarSystem.cs에 있었으나
// CreateMap.TileWall.cs(청크 경계 판정)와 Assets/Script/Unit/Visual/TorchVisual.cs(스프라이트 라벨)가
// 둘 다 참조해야 해서, 유닛의 Dir(Assets/Script/Unit/Core/UnitTypes.cs)과 동일한 이유로 어느 한쪽
// 레이어에 속하지 않는 이 파일로 옮겼다. new_torch.png의 스프라이트 라벨(Up/Right/Down)과 1:1
// 대응한다 — Left는 별도 스프라이트가 없어 Right 라벨을 좌우 반전(flipX)해서 재사용한다.
public enum TorchWallSide { Top, Right, Bottom, Left }

// ── Footprint 크기 (정사각형 전용, 1~5) ──
[Serializable]
public enum Footprint
{
	Size1 = 1,
	Size2 = 2,
	Size3 = 3,
	Size4 = 4,
	Size5 = 5
}

// ── 방 사이 통로(Gate) 정보 ──
[Serializable]
public struct Gate
{
	// 연결되는 두 방의 ID
	public int roomA;
	public int roomB;
	// 통로가 위치한 청크 좌표 (A쪽, B쪽)
	public int chunkAX;
	public int chunkAY;
	public int chunkBX;
	public int chunkBY;
	// 통로 폭 (타일 수)
	public int width;
	// 방향: true=수평(좌우 인접), false=수직(상하 인접)
	public bool isHorizontal;

	// 문 닫힘 시스템(2026-07-28 최초 도입, 2026-08-22 DoorSystem 진영 기반 개폐로 대체되며 미사용
	// 필드가 됨 — 기존 맵 저장 파일과의 직렬화 호환을 위해 필드 자체는 남겨둔다). 개폐 상태는 이제
	// InteractableObject.DoorIsOpenVisual(매 프레임 진영·근접 여부로 재계산)이 대신 담당한다.
	public bool isDoorClosed;
}

[Serializable]
public struct Tile
{
	// 타일 이름
	public string name;
	// 효과
	public TileEffect effect;
	// 점유 여부
	public bool isObjectExist;
	// 건물 존재 여부
	public bool isStructureExist;
	// 위험도
	public int dangerous;
	// 이해도
	public int understand;
	// 가중치
	public int weight;
	// 가시성
	public int visibility;
}

[Serializable]
public struct Chunks
{
	// 8x8 크기 tile 데이터
	public Tile[,] chunk;

	// 지형 프리팹 가져오기용
	public int landform;

	// 방 식별자
	public int roomId;
	public string roomName;

	// 방 역할 (시작방/일반방/서브목적방/보스방)
	public RoomRole roomRole;

	// 이 청크가 속한 층
	public int floorId;

	// 점령 상태
	public OccupationState occupationState;

	// 계단 연결 대상 층 (-1 = 계단 없음)
	public int stairTargetFloor;

	// 이 청크(방)에 진입 가능한 최대 Footprint 크기
	public int allowMaxFootprint;

	// 계단 개방 여부 (false면 잠김 상태)
	public bool stairIsOpen;

	// 인류 전용 계단 여부 (true면 인류만 통행 가능)
	public bool stairHumanOnly;
}

// ── 개별 층 데이터 ──
[Serializable]
public struct Floor
{
	public FloorConfig config;
	// 이 층의 청크 배열 (config.width × config.height)
	public Chunks[,] chunks;
	// 이 층의 모든 Gate(통로) 목록
	public System.Collections.Generic.List<Gate> gates;
}

[Serializable]
public struct Map
{
	// 층별 독립 데이터 (Floor_0 ~ Floor_3)
	public Floor[] floors;

	// 편의 접근: floorIndex(0~3)로 Floor 참조
	public Floor GetFloor(int floorIndex) => floors[floorIndex];
}

// ── 층별 생성 설정 ──
[Serializable]
public struct FloorConfig
{
	public FloorId floorId;
	// 바운더리 크기 (청크 단위)
	public int width;
	public int height;
	// 방 개수 제한
	public int normalRoomCount;
	public int subPurposeRoomCount;
	// 보스방 형태 설명 (예: "2x2", "ㄷ7", "3x3")
	public string bossRoomFormat;
	// 외곽 벽 두께 범위 (타일 단위)
	public int wallThicknessMin;
	public int wallThicknessMax;
	// 일반방 1개당 최대 청크 수
	public int maxNormalRoomChunks;
	// 총 방 수 (시작방 + 일반방 + 보스방). 서브 목적방은 별도 카운트.
	public int totalRoomCount;
	// 청크 1개의 타일 한 변 크기(정사각형, 기존엔 전 층 공용 상수 8이었음). 맵 크기 1.5배 확장
	// (2026-08-23 사용자 요청)으로 층별 설정값이 됐다 — CreateMap의 모든 청크 배열 할당/타일 좌표
	// 변환과 MapRandering/DoorSystem/FogOfWarSystem의 렌더링·문·안개 계산이 전부 이 값을 참조한다.
	public int chunkSize;
}

public struct MapData
{
	// Map 데이터 (Floor별 독립 배열 포함)
	public Map map;
}

// 타일 팩토리: 타일 종류별 기본값을 한 곳에서 관리합니다.
// 새로운 타일 종류(예: Water, Lava 등)를 추가하려면 여기에 메서드를 추가하세요.
public static class TileFactory
{
	public static Tile Wall()
	{
		return new Tile
		{
			name = "Wall",
			effect = TileEffect.None,
			isObjectExist = false,
			isStructureExist = false,
			dangerous = 0,
			understand = 0,
			weight = -1,
			visibility = 0
		};
	}

	public static Tile Floor()
	{
		return new Tile
		{
			name = "Floor",
			effect = TileEffect.None,
			isObjectExist = false,
			isStructureExist = false,
			dangerous = 0,
			understand = 0,
			weight = 1,
			visibility = 100
		};
	}

	public static Tile Stair()
	{
		return new Tile
		{
			name = "Stair",
			effect = TileEffect.None,
			isObjectExist = false,
			isStructureExist = true,
			dangerous = 0,
			understand = 0,
			weight = 1,
			visibility = 100
		};
	}

	// 확장 예시: 새 타일 종류를 추가할 때 아래처럼 메서드를 추가하세요.
	// public static Tile Water()
	// {
	//     return new Tile
	//     {
	//         name = "Water",
	//         effect = TileEffect.None,
	//         isObjectExist = false,
	//         isStructureExist = false,
	//         dangerous = 1,
	//         understand = 0,
	//         weight = 2,
	//         visibility = 0
	//     };
	// }
}

// ── Room ID Generator ──
// 방을 생성할 때 전역 고유 ID를 발급합니다.
// 사용법: RoomIdGenerator.Reset();
//        var id = RoomIdGenerator.GetNextId();
//        var name = RoomIdGenerator.FormatName(id);
public static class RoomIdGenerator
{
	private static int nextId = 0;

	public static int GetNextId() => nextId++;
	public static string FormatName(int id) => $"room{id}";
	public static void Reset() => nextId = 0;
}

// ── Floor Config 기본 설정 팩토리 ──
// 디자인 문서 기준 층별 기본 FloorConfig를 생성합니다.
public static class FloorConfigFactory
{
	public static FloorConfig[] CreateDefault()
	{
		return new FloorConfig[]
		{
			// Floor 0: 던전 입구(2026-08-20, "던전 입구 구조 프로그래머 지시서") — 1×3 청크 고정
			// 프리셋(가시 영역) + 최좌측에 플레이어에게 안 보이는 1×1 스폰 청크 1개, 합쳐서
			// width=4, height=1. GenerateFloor0은 이 4청크 전체를 여전히 단일 StartRoom으로 균일하게
			// 채운다(기존 로직 그대로) — 숨김 스폰 청크가 "안 보임"은 별도 렌더링 분리 없이
			// CameraController의 층별 카메라 관찰 범위 제한(Floor0HiddenChunksX)만으로 구현한다.
			// 맵 크기 1.5배 확장(2026-08-23 사용자 요청 "0층은 청크 크기만 늘려") — 청크 개수
			// (width/height)는 그대로 두고 청크 자체의 타일 크기만 8→12로 다른 층과 통일했다.
			// CameraController.Floor0ChunkSizeTiles/FogOfWarSystem의 0층 안개 스폰은 이 값을 그대로
			// 읽어가므로 자동으로 맞는다 — 다만 HumanWaveManager의 DungeonEntranceHiddenChunkCenterX/
			// DungeonEntranceRoomEntryX(0층 던전 입구 대기 위치)는 손으로 미리 계산해둔 상수라 이
			// chunkSize를 다시 바꾸면 그 두 값도 반드시 같이(비율 그대로) 맞춰야 한다.
			new FloorConfig
			{
				floorId = FloorId.Floor_0,
				width = 4, height = 1,
				normalRoomCount = 0, subPurposeRoomCount = 0,
				bossRoomFormat = "",
				wallThicknessMin = 1, wallThicknessMax = 1,
				maxNormalRoomChunks = 0,
				totalRoomCount = 1,
				chunkSize = 12
			},
			// Floor 1: 7×7 (청크 개수는 그대로, 청크 자체 크기만 1.5배 — 아래 청크 크기 주석 참고)
			new FloorConfig
			{
				floorId = FloorId.Floor_1,
				width = 7, height = 7,
				normalRoomCount = 4, subPurposeRoomCount = 1,
				bossRoomFormat = "2x2",
				wallThicknessMin = 1, wallThicknessMax = 2,
				maxNormalRoomChunks = 2,
				totalRoomCount = 6,
				chunkSize = 12
			},
			// Floor 2: 8×8
			new FloorConfig
			{
				floorId = FloorId.Floor_2,
				width = 8, height = 8,
				normalRoomCount = 5, subPurposeRoomCount = 2,
				bossRoomFormat = "ㄷ7",
				wallThicknessMin = 1, wallThicknessMax = 6,
				maxNormalRoomChunks = 3,
				totalRoomCount = 7,
				chunkSize = 12
			},
			// Floor 3: 9×9
			new FloorConfig
			{
				floorId = FloorId.Floor_3,
				width = 9, height = 9,
				normalRoomCount = 6, subPurposeRoomCount = 3,
				bossRoomFormat = "3x3",
				wallThicknessMin = 1, wallThicknessMax = 6,
				maxNormalRoomChunks = 5,
				totalRoomCount = 8,
				chunkSize = 12
			}
		};
	}
}

