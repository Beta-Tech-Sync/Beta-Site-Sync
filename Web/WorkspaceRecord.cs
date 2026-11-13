namespace SiteSyncTracker.Web;

public sealed class WorkspaceRecord
{
    public string OwnerId { get; set; } = "";
    public int Revision { get; set; }
    public string DataJson { get; set; } = "";
}
