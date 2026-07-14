namespace PTGOilSystem.Web.Diagnostics;

public sealed class MvcRequestTimingState
{
    private int _queryCount;

    public int QueryCount => Volatile.Read(ref _queryCount);

    public void Reset() => Volatile.Write(ref _queryCount, 0);

    public void IncrementQueryCount() => Interlocked.Increment(ref _queryCount);
}
