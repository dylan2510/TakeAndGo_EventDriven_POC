namespace Tag.Contracts;
public static class EventNames
{
    public const string EntryScanAccepted = "Entry.ScanAccepted";
    public const string ExitScanAccepted  = "Exit.ScanAccepted";
    public const string DoorOpenRequested = "Door.OpenRequested";
    public const string EntryGranted      = "Entry.Granted";
    public const string DisplayAppend     = "Display.Append";
    public const string DisplayRemove     = "Display.Remove";
    public const string VisitTimedOut     = "VisitSession.TimedOut";
}