namespace TimmyTools.WpfUi.Models;

public class BreakTimerSettingsModel : BaseModel
{
    public string ClassTitle { get; set => SetProperty(ref field, value); } = "";
    public string NextUp { get; set => SetProperty(ref field, value); } = "";
    public int Segment { get; set => SetProperty(ref field, value); } = 0;
}
