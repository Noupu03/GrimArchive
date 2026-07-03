using System.Threading;
using Cysharp.Threading.Tasks;
using Haare.Client.Routine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Demo.LobbyScene
{
    public class DemoCubeRotator : MonoRoutine
    {
        public float rotationSpeed = 50f;

        public override async UniTask Initialize(CancellationToken cts)
        {
            await base.Initialize(cts);
            await UniTask.CompletedTask;
        }
        // 매 프레임마다 호출되는 Update 함수입니다.
        protected override void UpdateProcess()
        {
            this.transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);

            // 원본은 레거시 Input Manager(Input.GetKeyDown)를 썼는데, 이 프로젝트는
            // 새 Input System 전용(activeInputHandler: 1)이라 그대로 두면 매 프레임 예외가 난다.
            if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
            {
                Destroy(gameObject);
            }
        }
        
        
        
    }
}