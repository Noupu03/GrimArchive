using UnityEngine;

// 문 한 짝(타일 한 칸)의 비주얼 — 문 오브젝트(1×2 묶음)는 칸마다 이 컴포넌트를 가진 자식 스프라이트 한 장씩을 둔다(양쪽으로 젖혀지는 예전 모양 유지).
// 진행 막대(ObjectProgressBarVisual)도 같은 비주얼 아래 SpriteRenderer를 만들므로, 문 스프라이트만 가려내려고 구분용 표식으로 쓴다.
public class DoorLeafVisual : MonoBehaviour
{
	public SpriteRenderer Renderer;
}
