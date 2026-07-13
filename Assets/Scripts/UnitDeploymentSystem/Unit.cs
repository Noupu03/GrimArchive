using System;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;

namespace UnitDeploymentSystem
{
    public class Unit : MonoBehaviour
    {
        [SerializeField] private int populationCost = 2;
        public int PopulationCost => populationCost;

        public Room CurrentRoom { get; private set; }
        public IBehavior CurrentBehavior { get; private set; }
        public bool IsMoving { get; private set; }

        private CancellationTokenSource moveCts;

        public void ChangeRoom(Room newRoom)
        {
            if (CurrentRoom != null)
            {
                CurrentRoom.RemoveUnit(this);
            }

            CurrentRoom = newRoom;
            
            if (CurrentRoom != null)
            {
                CurrentRoom.AddUnit(this);
            }
        }

        public void SetBehavior(IBehavior newBehavior)
        {
            CurrentBehavior = newBehavior;
        }

        // HAARE Framework: Native Routine (UniTask 기반 비동기 메서드)
        public async UniTask MoveToRoomRoutine(Room targetRoom, CancellationToken ct)
        {
            // 이미 취소된 토큰이면 즉시 종료
            ct.ThrowIfCancellationRequested();

            IsMoving = true;

            try
            {
                // 소속 즉시 변경 (이동 시작 시점에 목적지 방의 유닛으로 취급)
                ChangeRoom(targetRoom);

                // 경로 탐색 대기 (비동기 처리)
                PathData path = await PathfindingService.Instance.CalculatePathAsync(transform.position, targetRoom.transform.position, ct);

                // 실제 이동 로직 실행
                await MovePathAsync(path, ct);
            }
            catch (OperationCanceledException)
            {
                // 이동 취소됨 (새로운 명령 하달 시 정상적인 흐름)
                Debug.Log($"[Unit] MoveToRoomRoutine Canceled for {name}.");
                throw; // 필요한 경우 상위 콜스택으로 전파
            }
            finally
            {
                IsMoving = false;
            }
        }

        // HAARE Framework: 실제 물리적 이동 처리를 위한 Native Routine
        private async UniTask MovePathAsync(PathData path, CancellationToken ct)
        {
            // 더미 이동 로직 (실제로는 NavMeshAgent 등 사용)
            if (path == null || path.Points == null || path.Points.Length == 0) return;

            Vector3 destination = path.Points[path.Points.Length - 1];
            float speed = 5f;

            while (Vector3.Distance(transform.position, destination) > 0.1f)
            {
                ct.ThrowIfCancellationRequested();
                transform.position = Vector3.MoveTowards(transform.position, destination, speed * Time.deltaTime);
                await UniTask.Yield(PlayerLoopTiming.Update, ct); // 프레임 대기
            }
        }

        // 새로운 이동 명령이 하달될 때 기존 토큰을 취소하고 새 토큰을 셋업하는 유틸 메서드
        public void IssueMoveCommand(Room targetRoom)
        {
            // 기존 이동 명령 취소
            if (moveCts != null)
            {
                moveCts.Cancel();
                moveCts.Dispose();
            }

            // 새로운 토큰 발급
            moveCts = new CancellationTokenSource();
            
            // Native Routine 실행 (파이어 앤 포겟)
            MoveToRoomRoutine(targetRoom, moveCts.Token).Forget();
        }

        private void OnDestroy()
        {
            if (moveCts != null)
            {
                moveCts.Cancel();
                moveCts.Dispose();
            }
        }
    }
}
