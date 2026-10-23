using System;
using System.Collections.Generic;

namespace opp_lab_1_zirolio_ctrl.Models;

public class SpreadsheetEngine
{
    private readonly Dictionary<string, string> _expressions = new();
    private readonly Dictionary<string, double> _cache = new();
    private readonly HashSet<string> _visited = new();

    public void Clear()
    {
        _expressions.Clear();
        _cache.Clear();
        _visited.Clear();
    }

    public void SetExpression(string cellName, string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            _expressions.Remove(cellName);
        }
        else
        {
            _expressions[cellName] = expression;
        }

        _cache.Clear();
    }

    public string GetExpression(string cellName)
    {
        return _expressions.TryGetValue(cellName, out var expr) ? expr : string.Empty;
    }

    public string EvaluateCell(string cellName)
    {
        cellName = cellName.ToUpper();

        if (_cache.TryGetValue(cellName, out var cachedVal))
            return cachedVal.ToString();

        if (!_expressions.TryGetValue(cellName, out var expr) || string.IsNullOrWhiteSpace(expr))
            return "0";

        if (_visited.Contains(cellName))
            throw new InvalidOperationException($"Циклічне посилання у клітинці {cellName}!");

        _visited.Add(cellName);

        string result = expr;
        
        try
        {
            
            if (expr.StartsWith("="))
            {
                string formulaBody = expr[1..];

                double evaluatedResult = ExpressionEvaluator.Evaluate(formulaBody, name =>
                {
                    if (!_expressions.TryGetValue(name, out var referencedExpr) || string.IsNullOrWhiteSpace(referencedExpr))
                    {
                        throw new InvalidOperationException($"Клітинка {name} порожня або не існує!");
                    }
                    
                    var evaluate = EvaluateCell(name);
                    if (double.TryParse(evaluate, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double num))
                    {
                        return num;
                    }

                    throw new InvalidOperationException($"Не число використовується у виразі в клітинці {cellName}!");
                });

                _cache[cellName] = evaluatedResult;
                result = evaluatedResult.ToString();
            }
        }
        finally
        {
            _visited.Remove(cellName);
        }


        return result;
    }
}