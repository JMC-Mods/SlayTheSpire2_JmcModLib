namespace JmcModLib.Persistence.Run;

internal readonly record struct RunIdentity(int ProfileId, long StartTime, bool IsMultiplayer)
{
    public string FileStem
    {
        get
        {
            string mode = IsMultiplayer ? "mp" : "sp";
            return $"{mode}-{StartTime}";
        }
    }
}
