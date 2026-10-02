using UnityEngine;

// 03번 10장·05번 8장: 이 유닛이 "유효하게 아는" 리더의 마지막 위치 — 직접 확인·전파·집결 명령으로만 갱신된다.
// 실제 리더 위치를 읽어 이동 목적지로 쓰지 않기 위한 정보 격리 경계다.
public class KnownLeaderInfo
{
	public Human Leader;
	public Vector2Int Position;
	public float Timestamp;

	// 03번 11장: 이 위치에 도착해 부재를 확인한 기록. 새 정보가 오기 전까지 같은 위치를 목적지로 다시 고르지 않는다.
	public Vector2Int? EmptyConfirmedPosition;
	public float EmptyConfirmedTimestamp;

	// 더 최신인 정보일 때만 갱신한다(07문서 13장). 리더가 바뀌었으면 시점과 무관하게 교체한다.
	public bool Update(Human leader, Vector2Int position, float timestamp)
	{
		if (leader == null) return false;
		bool sameLeader = Leader == leader;
		if (sameLeader && timestamp < Timestamp) return false;

		Leader = leader;
		Position = position;
		Timestamp = timestamp;
		// 부재 확인 이후에 얻은 정보만 그 기록을 풀 수 있다(더 오래된 정보가 같은 빈 위치를 되살리지 못한다).
		if (!sameLeader || (EmptyConfirmedPosition.HasValue && timestamp > EmptyConfirmedTimestamp))
			EmptyConfirmedPosition = null;
		return true;
	}

	public void MarkEmpty(Vector2Int position, float timestamp)
	{
		EmptyConfirmedPosition = position;
		EmptyConfirmedTimestamp = timestamp;
	}

	public bool IsFor(Human currentLeader) => Leader != null && Leader == currentLeader;

	public void Clear()
	{
		Leader = null;
		Position = default;
		Timestamp = 0f;
		EmptyConfirmedPosition = null;
		EmptyConfirmedTimestamp = 0f;
	}
}
