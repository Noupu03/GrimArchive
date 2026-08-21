using System.Collections.Generic;
using UnityEngine;
using VContainer;
using Haare.Client.Routine;
using Haare.Client.UI;
using GrimArchive.Wave;

// 웨이브 시각화(2026-08-19 신규) — 인류 파티의 던전 도착까지 남은 시간을 화면 상단 가로형 게이지로
// 보여준다("웨이브 시각화 프로그래머 지시서" 구현 대상). DebugInfoPanel/StatusInfoPanel/
// BuildingControlPanel과 동일 관례 — [PanelAttribute]로 등록된 얇은 UGUI 프리팹 껍데기 + 실제
// 그리기는 OnGUI로 처리한다(이 프로젝트에서 이미 검증된 패턴).
[PanelAttribute("Prefabs/WaveGaugePanel")]
public class WaveGaugePanel : MonoRoutine, ICustomPanel
{
    public SceneUIManager uiManager { get; set; }
    public GameObject panel { get; set; }

    // DebugInfoPanel.Instance/BuildingControlPanel.Instance와 동일 관례 — NoticeCenter가 "지금 이
    // 게이지가 화면 위쪽을 얼마나 차지하고 있는지" 물어볼 때 쓴다(2026-08-21, 사용자 요청 "인류 웨이브
    // 시각화 UI 크기를 50% 늘려줘. 그에 따라 notice UI 생성 위치도 같이 내려줘").
    public static WaveGaugePanel Instance { get; private set; }

    private UnitSpriteManager _unitSpriteManager;

    [Inject]
    public void Construct(UnitSpriteManager unitSpriteManager)
    {
        _unitSpriteManager = unitSpriteManager;
    }

    protected override void Constructor()
    {
        base.Constructor();
        Instance = this;
    }

    public void OpenPanel()
    {
        gameObject.SetActive(true);
        panel = gameObject;
    }

    public void ClosePanel()
    {
        gameObject.SetActive(false);
    }

    public void BindEvent() { }

    // ── 스프라이트 ────────────────────────────────────────────────
    // Assets/Resources/UI/KtoDUI_1.png — 좌우 끝에 "왕국의 문"/"던전의 문"을 겸하는 장식 프레임
    // (_0, 346x23)과 그 안에 들어가는 얇은 진행 트랙(_1, 304x9) 두 서브스프라이트로 이미 잘려있다.
    // 별도의 문/게이지 이미지를 새로 만들지 않고 이 한 장을 그대로 재사용한다(사용자 지정 UI).
    private const string GaugeSpriteResourcePath = "UI/KtoDUI_1";
    private const string FrameSpriteName = "KtoDUI_1_0";
    private const string TrackSpriteName = "KtoDUI_1_1";

    private Sprite _frameSprite;
    private Sprite _trackSprite;
    private bool _spritesLoadAttempted;

    // ── 레이아웃 ──────────────────────────────────────────────────
    // 2026-08-21, 사용자 요청 "인류 웨이브 시각화 UI 크기를 50% 늘려줘" — BarWidth/PartyIconSize를
    // 1.5배(480→720/26→39). barHeight는 BarWidth에서 스프라이트 비율로 계산되므로(아래 OnGUI) 같이
    // 커진다 — NoticeCenter의 TopMargin 프로퍼티가 이 클래스의 GetReservedTopHeight()를 통해 그
    // 커진 높이를 실시간으로 반영한다(하드코딩 상수로 따로 안 둠 — 방 점령색 alpha 드리프트 버그와
    // 같은 종류의 실수를 막기 위함).
    private const float BarWidth = 720f;
    private const float BarTopMargin = 10f;
    private const float PartyIconSize = 39f;

    // 도착 임박 점멸 속도 — 문서가 "정확한 점멸 속도는 구현 후 플레이 테스트를 통해 조정한다"고
    // 명시 위임했으므로 우선 자리표시자 값을 쓴다. 점멸 시작 시점 자체는 진행도 임계값이 아니라
    // HumanWaveManager.IsMonstersSummonedThisCycle(2026-08-20 수정, 사용자 요청 "잠시후 웨이브가
    // 시작됩니다 문구 및 관련 표시들 등장하는 시점을 0층 몬스터 소환 시점으로 바꿔줘") — 실제로
    // 플레이어 몬스터들이 배치 위치로 소집되는 순간과 항상 같이 켜진다.
    private const float BlinkSpeed = 6f;

    private void EnsureSprites()
    {
        if (_spritesLoadAttempted) return;
        _spritesLoadAttempted = true;

        Sprite[] all = Resources.LoadAll<Sprite>(GaugeSpriteResourcePath);
        if (all == null) return;

        foreach (var s in all)
        {
            if (s == null) continue;
            if (s.name == FrameSpriteName) _frameSprite = s;
            else if (s.name == TrackSpriteName) _trackSprite = s;
        }
    }

    // 게이지 프레임 스프라이트의 실제 가로세로비로 계산한 세로 크기(스프라이트가 아직 안 불려왔으면
    // 0). OnGUI와 GetReservedTopHeight()가 공유해서, 이 UI가 커질 때(BarWidth 변경) 두 값이 항상
    // 같이 맞아떨어진다.
    private float GetBarHeight()
    {
        EnsureSprites();
        if (_frameSprite == null) return 0f;
        return BarWidth * (_frameSprite.rect.height / _frameSprite.rect.width);
    }

    // NoticeCenter가 이 게이지 바로 아래에 자기 UI를 배치하려고 물어보는 공개 API(2026-08-21,
    // 사용자 요청 "그에 따라 notice UI 생성 위치도 같이 내려줘") — BottomMenuBar.
    // GetReservedBottomLeftHeight와 동일한 관례. 스프라이트를 아직 못 불러왔으면(초기 프레임 등)
    // 안전하게 BarTopMargin만 반환한다.
    public float GetReservedTopHeight() => BarTopMargin + GetBarHeight();

    private void OnGUI()
    {
        HumanWaveManager wm = HumanWaveManager.Instance;
        if (wm == null) return;

        EnsureSprites();
        if (_frameSprite == null || _trackSprite == null) return;

        // 2026-08-20, 사용자 요청 — waveData에 설정된 시간(waveCooldown) 그대로가 진행 바 시간이
        // 되도록 HumanWaveManager.WaveProgress01(cooldownTimer/waveCooldown 비율)을 그대로 쓴다.
        // HumanWaveManager가 스폰 시점을 waveCooldown 예산에 맞춰 미리 계산해두므로, 바가 100%에
        // 도달하는 순간(cooldownTimer==0)이 곧 인간 파티의 "1층 진입 시작" 순간과 일치한다.
        float progress = Mathf.Clamp01(wm.WaveProgress01);
        bool waveApproaching = progress < 1f; // 1층 진입 시작 전까지는 계속 "다가오는 중" 취급.

        bool imminent = waveApproaching && wm.IsMonstersSummonedThisCycle;
        float blinkAlpha = imminent ? (0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * BlinkSpeed)) : 1f;

        float barHeight = GetBarHeight();
        Rect barRect = new Rect((Screen.width - BarWidth) * 0.5f, BarTopMargin, BarWidth, barHeight);

        // "왕국의 문 ─ 던전의 문" 장식 캡 폭만큼 좌우로 인셋하고, 프레임 안쪽 세로 중앙에 맞춰
        // 트랙(회색 바)을 프레임 그래픽 내부에 통합한다(사용자 피드백 2026-08-19 "별도 말고 통합.
        // 프레임 안에") — 별도 바로 분리하지 않고 프레임 위에 겹쳐 그린다.
        float insetXRatio = Mathf.Clamp01((_frameSprite.rect.width - _trackSprite.rect.width) / (2f * _frameSprite.rect.width));
        float trackHRatio = Mathf.Clamp01(_trackSprite.rect.height / _frameSprite.rect.height);

        Rect trackRect = new Rect(
            barRect.x + barRect.width * insetXRatio,
            barRect.y + barRect.height * (0.5f - trackHRatio * 0.5f),
            barRect.width * (1f - insetXRatio * 2f),
            barRect.height * trackHRatio
        );

        Color prevColor = GUI.color;

        // 1) 프레임("왕국의 문" ─ 게이지 ─ "던전의 문") — 배경은 원래 그림 그대로, 손대지 않는다.
        GUI.color = Color.white;
        GUISpriteUtil.Draw(_frameSprite, barRect);

        // 2) 프레임 안쪽 회색 트랙 — 진행도만큼만 그려서 그 자체가 차오르는 것처럼 보이게 한다
        // (사용자 피드백 2026-08-19 "백그라운드는 기존으로 두고, 회색 바가 차오르는 방식으로").
        // 별도 빈 상태 배경 레이어 없이 트랙 자체를 폭만 진행도에 맞춰 그린다.
        GUI.color = imminent ? new Color(1f, 0.82f, 0.25f, blinkAlpha) : Color.white;
        DrawSpritePartialWidth(_trackSprite, trackRect, progress);

        GUI.color = prevColor;

        // 3) 인간 파티 스프라이트(최대 3개, 겹쳐진 묶음) — 게이지 진행 위치와 동기화.
        DrawPartyBundle(wm, trackRect, progress);
    }

    private void DrawPartyBundle(HumanWaveManager wm, Rect trackRect, float progress)
    {
        List<string> typeNames = wm.GetApproachingPartyTypeNames(3);
        if (typeNames.Count == 0) return;

        float markerX = trackRect.x + trackRect.width * progress;
        float markerY = trackRect.y + trackRect.height * 0.5f;

        // 개별 유닛 정보 표시가 목적이 아니라 "파티가 이동 중"이라는 상황 전달이 목적이므로, 길게
        // 나열하지 않고 서로 가까이 겹쳐진 묶음 형태로 배치한다.
        Vector2[] offsets = { new Vector2(-8f, 4f), new Vector2(8f, 4f), new Vector2(0f, -6f) };

        for (int i = 0; i < typeNames.Count; i++)
        {
            Sprite icon = _unitSpriteManager != null ? _unitSpriteManager.GetIcon(typeNames[i]) : null;
            if (icon == null) continue;

            Vector2 offset = offsets[i % offsets.Length];
            float aspect = icon.rect.width > 0f ? icon.rect.height / icon.rect.width : 1f;
            float w = PartyIconSize;
            float h = PartyIconSize * aspect;

            Rect iconRect = new Rect(markerX + offset.x - w * 0.5f, markerY + offset.y - h * 0.5f, w, h);
            GUISpriteUtil.Draw(icon, iconRect);
        }
    }

    // 왼쪽부터 widthRatio(0~1)만큼만 잘라 그린다 — 진행도에 따라 트랙이 차오르는 효과.
    private static void DrawSpritePartialWidth(Sprite sprite, Rect fullScreenRect, float widthRatio)
    {
        widthRatio = Mathf.Clamp01(widthRatio);
        if (widthRatio <= 0f) return;

        Texture2D tex = sprite.texture;
        Rect r = sprite.rect;
        Rect uv = new Rect(r.x / tex.width, r.y / tex.height, (r.width * widthRatio) / tex.width, r.height / tex.height);
        Rect screenRect = new Rect(fullScreenRect.x, fullScreenRect.y, fullScreenRect.width * widthRatio, fullScreenRect.height);
        GUI.DrawTextureWithTexCoords(screenRect, tex, uv);
    }
}
