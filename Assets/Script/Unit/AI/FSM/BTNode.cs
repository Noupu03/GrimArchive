using System;

public enum BTStatus { Success, Failure, Running }

public abstract class BTNode
{
	public abstract BTStatus Tick(Unit unit);
}

public class BTSequence : BTNode
{
	private readonly BTNode[] _children;
	public BTSequence(params BTNode[] children) => _children = children;

	public override BTStatus Tick(Unit unit)
	{
		foreach (var c in _children)
		{
			var s = c.Tick(unit);
			if (s != BTStatus.Success) return s;
		}
		return BTStatus.Success;
	}
}

public class BTSelector : BTNode
{
	private readonly BTNode[] _children;
	public BTSelector(params BTNode[] children) => _children = children;

	public override BTStatus Tick(Unit unit)
	{
		foreach (var c in _children)
		{
			var s = c.Tick(unit);
			if (s != BTStatus.Failure) return s;
		}
		return BTStatus.Failure;
	}
}

public class BTCondition : BTNode
{
	private readonly Func<Unit, bool> _predicate;
	public BTCondition(Func<Unit, bool> predicate) => _predicate = predicate;

	public override BTStatus Tick(Unit unit)
		=> _predicate(unit) ? BTStatus.Success : BTStatus.Failure;
}

public class BTLeaf : BTNode
{
	private readonly Func<Unit, BTStatus> _action;
	public BTLeaf(Func<Unit, BTStatus> action) => _action = action;

	public override BTStatus Tick(Unit unit) => _action(unit);
}
