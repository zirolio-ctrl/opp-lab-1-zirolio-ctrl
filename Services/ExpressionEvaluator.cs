using System;
using System.Collections.Generic;
using System.Globalization;

namespace opp_lab_1_zirolio_ctrl.Models;

public sealed class ExpressionEvaluator
{
    private readonly Func<string, double>? _resolveCell;
    private List<string> _tokens = [];
    private int _position;

    public ExpressionEvaluator(Func<string, double>? resolveCell = null)
    {
        _resolveCell = resolveCell;
    }

    public double Evaluate(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            throw new ArgumentException("Expression cannot be empty.");

        expression = expression.Trim();

        if (expression.StartsWith('=')) expression = expression[1..];

        _tokens = Tokenize(expression);
        _position = 0;

        double result = ParseExpression();

        if (_position < _tokens.Count)
            throw new FormatException($"Unexpected token: {_tokens[_position]}");

        if (double.IsNaN(result) || double.IsInfinity(result))
            throw new ArithmeticException("Invalid calculation result.");

        return result;
    }

    private static List<string> Tokenize(string expression)
    {
        var tokens = new List<string>();
        int i = 0;

        while (i < expression.Length)
        {
            char c = expression[i];

            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            if (char.IsDigit(c) || c == '.')
            {
                int start = i;
                bool hasDecimalPoint = false;

                while (i < expression.Length)
                {
                    if (char.IsDigit(expression[i]))
                    {
                        i++;
                    }
                    else if (expression[i] == '.' && !hasDecimalPoint)
                    {
                        hasDecimalPoint = true;
                        i++;
                    }
                    else
                    {
                        break;
                    }
                }

                string number = expression[start..i];

                if (!double.TryParse(
                        number,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out _))
                {
                    throw new FormatException(
                        $"Invalid number: {number}");
                }

                tokens.Add(number);
                continue;
            }

            if (char.IsLetter(c))
            {
                int start = i;

                while (i < expression.Length && char.IsLetterOrDigit(expression[i]))
                {
                    i++;
                }

                tokens.Add(expression[start..i]);
                continue;
            }

            if ("+-*/^()".Contains(c))
            {
                tokens.Add(c.ToString());
                i++;
                continue;
            }

            throw new FormatException($"Invalid character: '{c}'");
        }

        if (tokens.Count == 0)
            throw new FormatException("Expression is empty.");

        return tokens;
    }

    private double ParseExpression()
    {
        double left = ParseTerm();

        while (Match("+") || Match("-"))
        {
            string op = Previous();
            double right = ParseTerm();

            left = op switch
            {
                "+" => left + right,
                "-" => left - right,
                _ => throw new InvalidOperationException()
            };
        }

        return left;
    }

    // Multiplication, division, div and mod.
    private double ParseTerm()
    {
        double left = ParseUnary();

        while   (Match("*") ||
                Match("/") ||
                MatchKeyword("div") ||
                MatchKeyword("mod"))
        {
            string op = Previous().ToLowerInvariant();
            double right = ParseUnary();

            if ((op == "/" || op == "div" || op == "mod") &&
                right == 0)
            {
                throw new DivideByZeroException(
                    "Division by zero is not allowed.");
            }

            left = op switch
            {
                "*" => left * right,
                "/" => left / right,
                "div" => Math.Truncate(left / right),
                "mod" => left % right,
                _ => throw new InvalidOperationException()
            };
        }

        return left;
    }

    // Unary + and -.
    private double ParseUnary()
    {
        if (Match("+")) return ParseUnary();
        if (Match("-")) return -ParseUnary();

        return ParsePower();
    }

    // Exponentiation.
    private double ParsePower()
    {
        double left = ParsePrimary();

        if (Match("^"))
        {
            double exponent = ParseUnary();
            left = Math.Pow(left, exponent);

            if (double.IsNaN(left) || double.IsInfinity(left))
                throw new ArithmeticException("Invalid exponentiation result.");
        }

        return left;
    }

    private double ParsePrimary()
    {
        if (Match("("))
        {
            double value = ParseExpression();

            if (!Match(")"))
                throw new FormatException("Missing closing parenthesis.");

            return value;
        }

        if (_position >= _tokens.Count)
            throw new FormatException("Expected a number.");

        string token = _tokens[_position++];

        if (double.TryParse(
                token,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double number))
        {
            return number;
        }

        if (IsCellReference(token))
        {
            if (_resolveCell is null)
            {
                throw new InvalidOperationException($"No cell resolver provided for '{token}'.");
            }

            return _resolveCell(token.ToUpperInvariant());
        }

        throw new FormatException($"Invalid expression token: {token}");
    }

    private static bool IsCellReference(string token)
    {
        int i = 0;

        while (i < token.Length && char.IsLetter(token[i]))
            i++;

        if (i == 0 || i == token.Length)
            return false;

        while (i < token.Length && char.IsDigit(token[i]))
            i++;

        return i == token.Length;
    }

    private bool Match(string expected)
    {
        if (_position >= _tokens.Count ||
            _tokens[_position] != expected)
        {
            return false;
        }

        _position++;
        return true;
    }

    private bool MatchKeyword(string expected)
    {
        if (_position >= _tokens.Count ||
            !string.Equals(
                _tokens[_position],
                expected,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        _position++;
        return true;
    }

    private string Previous() => _tokens[_position - 1];

    public static double Evaluate(string expression, Func<string, double> GetCellValue)
    {
        try
        {
            expression = expression.Replace(',', '.');

            var evaluator = new ExpressionEvaluator(cell => GetCellValue(cell));

            object result = evaluator.Evaluate(expression);

            if (result == null || result == DBNull.Value)
                return 0;

            return Convert.ToDouble(result, CultureInfo.InvariantCulture);
        }
        catch (Exception ex)
        {

            throw new InvalidOperationException($"Помилка обчислення виразу: {ex.Message}");
        }
    }
}