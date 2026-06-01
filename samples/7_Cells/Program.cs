using System;
using System.IO;
using System.Numerics;
using System.Collections.Generic;
using System.Globalization;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.RegularExpressions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Documents;
namespace CellsSample;

public class Cell
{
    public string Name { get; }
    public string Formula { get; set; } = string.Empty;
    public string EvaluatedValue { get; set; } = string.Empty;
    public HashSet<string> References { get; } = new();
    public HashSet<string> Dependents { get; } = new();

    public Cell(string name)
    {
        Name = name;
    }
}

public class RowData
{
    public int RowIndex { get; }
    public string RowHeader => (RowIndex + 1).ToString();

    public RowData(int rowIndex)
    {
        RowIndex = rowIndex;
    }

    public string GetFormula(int colIdx)
    {
        string cellName = $"{(char)('A' + colIdx)}{RowIndex + 1}";
        return Program.GetCellFormula(cellName);
    }

    public string GetEvaluatedValue(int colIdx)
    {
        string cellName = $"{(char)('A' + colIdx)}{RowIndex + 1}";
        return Program.GetCellEvaluated(cellName);
    }

    public void SetFormula(int colIdx, string formula)
    {
        string cellName = $"{(char)('A' + colIdx)}{RowIndex + 1}";
        Program.SetCellFormula(cellName, formula);
    }

    // Properties for DataGrid binding using reflection
    public string A { get => GetFormula(0); set => SetFormula(0, value); }
    public string B { get => GetFormula(1); set => SetFormula(1, value); }
    public string C { get => GetFormula(2); set => SetFormula(2, value); }
    public string D { get => GetFormula(3); set => SetFormula(3, value); }
    public string E { get => GetFormula(4); set => SetFormula(4, value); }
    public string F { get => GetFormula(5); set => SetFormula(5, value); }
    public string G { get => GetFormula(6); set => SetFormula(6, value); }
    public string H { get => GetFormula(7); set => SetFormula(7, value); }
    public string I { get => GetFormula(8); set => SetFormula(8, value); }
    public string J { get => GetFormula(9); set => SetFormula(9, value); }
    public string K { get => GetFormula(10); set => SetFormula(10, value); }
    public string L { get => GetFormula(11); set => SetFormula(11, value); }
    public string M { get => GetFormula(12); set => SetFormula(12, value); }
    public string N { get => GetFormula(13); set => SetFormula(13, value); }
    public string O { get => GetFormula(14); set => SetFormula(14, value); }
    public string P { get => GetFormula(15); set => SetFormula(15, value); }
    public string Q { get => GetFormula(16); set => SetFormula(16, value); }
    public string R { get => GetFormula(17); set => SetFormula(17, value); }
    public string S { get => GetFormula(18); set => SetFormula(18, value); }
    public string T { get => GetFormula(19); set => SetFormula(19, value); }
    public string U { get => GetFormula(20); set => SetFormula(20, value); }
    public string V { get => GetFormula(21); set => SetFormula(21, value); }
    public string W { get => GetFormula(22); set => SetFormula(22, value); }
    public string X { get => GetFormula(23); set => SetFormula(23, value); }
    public string Y { get => GetFormula(24); set => SetFormula(24, value); }
    public string Z { get => GetFormula(25); set => SetFormula(25, value); }
}

public static class Program
{
    public static Dictionary<string, Cell> Spreadsheet { get; } = new();

    public static string GetCellFormula(string name)
    {
        if (Spreadsheet.TryGetValue(name, out var cell)) return cell.Formula;
        return string.Empty;
    }

    public static string GetCellEvaluated(string name)
    {
        if (Spreadsheet.TryGetValue(name, out var cell)) return cell.EvaluatedValue;
        return string.Empty;
    }

    public static void SetCellFormula(string cellName, string formula)
    {
        if (!Spreadsheet.TryGetValue(cellName, out var cell))
        {
            cell = new Cell(cellName);
            Spreadsheet[cellName] = cell;
        }

        // Remove old dependencies
        foreach (var oldRef in cell.References)
        {
            if (Spreadsheet.TryGetValue(oldRef, out var refCell))
            {
                refCell.Dependents.Remove(cellName);
            }
        }

        cell.Formula = formula;
        cell.References.Clear();

        // Parse new references from formula
        if (formula.StartsWith("="))
        {
            string expr = formula.Substring(1).ToUpperInvariant();
            
            // 1. Parse standard cell references (e.g. A1, B20)
            var matches = Regex.Matches(expr, @"[A-Z][0-9]{1,2}");
            foreach (Match m in matches)
            {
                string refName = m.Value;
                cell.References.Add(refName);
                
                if (!Spreadsheet.TryGetValue(refName, out var refCell))
                {
                    refCell = new Cell(refName);
                    Spreadsheet[refName] = refCell;
                }
                refCell.Dependents.Add(cellName);
            }

            // 2. Parse ranges like SUM(A1:B3) and add their references
            var rangeMatches = Regex.Matches(expr, @"[A-Z][0-9]{1,2}:[A-Z][0-9]{1,2}");
            foreach (Match m in rangeMatches)
            {
                var parts = m.Value.Split(':');
                var cells = ExpandRange(parts[0], parts[1]);
                foreach (var rCell in cells)
                {
                    cell.References.Add(rCell);
                    if (!Spreadsheet.TryGetValue(rCell, out var refCell))
                    {
                        refCell = new Cell(rCell);
                        Spreadsheet[rCell] = refCell;
                    }
                    refCell.Dependents.Add(cellName);
                }
            }
        }

        RecalculateCell(cellName);
    }

    private static List<string> ExpandRange(string start, string end)
    {
        var list = new List<string>();
        char startCol = start[0];
        int startRow = int.Parse(start.Substring(1));
        char endCol = end[0];
        int endRow = int.Parse(end.Substring(1));

        char minCol = (char)Math.Min(startCol, endCol);
        char maxCol = (char)Math.Max(startCol, endCol);
        int minRow = Math.Min(startRow, endRow);
        int maxRow = Math.Max(startRow, endRow);

        for (char c = minCol; c <= maxCol; c++)
        {
            for (int r = minRow; r <= maxRow; r++)
            {
                list.Add($"{c}{r}");
            }
        }
        return list;
    }

    public static void RecalculateCell(string cellName)
    {
        if (!Spreadsheet.TryGetValue(cellName, out var cell)) return;

        var visited = new HashSet<string>();
        if (HasCycle(cellName, visited))
        {
            cell.EvaluatedValue = "#REF!";
        }
        else
        {
            try
            {
                cell.EvaluatedValue = EvaluateFormula(cell.Formula);
            }
            catch
            {
                cell.EvaluatedValue = "#ERR!";
            }
        }

        // Recursively trigger recalculation on dependents
        foreach (var dep in cell.Dependents)
        {
            RecalculateCell(dep);
        }
    }

    private static bool HasCycle(string startCell, HashSet<string> visited)
    {
        if (visited.Contains(startCell)) return true;
        visited.Add(startCell);

        if (Spreadsheet.TryGetValue(startCell, out var cell))
        {
            foreach (var refCell in cell.References)
            {
                if (HasCycle(refCell, visited)) return true;
            }
        }

        visited.Remove(startCell);
        return false;
    }

    public static string EvaluateFormula(string formula)
    {
        if (string.IsNullOrEmpty(formula)) return string.Empty;
        if (!formula.StartsWith("=")) return formula;

        string expr = formula.Substring(1).Trim().ToUpperInvariant();

        // 1. Evaluate range functions like SUM(A1:B3) and PROD(A1:B3)
        expr = EvaluateFunctions(expr);

        // 2. Replace cell references with their evaluated values
        var matches = Regex.Matches(expr, @"[A-Z][0-9]{1,2}");
        var list = new List<Match>();
        foreach (Match m in matches) list.Add(m);
        list.Sort((x, y) => y.Index.CompareTo(x.Index)); // right to left replacement

        foreach (var m in list)
        {
            string refName = m.Value;
            string refVal = GetCellEvaluated(refName);
            if (refVal == "#REF!" || refVal == "#ERR!") throw new Exception("Error in reference");
            if (string.IsNullOrEmpty(refVal)) refVal = "0";

            expr = expr.Remove(m.Index, m.Length).Insert(m.Index, refVal);
        }

        // 3. Evaluate math
        try
        {
            double val = ParseAndEvalMath(expr);
            return val.ToString("G", CultureInfo.InvariantCulture);
        }
        catch
        {
            return "#ERR!";
        }
    }

    private static string EvaluateFunctions(string expr)
    {
        while (true)
        {
            var m = Regex.Match(expr, @"(SUM|PROD)\(([A-Z][0-9]{1,2}):([A-Z][0-9]{1,2})\)");
            if (!m.Success) break;

            string func = m.Groups[1].Value;
            string start = m.Groups[2].Value;
            string end = m.Groups[3].Value;

            var cells = ExpandRange(start, end);
            double result = func == "SUM" ? 0 : 1;

            foreach (var cell in cells)
            {
                string valStr = GetCellEvaluated(cell);
                if (valStr == "#REF!" || valStr == "#ERR!") throw new Exception("Error in reference");
                double val = 0;
                if (!string.IsNullOrEmpty(valStr))
                {
                    double.TryParse(valStr, CultureInfo.InvariantCulture, out val);
                }

                if (func == "SUM") result += val;
                else result *= val;
            }

            expr = expr.Remove(m.Index, m.Length).Insert(m.Index, result.ToString("G", CultureInfo.InvariantCulture));
        }
        return expr;
    }

    // --- RECURSIVE DESCENT MATH PARSER ---
    public static double ParseAndEvalMath(string expression)
    {
        int index = 0;
        
        double ParseExpression()
        {
            double result = ParseTerm();
            while (index < expression.Length)
            {
                char op = expression[index];
                if (op == '+' || op == '-')
                {
                    index++;
                    double next = ParseTerm();
                    if (op == '+') result += next;
                    else result -= next;
                }
                else break;
            }
            return result;
        }

        double ParseTerm()
        {
            double result = ParseFactor();
            while (index < expression.Length)
            {
                char op = expression[index];
                if (op == '*' || op == '/')
                {
                    index++;
                    double next = ParseFactor();
                    if (op == '*') result *= next;
                    else
                    {
                        if (next == 0) throw new DivideByZeroException();
                        result /= next;
                    }
                }
                else break;
            }
            return result;
        }

        double ParseFactor()
        {
            while (index < expression.Length && char.IsWhiteSpace(expression[index])) index++;
            if (index >= expression.Length) return 0;

            if (expression[index] == '(')
            {
                index++; // skip '('
                double result = ParseExpression();
                if (index < expression.Length && expression[index] == ')') index++; // skip ')'
                return result;
            }

            if (expression[index] == '-')
            {
                index++;
                return -ParseFactor();
            }

            int start = index;
            while (index < expression.Length && (char.IsDigit(expression[index]) || expression[index] == '.'))
            {
                index++;
            }
            string numStr = expression.Substring(start, index - start);
            if (double.TryParse(numStr, CultureInfo.InvariantCulture, out double val))
            {
                return val;
            }
            return 0;
        }

        expression = expression.Replace(" ", "");
        return ParseExpression();
    }

    public static void Main(string[] args)
    {
        AppBuilder<App>.Configure()
            .WithTitle("7GUI - 7. Cells Spreadsheet (WinUI Application)")
            .WithSize(900, 600)
            .Build()
            .Run(args);
    }
}

public class App : Application
{
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var window = new Window();
        window.Title = "7GUI - 7. Cells Spreadsheet";
        window.Width = 900;
        window.Height = 600;

        var rootGrid = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        var mainLayout = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Margin = new Thickness(16)
        };
        mainLayout.RowDefinitions.Add(new GridLength(70f, GridUnitType.Absolute)); // Header description
        mainLayout.RowDefinitions.Add(new GridLength(1f, GridUnitType.Star));       // DataGrid Area

        // Description Header
        var descStack = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 0, 0, 10) };
        var title = new RichTextBlock { FontSize = 16f };
        title.Inlines.Add(new Bold(new Run("7GUI - Reactive GPU Cells Spreadsheet")));
        descStack.AddChild(title);

        var subtitle = new RichTextBlock { FontSize = 11f, Foreground = ThemeManager.GetBrush("TextSecondary"), Margin = new Thickness(0, 4, 0, 0) };
        subtitle.Inlines.Add(new Run("Double-click any cell to edit. Enter raw formulas starting with "));
        subtitle.Inlines.Add(new Bold(new Run("=")) { Foreground = ThemeManager.GetBrush("SystemAccentColor") });
        subtitle.Inlines.Add(new Run(" (e.g., "));
        subtitle.Inlines.Add(new Bold(new Run("=A1+B1")) { Foreground = ThemeManager.GetBrush("SystemAccentColor") });
        subtitle.Inlines.Add(new Run(" or "));
        subtitle.Inlines.Add(new Bold(new Run("=SUM(A1:B3)")) { Foreground = ThemeManager.GetBrush("SystemAccentColor") });
        subtitle.Inlines.Add(new Run("). Supports topological dependency updates, error reporting (#ERR!), and cycle validation (#REF!)."));
        descStack.AddChild(subtitle);

        mainLayout.AddChild(descStack);
        Grid.SetRow(descStack, 0);

        // Virtualized DataGrid
        var dataGrid = new DataGrid
        {
            RowHeight = 28f,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        // 1. Add Row Header column
        dataGrid.Columns.Add(new DataGridColumn("Row", 60f, "RowHeader"));

        // 2. Add columns A to Z
        for (int i = 0; i < 26; i++)
        {
            string colLetter = ((char)('A' + i)).ToString();
            dataGrid.Columns.Add(new DataGridColumn(colLetter, 80f, colLetter));
        }

        // 3. Setup Direct Cell Value Binding
        dataGrid.CellValueBinding = (item, prop) =>
        {
            if (item is RowData row)
            {
                if (prop == "RowHeader") return row.RowHeader;

                int colIdx = prop[0] - 'A';
                
                // If this specific cell is in edit mode, return its raw formula!
                if (dataGrid.EditingRow == row.RowIndex && dataGrid.EditingCol == colIdx + 1) // +1 due to RowHeader col at 0
                {
                    return row.GetFormula(colIdx);
                }

                // Otherwise, show evaluated value
                return row.GetEvaluatedValue(colIdx);
            }
            return string.Empty;
        };

        // 4. Pre-populate 100 rows
        for (int r = 0; r < 100; r++)
        {
            dataGrid.AddItem(new RowData(r));
        }

        // Prepopulate a few sample formulas to wow the user!
        Program.SetCellFormula("A1", "10");
        Program.SetCellFormula("A2", "20");
        Program.SetCellFormula("B1", "=A1*A2");
        Program.SetCellFormula("C1", "=SUM(A1:B2)");

        mainLayout.AddChild(dataGrid);
        Grid.SetRow(dataGrid, 1);

        rootGrid.AddChild(mainLayout);
        
        window.Content = rootGrid;
        window.Activate();
    }
}
