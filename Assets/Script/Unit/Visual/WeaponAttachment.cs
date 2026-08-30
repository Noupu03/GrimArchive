using UnityEngine;

// 유닛 프리팹의 Visual 자식(예: Visual/WeaponSocket)에 붙여 8방향(Dir)에 맞춰 무기 스프라이트의
// 위치/회전/정렬순서를 조정하는 범용 컴포넌트 — 유닛/무기 종류 무관하게 재사용 가능하며 프리팹마다
// poses/nativeSpriteAngle만 튜닝하면 된다. UnitSpriteManager.GetSpriteLabelForDirection()과 동일하게
// UP/UP_LEFT/LEFT/DOWN_LEFT/DOWN 5개 기준 방향만 데이터로 갖고 RIGHT 계열은 좌우 미러링으로 재사용한다.
[RequireComponent(typeof(SpriteRenderer))]
public class WeaponAttachment : MonoBehaviour
{
    [System.Serializable]
    public class DirectionalPose
    {
        public Dir direction;
        public Vector2 offset;

        // "스프라이트가 시각적으로 향해야 할 각도" (0=오른쪽, 90=위, 반시계 +) — SkillAction.GetRotationForDirection과
        // 동일한 각도 컨벤션. 실제 적용 회전값은 nativeSpriteAngle을 빼서 자동 보정된다.
        [Range(-180f, 180f)] public float targetAngle;
        public int sortingOrder = 10;
    }

    [Header("스프라이트 자체 방향 보정")]
    [Tooltip("무기 스프라이트가 회전 0도일 때 실제로 그려진 방향각(0=오른쪽, 90=위, 반시계 +).\n" +
             "Weapon_LongSwordA는 피벗이 우하단(칼자루)이고 칼날이 좌상단을 향해 그려져 있어 135도.")]
    [SerializeField] private float nativeSpriteAngle = 135f;

    [SerializeField]
    private DirectionalPose[] poses = new DirectionalPose[]
    {
        new DirectionalPose { direction = Dir.UP,        offset = new Vector2(0f,     0.7f),  targetAngle = 90f,   sortingOrder = 8  },
        new DirectionalPose { direction = Dir.UP_LEFT,   offset = new Vector2(-0.32f, 0.32f), targetAngle = 135f,  sortingOrder = 8  },
        new DirectionalPose { direction = Dir.LEFT,      offset = new Vector2(-0.15f,  0.35f),  targetAngle = 180f,  sortingOrder = 11 },
        new DirectionalPose { direction = Dir.DOWN_LEFT, offset = new Vector2(-0.2f, -0.42f),targetAngle = -135f, sortingOrder = 11 },
        new DirectionalPose { direction = Dir.DOWN,      offset = new Vector2(0f,    0.25f),  targetAngle = -90f,  sortingOrder = 11 },
    };

    private SpriteRenderer _sr;
    private Dir _lastDir = (Dir)(-1);

    private void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
    }

    public void UpdatePose(Dir direction)
    {
        if (direction == _lastDir) return;
        _lastDir = direction;

        bool mirror = direction == Dir.RIGHT || direction == Dir.UP_RIGHT || direction == Dir.DOWN_RIGHT;
        Dir lookup = direction switch
        {
            Dir.RIGHT      => Dir.LEFT,
            Dir.UP_RIGHT   => Dir.UP_LEFT,
            Dir.DOWN_RIGHT => Dir.DOWN_LEFT,
            _              => direction
        };

        DirectionalPose pose = System.Array.Find(poses, p => p.direction == lookup);
        if (pose == null) return;

        _sr.flipX = mirror;
        transform.localPosition = new Vector3(mirror ? -pose.offset.x : pose.offset.x, pose.offset.y, 0f);

        // 미러링 시 목표 각도 자체도 좌우 반사(180 - angle)되고, 스프라이트 보정각도 flipX로 인해
        // 같이 반사(180 - nativeSpriteAngle)되므로 두 반사가 상쇄되어 부호만 반대가 된다.
        float targetAngle = mirror ? 180f - pose.targetAngle : pose.targetAngle;
        float nativeAngle  = mirror ? 180f - nativeSpriteAngle : nativeSpriteAngle;
        transform.localRotation = Quaternion.Euler(0f, 0f, targetAngle - nativeAngle);

        _sr.sortingOrder = pose.sortingOrder;
    }
}
