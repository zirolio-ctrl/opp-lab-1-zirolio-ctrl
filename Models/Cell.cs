using System.Collections.Generic;

namespace opp_lab_1_zirolio_ctrl.Models;

public class Cell
{
    public string Name { get; set; } = string.Empty;
    public string Expression { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public HashSet<string> Dependencies { get; set; } = new();
}