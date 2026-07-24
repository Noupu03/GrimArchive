public interface IFSMState
{
	float    GetPriority(Unit unit);
	bool     IsSticky(Unit unit);
	bool     ShouldInterrupt(Unit unit);
	void     OnEnter(Unit unit);
	void     OnExit(Unit unit);
	BTStatus Tick(Unit unit);
	string   GetLabel(Unit unit);
}
