using UnityEngine;

// 유닛 프리팹의 Visual 자식에 붙여 몸 스프라이트 방향에 맞춰 무기 스프라이트의 위치/회전/정렬순서를 조정하는
// 범용 컴포넌트 — 프리팹마다 poses/nativeSpriteAngle만 튜닝하면 재사용 가능하다.
// 몸 스프라이트가 반측면 정면/후면 2종 + 좌우 반전(SpriteFacingMath, 회의록 2026-11-09)이 되면서 실제로 읽는
// 포즈는 DOWN_LEFT(정면)·UP_LEFT(후면) 둘뿐이고 오른쪽은 미러링으로 재사용한다. poses의 UP/LEFT/DOWN 항목은
// 더 이상 쓰이지 않지만 프리팹 직렬화 데이터라 그대로 둔다.
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
    private bool _lastMirror;

    private void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
    }

    // facing: 몸 스프라이트와 같은 SpriteFacing — PoseDir(UP_LEFT/DOWN_LEFT)로 포즈를 찾고 FlipX면 미러링한다.
    public void UpdatePose(SpriteFacing facing)
    {
        Dir lookup = facing.PoseDir;
        bool mirror = facing.FlipX;
        if (lookup == _lastDir && mirror == _lastMirror) return;
        _lastDir = lookup;
        _lastMirror = mirror;

        DirectionalPose pose = System.Array.Find(poses, p => p.direction == lookup);
        if (pose == null) return;

        _sr.flipX = mirror;
        transform.localPosition = new Vector3(mirror ? -pose.offset.x : pose.offset.x, pose.offset.y, 0f);

        // 미러링 시 목표 각도(180-angle)와 보정각도(180-nativeSpriteAngle) 둘 다 반사되므로 두 반사가
        // 상쇄되어 부호만 반대가 된다.
        float targetAngle = mirror ? 180f - pose.targetAngle : pose.targetAngle;
        float nativeAngle  = mirror ? 180f - nativeSpriteAngle : nativeSpriteAngle;
        transform.localRotation = Quaternion.Euler(0f, 0f, targetAngle - nativeAngle);

        _sr.sortingOrder = pose.sortingOrder;
    }
}
