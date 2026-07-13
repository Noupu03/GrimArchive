using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace UnitDeploymentSystem
{
    public interface IBehavior
    {
        void ExecuteBehavior(Unit unit);
    }

    public class PathData
    {
        public Vector3[] Points { get; set; }
    }

    public class PathfindingService
    {
        // 싱글톤 혹은 의존성 주입으로 제공된다고 가정
        public static PathfindingService Instance { get; set; } = new PathfindingService();

        public async UniTask<PathData> CalculatePathAsync(Vector3 start, Vector3 end, CancellationToken ct)
        {
            // 실제 길찾기 로직 (여기서는 더미 딜레이)
            await UniTask.Delay(100, cancellationToken: ct);
            return new PathData { Points = new Vector3[] { start, end } };
        }
    }
}
