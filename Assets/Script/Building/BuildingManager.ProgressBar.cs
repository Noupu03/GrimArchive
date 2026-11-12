using System.Collections.Generic;
using UnityEngine;

// 건설 설계명세서 v0.2 2-13 - 외부 작업 진행도 바(시설·홀로그램 하단). 중단되면 막대는 멈춘 채 흐려지고, 완료·취소·파괴되면 사라진다.
public partial class BuildingManager
{
	private readonly Dictionary<BuildingData, FacilityProgressBar> _progressBars = new Dictionary<BuildingData, FacilityProgressBar>();
	private float _nextProgressBarAt;

	private void UpdateProgressBars()
	{
		if (Time.unscaledTime < _nextProgressBarAt) return;
		_nextProgressBarAt = Time.unscaledTime + 0.1f;

		List<BuildingData> stale = null;
		foreach (var kv in _progressBars)
			if (!_allBuildings.Contains(kv.Key)) (stale ??= new List<BuildingData>()).Add(kv.Key);
		if (stale != null)
			foreach (BuildingData s in stale) { _progressBars[s].Destroy(); _progressBars.Remove(s); }

		for (int i = 0; i < _allBuildings.Count; i++)
		{
			BuildingData b = _allBuildings[i];
			if (b == null || b.IsDummy) continue;

			bool show = ShouldShowProgressBar(b, out float progress, out float originalConstruction);
			if (!_progressBars.TryGetValue(b, out FacilityProgressBar bar))
			{
				if (!show) continue;
				bar = new FacilityProgressBar($"WorkBar_{b.FacilityId}");
				_progressBars[b] = bar;
			}
			if (!show) { bar.Hide(); continue; }

			Vector3 off = unitGenerate != null ? unitGenerate.GetFloorOffset(b.Position.z) : Vector3.zero;
			float width = Mathf.Max(1f, b.Footprint.x * 0.9f);
			var center = new Vector3(b.Position.x + b.Footprint.x * 0.5f, b.Position.y - 0.28f, 0f) + off;
			bool stopped = !string.IsNullOrEmpty(b.WaitReason);
			Color fill = stopped ? new Color(0.62f, 0.62f, 0.62f, 1f) : new Color(0.35f, 0.9f, 0.45f, 1f);
			bar.Show(center, width, progress, fill, originalConstruction);
		}
	}

	private bool ShouldShowProgressBar(BuildingData b, out float progress, out float originalConstruction)
	{
		progress = 0f;
		originalConstruction = -1f;
		if (b.ActiveFieldTask == FacilityFieldTaskKind.None) return false;
		if (b.VisualObject == null || !b.VisualObject.activeSelf) return false;

		// 착공 전 건설 계획(홀로그램)에는 진행 바가 없다 - 클릭하면 건설·위치 변경·취소 메뉴가 열린다.
		if (b.ActiveFieldTask == FacilityFieldTaskKind.Construction && (b.ActiveConstructionOrUpgrade == null || !b.ActiveConstructionOrUpgrade.HasStartedWork)) return false;

		progress = GetTaskProgress01(b);
		if (b.ActiveFieldTask == FacilityFieldTaskKind.Relocation && b.MoveOfUnfinishedConstruction && b.ActiveConstructionOrUpgrade != null)
			originalConstruction = Mathf.Clamp01((float)b.ActiveConstructionOrUpgrade.Progress01);
		return true;
	}
}
