using System.Collections.Generic;

namespace opp_lab_1_zirolio_ctrl.Models;

public sealed class SpreadsheetFile
{
    public int Rows { get; set; }
    public int Columns { get; set; }

    public Dictionary<string, string> Cells { get; set; } = new();
}