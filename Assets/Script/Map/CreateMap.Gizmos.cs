using System.Collections.Generic;
using UnityEngine;

public partial class CreateMap
{
#if UNITY_EDITOR
    // 맵 생성 로직이 MonoBehaviour에서 NativeRoutine으로 전환되면서 기존 기즈모 그리기 코드가 동작하지 않게 됨.
    // 차후 SceneView 렌더링용으로 리팩토링하기 전까지 비워둠.
#endif
}