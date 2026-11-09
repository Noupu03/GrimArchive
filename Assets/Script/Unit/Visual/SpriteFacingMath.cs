// 유닛 스프라이트 방향 환산 — 논리 방향 Dir(8방향, 시야·전투·이동용)을 스프라이트 4방향으로 접는 순수 함수.
// 회의록 2026-11-09: 기존 5종(정면/반측면/측면/후면 라벨 Up·Down·Left·UpLeft·DownLeft)을
// "반측면 정면(DownLeft) + 반측면 후면(UpLeft)" 2종으로 줄이고 좌우는 반전으로 처리한다 = 4방향.
// Dir 자체는 8방향 그대로다 — 논리는 건드리지 않고 "그림을 고르는 단계"에서만 접는다.
//  - 위쪽 성분이 있으면(UP·UP_LEFT·UP_RIGHT) 후면, 그 밖(DOWN 계열·LEFT·RIGHT)은 정면. 순수 가로(LEFT/RIGHT)는 정면(사용자 확정).
//  - 오른쪽 성분이 있으면(RIGHT 계열) 좌우 반전. 순수 UP/DOWN은 좌우가 정해지지 않으므로 직전 반전 상태를 유지한다(사용자 확정).
public readonly struct SpriteFacing : System.IEquatable<SpriteFacing>
{
	public readonly bool Back;   // true = 반측면 후면, false = 반측면 정면
	public readonly bool FlipX;  // true = 오른쪽(좌우 반전), false = 왼쪽

	public SpriteFacing(bool back, bool flipX)
	{
		Back = back;
		FlipX = flipX;
	}

	// 스프라이트 라이브러리 라벨 — 기존 라이브러리의 라벨을 그대로 재사용한다(에셋 변경 없음).
	public string Label => Back ? SpriteFacingMath.BackLabel : SpriteFacingMath.FrontLabel;

	// 무기 포즈 조회 키 — 왼쪽 기준 포즈(UP_LEFT/DOWN_LEFT)만 쓰고 오른쪽은 반전으로 재사용한다.
	public Dir PoseDir => Back ? Dir.UP_LEFT : Dir.DOWN_LEFT;

	public bool Equals(SpriteFacing other) => Back == other.Back && FlipX == other.FlipX;
	public override bool Equals(object obj) => obj is SpriteFacing other && Equals(other);
	public override int GetHashCode() => (Back ? 1 : 0) | (FlipX ? 2 : 0);
}

public static class SpriteFacingMath
{
	public const string FrontLabel = "DownLeft";
	public const string BackLabel  = "UpLeft";

	// previousFlipX: 이 유닛이 직전에 적용한 좌우 반전 — UP/DOWN일 때만 쓰인다. 최초 값은 false(왼쪽).
	public static SpriteFacing Resolve(Dir dir, bool previousFlipX)
	{
		bool back = dir == Dir.UP || dir == Dir.UP_LEFT || dir == Dir.UP_RIGHT;

		bool flipX;
		switch (dir)
		{
			case Dir.RIGHT:
			case Dir.UP_RIGHT:
			case Dir.DOWN_RIGHT:
				flipX = true;
				break;
			case Dir.LEFT:
			case Dir.UP_LEFT:
			case Dir.DOWN_LEFT:
				flipX = false;
				break;
			default: // UP, DOWN(+범위 밖 값) — 좌우 성분이 없으니 직전 상태 유지
				flipX = previousFlipX;
				break;
		}

		return new SpriteFacing(back, flipX);
	}
}
