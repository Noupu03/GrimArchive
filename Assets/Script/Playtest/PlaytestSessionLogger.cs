using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using GrimArchive.Wave;

// 체험판(1층 보스까지) 플레이테스트용 세션 기록기 — "Kubonhon" 씬에서만 켜진다.
// 게임 상태를 읽기만 하고 바꾸지 않는다(폴링 + NoticeCenter 알림 구독 + 콘솔 오류 구독).
// 기록 위치: 에디터는 <프로젝트>/PlaytestLogs/, 빌드는 Application.persistentDataPath/PlaytestLogs/.
// 테스터 단축키(F8/F9)는 2026-11-18 플레이어용 씬 정리로 제거했다.
public class PlaytestSessionLogger : MonoBehaviour
{
	public const string TargetSceneName = "Kubonhon";
	private const float PollInterval = 0.5f;
	private const float ResourceSnapshotInterval = 60f;
	private const float IdleWarnSeconds = 30f;

	private static PlaytestSessionLogger _instance;

	private string _dir;
	private string _logPath;
	private StreamWriter _writer;
	private float _realStart;
	private float _nextPoll;
	private float _nextResourceSnapshot;
	private float _lastInputRealTime;
	private bool _idleWarned;

	// 이전 폴링 상태
	private readonly Dictionary<Unit, (string name, string faction, float hp, Vector2Int pos, int floor)> _known = new Dictionary<Unit, (string, string, float, Vector2Int, int)>();
	private int _lastWaveNumber = -1;
	private string _lastWaveState = "";
	private int _lastPlayerRooms = -1;
	private bool _bossSeenAlive;
	private bool _bossKilled;
	private bool _coreActivated;
	private bool _lastPaused;
	private bool _demoGoalReached;

	// 집계
	private readonly List<(string label, float real, float game)> _milestones = new List<(string, float, float)>();
	private int _playerDeaths, _humanDeaths, _wildDeaths, _humanExits;
	private int _leftClicks, _rightClicks;
	private int _errorCount;
	private readonly Dictionary<string, int> _noticeCounts = new Dictionary<string, int>();
	private string _lastNoticeKey, _lastNoticeText;
	private float _lastNoticeTime;
	private string _flash;
	private float _flashUntil;

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void Bootstrap()
	{
		SceneManager.sceneLoaded -= OnSceneLoaded;
		SceneManager.sceneLoaded += OnSceneLoaded;
		TryCreate(SceneManager.GetActiveScene());
	}

	private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryCreate(scene);

	private static void TryCreate(Scene scene)
	{
		if (_instance != null || scene.name != TargetSceneName) return;
		var go = new GameObject("[PlaytestSessionLogger]");
		_instance = go.AddComponent<PlaytestSessionLogger>();
	}

	private void Awake()
	{
		string root = Application.isEditor
			? Path.Combine(Directory.GetParent(Application.dataPath).FullName, "PlaytestLogs")
			: Path.Combine(Application.persistentDataPath, "PlaytestLogs");
		string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
		_dir = Path.Combine(root, "session_" + stamp);
		try
		{
			Directory.CreateDirectory(_dir);
			_logPath = Path.Combine(_dir, "log.txt");
			_writer = new StreamWriter(_logPath, false, new UTF8Encoding(true)) { AutoFlush = true };
		}
		catch (Exception e)
		{
			Debug.LogWarning("[Playtest] 로그 파일을 만들 수 없음: " + e.Message);
		}
		_realStart = Time.realtimeSinceStartup;
		_lastInputRealTime = _realStart;
		Write("세션 시작", $"씬={SceneManager.GetActiveScene().name}, 버전={Application.version}, 기록 위치={_dir}");

		NoticeCenter.OnNoticePushed += OnNotice;
		Application.logMessageReceived += OnLog;
	}

	private void OnDestroy()
	{
		NoticeCenter.OnNoticePushed -= OnNotice;
		Application.logMessageReceived -= OnLog;
		WriteSummary();
		_writer?.Dispose();
		_writer = null;
		if (_instance == this) _instance = null;
	}

	private void OnApplicationQuit() => WriteSummary();

	private void Update()
	{
		ReadInput();
		if (Time.realtimeSinceStartup >= _nextPoll)
		{
			_nextPoll = Time.realtimeSinceStartup + PollInterval;
			Poll();
		}
	}

	// ───────── 입력 ─────────
	private void ReadInput()
	{
		var kb = Keyboard.current;
		var ms = Mouse.current;
		bool any = false;
		if (ms != null)
		{
			if (ms.leftButton.wasPressedThisFrame) { _leftClicks++; any = true; }
			if (ms.rightButton.wasPressedThisFrame) { _rightClicks++; any = true; }
			if (Mathf.Abs(ms.scroll.ReadValue().y) > 0.01f) any = true;
		}
		if (kb != null)
		{
			if (kb.anyKey.wasPressedThisFrame) any = true;
		}
		if (any)
		{
			if (_idleWarned) Write("입력 재개", $"{Time.realtimeSinceStartup - _lastInputRealTime:0}초 만에 입력");
			_lastInputRealTime = Time.realtimeSinceStartup;
			_idleWarned = false;
		}
	}

	// ───────── 상태 폴링 ─────────
	private void Poll()
	{
		var session = GameSession.Instance;
		if (session == null) return;

		// 일시정지
		if (session.isPaused != _lastPaused)
		{
			_lastPaused = session.isPaused;
			Write(_lastPaused ? "일시정지" : "재개", "");
		}

		// 입력 없음
		if (!_idleWarned && !session.isPaused && Time.realtimeSinceStartup - _lastInputRealTime >= IdleWarnSeconds)
		{
			_idleWarned = true;
			Write("입력 없음", $"{IdleWarnSeconds:0}초 동안 입력 없음(게임 진행 중) — {DescribeContext()}");
		}

		PollUnits(session);
		PollWave();
		PollRooms(session);
		PollCore(session);

		if (Time.realtimeSinceStartup >= _nextResourceSnapshot)
		{
			_nextResourceSnapshot = Time.realtimeSinceStartup + ResourceSnapshotInterval;
			Write("현황", DescribeContext());
		}
	}

	private void PollUnits(GameSession session)
	{
		var alive = new HashSet<Unit>();
		bool bossAlive = false;
		foreach (var u in session.units)
		{
			if (u == null) continue;
			alive.Add(u);
			string name = u.unitType != null ? u.unitType.typeName : u.GetType().Name;
			string faction = FactionOf(u);
			if (u.unitType != null && u.unitType.isBoss) bossAlive = true;
			if (!_known.ContainsKey(u) && _known.Count > 0 && faction == "아군")
				Write("아군 유닛 등장", $"{name} @({u.position.x},{u.position.y}) {u.currentFloor}층");
			_known[u] = (name, faction, u.Health != null ? u.hp : 0f, u.position, u.currentFloor);
		}

		var gone = new List<Unit>();
		foreach (var kv in _known) if (!alive.Contains(kv.Key)) gone.Add(kv.Key);
		foreach (var u in gone)
		{
			var info = _known[u];
			_known.Remove(u);
			bool died = u == null || info.hp <= 0f || (u.Health != null && u.hp <= 0f);
			if (died)
			{
				if (info.faction == "아군") _playerDeaths++;
				else if (info.faction == "인류") _humanDeaths++;
				else _wildDeaths++;
				if (info.faction == "아군" || u?.unitType?.isBoss == true)
					Write($"{info.faction} 사망", $"{info.name} @({info.pos.x},{info.pos.y}) {info.floor}층");
			}
			else if (info.faction == "인류")
			{
				_humanExits++;
			}
		}

		if (bossAlive && !_bossSeenAlive) { _bossSeenAlive = true; }
		if (_bossSeenAlive && !bossAlive && !_bossKilled)
		{
			_bossKilled = true;
			Milestone("1층 보스 처치");
		}
	}

	private void PollWave()
	{
		var wm = HumanWaveManager.Instance;
		if (wm == null) return;
		string state = wm.currentState.ToString();
		if (wm.WaveNumber != _lastWaveNumber || state != _lastWaveState)
		{
			if (state == "Running" && _lastWaveState != "Running") Milestone($"웨이브 {wm.WaveNumber} 시작");
			else if (_lastWaveState == "Running" && state != "Running") Milestone($"웨이브 {wm.WaveNumber} 종료");
			_lastWaveNumber = wm.WaveNumber;
			_lastWaveState = state;
		}
	}

	private void PollRooms(GameSession session)
	{
		int owned = 0;
		foreach (var r in session.allRooms) if (r != null && r.RoomFaction == FactionType.Player) owned++;
		if (owned != _lastPlayerRooms)
		{
			if (_lastPlayerRooms >= 0)
			{
				if (owned > _lastPlayerRooms) Milestone($"방 확보 → 확보한 방 {owned}개");
				else Write("방 상실", $"확보한 방 {_lastPlayerRooms} → {owned}");
			}
			_lastPlayerRooms = owned;
		}
	}

	private void PollCore(GameSession session)
	{
		if (_coreActivated || session.Core == null) return;
		if (session.Core.TryGetFloorCore(1, out _, out var core) && core != null && core.CoreEverActivated)
		{
			_coreActivated = true;
			Milestone("1층 코어 완전 활성화");
			if (!_demoGoalReached)
			{
				_demoGoalReached = true;
				Milestone("★ 체험판 목표 달성");
				_flash = "체험판 목표 달성! (1층 코어 활성화)";
				_flashUntil = Time.realtimeSinceStartup + 6f;
			}
		}
	}

	// ───────── 알림 / 오류 ─────────
	private void OnNotice(string key, string text)
	{
		if (string.IsNullOrEmpty(text)) return;
		float now = Time.realtimeSinceStartup;
		// 같은 알림이 매 프레임 다시 올라오는 경우(고정형 갱신 등)는 2초 안에 한 번만 기록
		if (key == _lastNoticeKey && text == _lastNoticeText && now - _lastNoticeTime < 2f) return;
		_lastNoticeKey = key; _lastNoticeText = text; _lastNoticeTime = now;
		string plain = StripTags(text);
		_noticeCounts.TryGetValue(plain, out int c);
		_noticeCounts[plain] = c + 1;
		Write("알림", key != null ? $"[{key}] {plain}" : plain);
	}

	private void OnLog(string condition, string stackTrace, LogType type)
	{
		if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert) return;
		_errorCount++;
		if (_errorCount > 200) return; // 폭주 시 파일 보호
		string firstStack = stackTrace;
		if (!string.IsNullOrEmpty(firstStack))
		{
			var lines = firstStack.Split('\n');
			firstStack = string.Join(" | ", lines, 0, Mathf.Min(3, lines.Length));
		}
		Write($"오류({type})", $"{condition} :: {firstStack}");
	}

	// ───────── 기록 ─────────
	private void Milestone(string label)
	{
		float real = Time.realtimeSinceStartup - _realStart;
		_milestones.Add((label, real, Time.time));
		Write("◆ " + label, DescribeContext());
	}

	private void Write(string tag, string detail)
	{
		float real = Time.realtimeSinceStartup - _realStart;
		string line = $"[{FormatTime(real)} | 게임 {Time.time:0.0}s] {tag}" + (string.IsNullOrEmpty(detail) ? "" : " — " + detail);
		try { _writer?.WriteLine(line); } catch { }
	}

	private bool _summaryWritten;
	private void WriteSummary()
	{
		if (_summaryWritten || _writer == null) return;
		_summaryWritten = true;
		float real = Time.realtimeSinceStartup - _realStart;
		var sb = new StringBuilder();
		sb.AppendLine();
		sb.AppendLine("==================== 세션 요약 ====================");
		sb.AppendLine($"총 플레이 시간(실제): {FormatTime(real)} / 게임 시간: {Time.time:0}s");
		sb.AppendLine($"체험판 목표(1층 코어 활성화): {(_demoGoalReached ? "달성" : "미달성")}");
		sb.AppendLine("주요 시점:");
		if (_milestones.Count == 0) sb.AppendLine("  (없음)");
		foreach (var m in _milestones) sb.AppendLine($"  {FormatTime(m.real)}  {m.label}");
		sb.AppendLine($"사망 — 아군 {_playerDeaths} / 인류 {_humanDeaths} / 야생 {_wildDeaths}, 인류 이탈 {_humanExits}");
		sb.AppendLine($"클릭 — 좌 {_leftClicks} / 우 {_rightClicks}");
		sb.AppendLine($"콘솔 오류 {_errorCount}");
		sb.AppendLine("자주 뜬 알림(상위 10):");
		var list = new List<KeyValuePair<string, int>>(_noticeCounts);
		list.Sort((a, b) => b.Value.CompareTo(a.Value));
		for (int i = 0; i < Mathf.Min(10, list.Count); i++) sb.AppendLine($"  {list[i].Value}회  {list[i].Key}");
		try { _writer.Write(sb.ToString()); } catch { }
	}

	private string DescribeContext()
	{
		var session = GameSession.Instance;
		var wm = HumanWaveManager.Instance;
		var rm = ResourceManager.Instance;
		int ally = 0, human = 0, wild = 0;
		if (session != null)
			foreach (var u in session.units)
			{
				if (u == null) continue;
				switch (FactionOf(u)) { case "아군": ally++; break; case "인류": human++; break; default: wild++; break; }
			}
		string wave = wm != null ? $"웨이브 {wm.WaveNumber}({wm.currentState})" : "웨이브 ?";
		string res = rm != null ? $"생명에너지 {rm.LifeEnergyAccount.DisplayBalance}, 영혼파편 {rm.SoulShardAccount.DisplayBalance}" : "";
		string cam = CameraController.Instance != null ? CameraController.Instance.CurrentFloor.ToString() : "?";
		return $"{wave}, 확보한 방 {Mathf.Max(0, _lastPlayerRooms)}, 유닛 아군 {ally}/인류 {human}/야생 {wild}, {res}, 보는 층 {cam}";
	}

	private static string FactionOf(Unit u)
	{
		if (u.IsPlayerMonsterFaction) return "아군";
		if (u.IsHumanFaction) return "인류";
		return "야생";
	}

	private static string FormatTime(float seconds)
	{
		var t = TimeSpan.FromSeconds(Mathf.Max(0f, seconds));
		return $"{(int)t.TotalMinutes:00}:{t.Seconds:00}";
	}

	private static string StripTags(string s) => System.Text.RegularExpressions.Regex.Replace(s, "<.*?>", "");

	// ───────── 화면 안내 ─────────
	private GUIStyle _style;
	private void OnGUI()
	{
		if (_style == null)
		{
			_style = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.UpperCenter };
			_style.normal.textColor = new Color(1f, 1f, 1f, 0.55f);
			GUIMenuStyleUtil.LockStateColors(_style);
		}
		// 플레이어 화면에는 상시 안내를 띄우지 않는다(2026-11-18) — 목표 달성 같은 짧은 확인 문구만 보인다. 기록은 백그라운드로 계속된다.
		if (_flash == null || Time.realtimeSinceStartup >= _flashUntil) return;
		string text = _flash;
		GUI.Label(new Rect((Screen.width - 410) * 0.5f, 2, 410, 40), text, _style);
	}
}
