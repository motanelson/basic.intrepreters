
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;

class BasicInterpreter
{
    private string[] lines = Array.Empty<string>();
    private Dictionary<string, int> labels = new();
    private Dictionary<string, object> variables = new();
    private Stack<int> callStack = new();

    private int pc;
    private bool running;
    private int currentLine;
    private readonly Random random = new();

    public void Run(string code)
    {
        Prepare(code);

        variables.Clear();
        callStack.Clear();
        pc = 0;
        running = true;

        Console.WriteLine();
        Console.WriteLine("--- Label-BASIC Interpreter ---");
        Console.WriteLine();

        while (running && pc < lines.Length)
        {
            int lineIndex = pc;
            string line = lines[pc++];

            currentLine = lineIndex + 1;

            if (string.IsNullOrWhiteSpace(line))
                continue;

            try
            {
                ExecuteLine(line);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error at line {currentLine}: {ex.Message}");
                Console.ResetColor();
                running = false;
            }
        }

        Console.WriteLine();
        Console.WriteLine("--- Execution finished ---");
    }

    public void Stop()
    {
        running = false;
    }

    private void Prepare(string code)
    {
        string[] rawLines = code.Replace("\r", "").Split('\n');

        List<string> prepared = new();
        labels.Clear();

        for (int i = 0; i < rawLines.Length; i++)
        {
            string line = rawLines[i].Trim();

            if (line.Equals("REM", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("REM ", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("'"))
            {
                line = "";
            }

            Match match = Regex.Match(
                line,
                @"^([a-zA-Z_][a-zA-Z0-9_]*):(.*)$"
            );

            if (match.Success)
            {
                string label = match.Groups[1].Value.ToLowerInvariant();

                if (labels.ContainsKey(label))
                    throw new Exception($"Duplicate label: {label}");

                labels[label] = prepared.Count;
                line = match.Groups[2].Value.Trim();
            }

            prepared.Add(line);
        }

        lines = prepared.ToArray();
    }

    private void ExecuteLine(string line)
    {
        // IF condition THEN statement
        Match m = Regex.Match(
            line,
            @"^IF\s+(.*?)\s+(?:THEN\s+GOTO|THEN|GOTO)\s+(.+)$",
            RegexOptions.IgnoreCase
        );

        if (m.Success)
        {
            object condition = Eval(m.Groups[1].Value);

            if (Truthy(condition))
            {
                ExecuteLine(m.Groups[2].Value.Trim());
            }

            return;
        }

        // GOTO
        m = Regex.Match(
            line,
            @"^GOTO\s+([a-zA-Z_][a-zA-Z0-9_]*)$",
            RegexOptions.IgnoreCase
        );

        if (m.Success)
        {
            Jump(m.Groups[1].Value);
            return;
        }

        // GOSUB
        m = Regex.Match(
            line,
            @"^GOSUB\s+([a-zA-Z_][a-zA-Z0-9_]*)$",
            RegexOptions.IgnoreCase
        );

        if (m.Success)
        {
            string label = m.Groups[1].Value.ToLowerInvariant();

            if (!labels.ContainsKey(label))
                throw new Exception($"Undefined label: {label}");

            callStack.Push(pc);
            pc = labels[label];
            return;
        }

        // RETURN
        if (Regex.IsMatch(line, @"^RETURN$", RegexOptions.IgnoreCase))
        {
            if (callStack.Count == 0)
                throw new Exception("RETURN without GOSUB");

            pc = callStack.Pop();
            return;
        }

        // PRINT
        if (Regex.IsMatch(line, @"^PRINT(?:\s+.*)?$", RegexOptions.IgnoreCase))
        {
            string expression = line.Length > 5
                ? line.Substring(5).Trim()
                : "";

            if (expression.Length == 0)
                Console.WriteLine();
            else
                Console.WriteLine(Format(Eval(expression)));

            return;
        }

        // INPUT
        m = Regex.Match(
            line,
            @"^INPUT\s+(?:""([^""]*)""\s*,\s*)?([a-zA-Z_][a-zA-Z0-9_]*)$",
            RegexOptions.IgnoreCase
        );

        if (m.Success)
        {
            string prompt = m.Groups[1].Success
                ? m.Groups[1].Value
                : "? ";

            string variable = m.Groups[2].Value.ToLowerInvariant();

            Console.Write(prompt);

            string input = Console.ReadLine() ?? "";

            if (double.TryParse(
                input,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double number))
            {
                variables[variable] = number;
            }
            else
            {
                variables[variable] = input;
            }

            return;
        }

        // LET variable = expression
        // variable = expression
        m = Regex.Match(
            line,
            @"^(?:LET\s+)?([a-zA-Z_][a-zA-Z0-9_]*)\s*=\s*(.*)$",
            RegexOptions.IgnoreCase
        );

        if (m.Success)
        {
            string name = m.Groups[1].Value.ToLowerInvariant();
            string expression = m.Groups[2].Value;

            variables[name] = Eval(expression);
            return;
        }

        // SLEEP
        m = Regex.Match(
            line,
            @"^SLEEP\s+(.*)$",
            RegexOptions.IgnoreCase
        );

        if (m.Success)
        {
            double ms = Number(Eval(m.Groups[1].Value));

            if (ms < 0 || ms > int.MaxValue)
                throw new Exception("Invalid SLEEP duration");

            Thread.Sleep((int)ms);
            return;
        }

        // END
        if (Regex.IsMatch(line, @"^END$", RegexOptions.IgnoreCase))
        {
            running = false;
            return;
        }

        throw new Exception("Syntax error or unknown command: " + line);
    }

    private void Jump(string label)
    {
        label = label.ToLowerInvariant();

        if (!labels.ContainsKey(label))
            throw new Exception("Undefined label: " + label);

        pc = labels[label];
    }

    private bool Truthy(object value)
    {
        if (value is bool b)
            return b;

        if (value is string s)
            return s.Length > 0;

        return Number(value) != 0;
    }

    private double Number(object value)
    {
        if (value is double d)
            return d;

        if (value is int i)
            return i;

        if (value is bool b)
            return b ? 1 : 0;

        if (double.TryParse(
            Convert.ToString(value, CultureInfo.InvariantCulture),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out double result))
        {
            return result;
        }

        throw new Exception("Numeric value expected");
    }

    private string Format(object value)
    {
        if (value is double d)
            return d.ToString("G", CultureInfo.InvariantCulture);

        if (value is bool b)
            return b ? "TRUE" : "FALSE";

        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
    }

    private object Eval(string expression)
    {
        return new ExpressionParser(expression, variables, random).Parse();
    }
}

// ======================================================
// BASIC EXPRESSION PARSER
// ======================================================

class ExpressionParser
{
    private readonly List<string> tokens = new();
    private readonly Dictionary<string, object> variables;
    private readonly Random random;

    private int position;

    public ExpressionParser(
        string expression,
        Dictionary<string, object> variables,
        Random random)
    {
        this.variables = variables;
        this.random = random;

        Tokenize(expression);
    }

    private void Tokenize(string expression)
    {
        string pattern =
            @"""(?:[^""]|"""")*""|" +
            @"(?:\d+(?:\.\d*)?|\.\d+)" +
            @"(?:[eE][+-]?\d+)?" +
            @"|[a-zA-Z_][a-zA-Z0-9_]*" +
            @"|<=|>=|<>|==" +
            @"|[()+\-*/%,=<>!]";

        MatchCollection matches = Regex.Matches(expression, pattern);

        string remaining = expression;

        foreach (Match match in matches)
        {
            int start = match.Index;

            if (!string.IsNullOrWhiteSpace(remaining.Substring(
                0, start - (expression.Length - remaining.Length))))
            {
                // Token positions are checked below.
            }

            tokens.Add(match.Value);
        }

        string reconstructed = string.Concat(tokens);

        string original = Regex.Replace(expression, @"\s+", "");

        if (!string.Equals(reconstructed, original, StringComparison.Ordinal))
        {
            // Validate using the same token pattern.
            int consumed = 0;

            foreach (Match match in matches)
            {
                string between = expression.Substring(
                    consumed,
                    match.Index - consumed
                );

                if (!string.IsNullOrWhiteSpace(between))
                    throw new Exception("Invalid expression: " + expression);

                consumed = match.Index + match.Length;
            }

            if (!string.IsNullOrWhiteSpace(expression.Substring(consumed)))
                throw new Exception("Invalid expression: " + expression);
        }
    }

    private string Peek()
    {
        return position < tokens.Count ? tokens[position] : "";
    }

    private string Take()
    {
        if (position >= tokens.Count)
            throw new Exception("Unexpected end of expression");

        return tokens[position++];
    }

    private bool Match(string value)
    {
        if (Peek().Equals(value, StringComparison.OrdinalIgnoreCase))
        {
            position++;
            return true;
        }

        return false;
    }

    public object Parse()
    {
        object result = ParseOr();

        if (position != tokens.Count)
            throw new Exception("Unexpected token: " + Peek());

        return result;
    }

    // OR
    private object ParseOr()
    {
        object left = ParseAnd();

        while (Match("OR"))
        {
            object right = ParseAnd();
            left = Truthy(left) || Truthy(right);
        }

        return left;
    }

    // AND
    private object ParseAnd()
    {
        object left = ParseComparison();

        while (Match("AND"))
        {
            object right = ParseComparison();
            left = Truthy(left) && Truthy(right);
        }

        return left;
    }

    // Comparisons
    private object ParseComparison()
    {
        object left = ParseAdditive();

        string op = Peek();

        if (op == "=" || op == "==" || op == "<>" ||
            op == "<" || op == ">" || op == "<=" || op == ">=")
        {
            Take();
            object right = ParseAdditive();

            bool numeric =
                left is double || left is int || left is bool;

            bool result;

            if (numeric)
            {
                double a = Number(left);
                double b = Number(right);

                result = op switch
                {
                    "=" or "==" => a == b,
                    "<>" => a != b,
                    "<" => a < b,
                    ">" => a > b,
                    "<=" => a <= b,
                    ">=" => a >= b,
                    _ => false
                };
            }
            else
            {
                int comparison = string.Compare(
                    Format(left),
                    Format(right),
                    StringComparison.OrdinalIgnoreCase
                );

                result = op switch
                {
                    "=" or "==" => comparison == 0,
                    "<>" => comparison != 0,
                    "<" => comparison < 0,
                    ">" => comparison > 0,
                    "<=" => comparison <= 0,
                    ">=" => comparison >= 0,
                    _ => false
                };
            }

            return result;
        }

        return left;
    }

    // Addition and subtraction
    private object ParseAdditive()
    {
        object left = ParseMultiplicative();

        while (true)
        {
            if (Match("+"))
            {
                object right = ParseMultiplicative();

                if (left is string || right is string)
                    left = Format(left) + Format(right);
                else
                    left = Number(left) + Number(right);
            }
            else if (Match("-"))
            {
                object right = ParseMultiplicative();
                left = Number(left) - Number(right);
            }
            else
            {
                break;
            }
        }

        return left;
    }

    // Multiplication, division and modulo
    private object ParseMultiplicative()
    {
        object left = ParseUnary();

        while (true)
        {
            if (Match("*"))
            {
                object right = ParseUnary();
                left = Number(left) * Number(right);
            }
            else if (Match("/"))
            {
                object right = ParseUnary();
                double divisor = Number(right);

                if (divisor == 0)
                    throw new Exception("Division by zero");

                left = Number(left) / divisor;
            }
            else if (Match("%"))
            {
                object right = ParseUnary();
                left = Number(left) % Number(right);
            }
            else
            {
                break;
            }
        }

        return left;
    }

    // Unary operators
    private object ParseUnary()
    {
        if (Match("-"))
            return -Number(ParseUnary());

        if (Match("+"))
            return Number(ParseUnary());

        if (Match("NOT"))
            return !Truthy(ParseUnary());

        return ParsePrimary();
    }


    // Numbers, strings, variables and functions
    private object ParsePrimary()
    {
        if (Match("("))
        {
            object value = ParseOr();

            if (!Match(")"))
                throw new Exception("Missing closing parenthesis");

            return value;
        }

        string token = Take();

        // String
        if (token.StartsWith("\""))
        {
            return token.Substring(1, token.Length - 2)
                .Replace("\"\"", "\"");
        }

        // Number
        if (double.TryParse(
            token,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out double number))
        {
            return number;
        }

        // Boolean
        if (token.Equals("TRUE", StringComparison.OrdinalIgnoreCase))
            return true;

        if (token.Equals("FALSE", StringComparison.OrdinalIgnoreCase))
            return false;

        // Functions
        if (Match("("))
        {
            object argument = ParseOr();

            if (!Match(")"))
                throw new Exception("Missing closing parenthesis");

            double n = Number(argument);

            if (token.Equals("INT", StringComparison.OrdinalIgnoreCase))
                return Math.Floor(n);

            if (token.Equals("RND", StringComparison.OrdinalIgnoreCase))
            {
                if (n <= 0 || n > int.MaxValue)
                    throw new Exception("RND requires a positive integer range");

                return random.Next((int)n);
            }

            throw new Exception("Unknown function: " + token);
        }

        // Variables
        string name = token.ToLowerInvariant();

        if (variables.TryGetValue(name, out object? variableValue))
            return variableValue;

        throw new Exception("Undefined variable: " + token);
    }

    private double Number(object value)
    {
        if (value is double d)
            return d;

        if (value is int i)
            return i;

        if (value is bool b)
            return b ? 1 : 0;

        if (double.TryParse(
            Convert.ToString(value, CultureInfo.InvariantCulture),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out double result))
            return result;

        throw new Exception("Numeric value expected");
    }

    private bool Truthy(object value)
    {
        if (value is bool b)
            return b;

        if (value is string s)
            return s.Length > 0;

        return Number(value) != 0;
    }

    private string Format(object value)
    {
        if (value is double d)
            return d.ToString("G", CultureInfo.InvariantCulture);

        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
    }
}

// ======================================================
// MAIN CONSOLE APPLICATION
// ======================================================

class Program
{
    static void Main(string[] args)
    {
        Console.BackgroundColor= ConsoleColor.White;
        Console.ForegroundColor= ConsoleColor.Black;
        Console.Clear();
        Console.Title = "Label-BASIC Interpreter";

        Console.WriteLine("Label-BASIC IDE - Console Edition");
        Console.WriteLine("---------------------------------");

        string file;

        if (args.Length > 0)
        {
            file = args[0];
        }
        else
        {
            Console.Write("BASIC source file [program.bas]: ");

            file = Console.ReadLine() ?? "";

            if (string.IsNullOrWhiteSpace(file))
                file = "program.bas";
        }

        if (!File.Exists(file))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("File not found: " + file);
            Console.ResetColor();
            return;
        }

        try
        {
            string source = File.ReadAllText(file);

            BasicInterpreter interpreter = new BasicInterpreter();

            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                interpreter.Stop();
                Console.WriteLine("\n[Execution Interrupted]");
            };

            interpreter.Run(source);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Fatal error: " + ex.Message);
        }
    }
}
