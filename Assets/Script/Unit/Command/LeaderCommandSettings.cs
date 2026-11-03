// 리더 명령 시스템의 켜짐 스위치와 설정값 — RetreatSystem.Enabled와 같은 방식(테스트용 덮어쓰기 + 설정 에셋 + 에셋이 없으면 문서 기본값).
public static class LeaderCommandSettings
{
	// 테스트·debug용 — null이 아니면 설정 에셋 대신 이 값을 쓴다. 기본은 켜짐(설정 에셋이 없어도 켜지고, 에셋의 kill-switch로만 끈다).
	public static bool? EnabledOverride;
	public static bool Enabled => EnabledOverride ?? (AIConfigLoader.Behavior?.leaderCommandSystemEnabled ?? true);

	// 11-1장 접근 제한 거리(칸). 0 = 사용 안 함. 테스트용 덮어쓰기가 있다.
	public static float? ApproachLimitOverride;
	public static float ApproachLimit => ApproachLimitOverride ?? (AIConfigLoader.Behavior?.leaderApproachLimitDistance ?? 0f);

	public static bool RallyRulesEnabled => Enabled && (AIConfigLoader.Behavior?.leaderRallyRulesEnabled ?? true);
}
