using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using VContainer;

public class InputManager : MonoBehaviour
{
	// GoapCore/Actions 등 DI로 닿지 않는 순수 C# 로직(ScriptableObject 기반 Unit/AI)이 계속 참조하므로 유지한다.
	public static InputManager Instance;
	public Unit selectedUnit;

	private UnitGenerate _unitGenerate;

	[Inject]
	public void Construct(UnitGenerate unitGenerate)
	{
		_unitGenerate = unitGenerate;
	}

	void Awake()
	{
		Instance = this;
	}

	private bool IsPointInFootprint(Vector3Int pos, Unit u)
	{
		int w = (int)u.unitType.footprint.x;
		int h = (int)u.unitType.footprint.y;

		return (pos.x >= u.position.x && pos.x < u.position.x + w &&
				pos.y >= u.position.y && pos.y < u.position.y + h);
	}

	void Update()
	{
		if (GameSession.Instance == null) return;
		if (Keyboard.current == null || Mouse.current == null) return;

		int currentFloor = 1;
		Vector3 floorOffset =
			_unitGenerate != null
			? _unitGenerate.GetFloorOffset(currentFloor)
			: Vector3.zero;

		// =====================================================
		// 좌클릭
		// =====================================================
		if (Mouse.current.leftButton.wasPressedThisFrame)
		{
			Vector2 mousePos = Mouse.current.position.ReadValue();

			Vector3 worldPoint = Camera.main.ScreenToWorldPoint(
				new Vector3(mousePos.x, mousePos.y, Mathf.Abs(Camera.main.transform.position.z))
			);

			Vector3 localPoint = worldPoint - floorOffset;

			Vector3Int gridPos = new Vector3Int(
				Mathf.FloorToInt(localPoint.x),
				Mathf.FloorToInt(localPoint.y),
				currentFloor
			);

			// =================================================
			// 1. 먼저 "유닛 선택" (최우선)
			// =================================================
			Unit clickedUnit = null;

			foreach (var u in GameSession.Instance.units)
			{
				if (u == null || u.hp <= 0) continue;
				if (u.currentFloor != currentFloor) continue;

				if (IsPointInFootprint(gridPos, u))
				{
					clickedUnit = u;
					break;
				}
			}

			// =================================================
			// 2. 유닛 클릭이면 무조건 선택 (진영 제한 없음)
			// =================================================
			if (clickedUnit != null)
			{
				selectedUnit = clickedUnit;
				Debug.Log($"선택: {clickedUnit.unitType.typeName}");
				return;
			}

			// =================================================
			// 3. 공격 처리 (선택된 상태에서만)
			// =================================================
			if (selectedUnit != null && selectedUnit.hp > 0)
			{
				foreach (var u in GameSession.Instance.units)
				{
					if (u == selectedUnit || u.hp <= 0) continue;
					if (u.currentFloor != currentFloor) continue;

					if (IsPointInFootprint(gridPos, u))
					{
						bool isEnemy =
							(selectedUnit is Monster && u is Human) ||
							(selectedUnit is Human && u is Monster);

						if (isEnemy)
						{
							selectedUnit.playerAttackTarget = u;
							selectedUnit.playerMoveTarget = null;

							Debug.Log(
								$"공격 명령: {selectedUnit.unitType.typeName} -> {u.unitType.typeName}"
							);
							return;
						}
					}
				}
			}

			// =================================================
			// 4. 허공 클릭 → 선택 해제
			// =================================================
			selectedUnit = null;
		}

		// =====================================================
		// 우클릭 (이동)
		// =====================================================
		if (Mouse.current.rightButton.wasPressedThisFrame &&
			selectedUnit != null &&
			selectedUnit.hp > 0)
		{
			Vector2 mousePos = Mouse.current.position.ReadValue();

			Vector3 worldPoint = Camera.main.ScreenToWorldPoint(
				new Vector3(mousePos.x, mousePos.y, Mathf.Abs(Camera.main.transform.position.z))
			);

			Vector3 localPoint = worldPoint - floorOffset;

			Vector3Int gridPos = new Vector3Int(
				Mathf.FloorToInt(localPoint.x),
				Mathf.FloorToInt(localPoint.y),
				currentFloor
			);

			selectedUnit.playerMoveTarget = new Vector2Int(gridPos.x, gridPos.y);
			selectedUnit.playerAttackTarget = null;

			Debug.Log(
				$"이동 명령: {selectedUnit.unitType.typeName} -> ({gridPos.x}, {gridPos.y})"
			);
		}

		// =====================================================
		// 속도 / 일시정지 (그대로 유지)
		// =====================================================
		if (Keyboard.current.spaceKey.wasPressedThisFrame)
		{
			GameSession.Instance.isPaused = !GameSession.Instance.isPaused;
			Time.timeScale = GameSession.Instance.isPaused
				? 0.0001f
				: GameSession.Instance.currentGameSpeed;
		}
		if (Keyboard.current.digit0Key.wasPressedThisFrame)
		{
			GameSession.Instance.currentGameSpeed = 0.5f;
			if (!GameSession.Instance.isPaused) Time.timeScale = 0.5f;
		}

		if (Keyboard.current.digit1Key.wasPressedThisFrame)
		{
			GameSession.Instance.currentGameSpeed = 1f;
			if (!GameSession.Instance.isPaused) Time.timeScale = 1f;
		}

		if (Keyboard.current.digit2Key.wasPressedThisFrame)
		{
			GameSession.Instance.currentGameSpeed = 2f;
			if (!GameSession.Instance.isPaused) Time.timeScale = 2f;
		}

		if (Keyboard.current.digit3Key.wasPressedThisFrame)
		{
			GameSession.Instance.currentGameSpeed = 3f;
			if (!GameSession.Instance.isPaused) Time.timeScale = 3f;
		}
	}
}
