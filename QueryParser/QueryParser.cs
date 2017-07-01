using System;
using System.Collections.Generic;
using System.Text;

namespace QueryParser
{
    public class QueryParser
    {
        private static readonly char[] _operatorStartMatches = {'<', '>', '=', 'b', 'B', 'i', 'I', '('};
        private static readonly string[] _binaryOperators = {"or", "and"};

        private int _depth;
        
        private int _statePos;
        private NextTokenOptions _state = NextTokenOptions.Parenthesis;

        private enum NextTokenOptions
        {
            Parenthesis,
            BinaryOp
        }
        
        public QueryScanner Scanner = new QueryScanner();

        public void Init(string q)
        {
            _depth = 0;
            Scanner.Init(q);
        }

        public bool ParseParameter(out int tokenStart, out int tokenLength)
        {
            if (Scanner.TryScan('?') == false)
            {
                tokenStart = 0;
                tokenLength = 0;
                return false;
            }

            tokenStart = Scanner.TokenStart;
            tokenLength = 1;

            if (Scanner.TryIdentifier(false))
                tokenLength += Scanner.TokenLength;
            return true;
        }

        public bool Expression(out QueryExpression op)
        {
            if (++_depth > 128)
            {
                ThrowQueryException("Query is too complex, over 128 nested clauses are not allowed");
            }
            if (Scanner.Position != _statePos)
            {
                _statePos = Scanner.Position;
                _state = NextTokenOptions.Parenthesis;
            }
            var result = Binary(out op);
            _depth--;
            return result;
        }

        public bool Binary(out QueryExpression op)
        {
            switch (_state)
            {
                case NextTokenOptions.Parenthesis:
                    if(Parenthesis(out op) == false)
                        return false;
                    break;
                case NextTokenOptions.BinaryOp:
                    _state = NextTokenOptions.Parenthesis;
                    if (Operator(out op) == false)
                        return false;
                    break;
                default:
                    op = null;
                    return false;
            }
                

            if (Scanner.TryScan(_binaryOperators, out var found) == false)
                return true; // found simple

            var negate = Scanner.TryScan("not");
            var type = found == "or"
                ? (negate ? OperatorType.OrNot : OperatorType.Or)
                : (negate ? OperatorType.AndNot : OperatorType.And);

            _state = NextTokenOptions.Parenthesis;

            if (Parenthesis(out var op2) == false)
                ThrowParseException($"Failed to find second part of {type} expression");

            op = new QueryExpression
            {
                Type = type,
                Left = op,
                Right = op2
            };
            return true;
        }

        public bool Parenthesis(out QueryExpression op)
        {
            if (Scanner.TryScan('(') == false)
            {
                _state = NextTokenOptions.BinaryOp;
                return Binary(out op);
            }
            
            if (Expression(out op) == false)
                return false;

            if (Scanner.TryScan(')') == false)
                ThrowParseException("Unmatched parenthesis, expected ')'");
            return true;
        }

        public bool Operator(out QueryExpression op)
        {
            if (ParseField(out var field) == false)
            {
                op = null;
                return false;
            }

            if (Scanner.TryScan(_operatorStartMatches, out var match) == false)
                ThrowParseException("Invalid operator expected any of (In, Between, =, <, >, <=, >=)");

            OperatorType type;

            switch (match)
            {
                case '<':
                    type = Scanner.TryScan('=', false)
                        ? OperatorType.LessThenEqual
                        : OperatorType.LessThen;
                    break;
                case '>':
                    type = Scanner.TryScan('=', false)
                        ? OperatorType.GreaterThenEqual
                        : OperatorType.GreaterThen;
                    break;
                case '=':
                    type = OperatorType.Equal;
                    break;
                case 'B':
                case 'b':
                    if (Scanner.TryScan("etween", false) == false)
                    {
                        Scanner.Back(1);
                        ThrowParseException("Invalid operator expected any of (In, Between, =, <, >, <=, >=)");
                    }
                    type = OperatorType.Between;
                    break;
                case 'i':
                case 'I':
                    if (Scanner.TryScan("n") == false)
                    {
                        Scanner.Back(1);
                        ThrowParseException("Invalid operator expected any of (In, Between, =, <, >, <=, >=)");
                    }
                    type = OperatorType.In;
                    break;
                case '(':
                    type = OperatorType.Method;
                    break;
                default:
                    op = null;
                    return false;
            }

            switch (type)
            {
                case OperatorType.Method:
                    var args = new List<object>();
                    do
                    {
                        if (Scanner.TryScan(')'))
                            break;

                        if (args.Count != 0)
                            if (Scanner.TryScan(',') == false)
                                ThrowParseException("parsing method expression, expected ','");

                        if (ParseValue(out var argVal))
                            args.Add(argVal);
                        else if (Expression(out var expr))
                            args.Add(expr);
                        else
                            ThrowParseException("parsing method, expected an argument");
                    } while (true);

                    op = new QueryExpression
                    {
                        Field = field,
                        Type = OperatorType.Method,
                        Arguments = args
                    };
                    return true;
                case OperatorType.Between:
                    if (ParseValue(out var fst) == false)
                        ThrowParseException("parsing Between, expected value (1st)");
                    if (Scanner.TryScan("and") == false)
                        ThrowParseException("parsing Between, expected AND");
                    if (ParseValue(out var snd) == false)
                        ThrowParseException("parsing Between, expected value (2nd)");

                    if (fst.Type != snd.Type)
                        ThrowQueryException(
                            $"Invalid Between expression, values must have the same type but got {fst.Type} and {snd.Type}");

                    op = new QueryExpression
                    {
                        Field = field,
                        Type = OperatorType.Between,
                        First = fst,
                        Second = snd
                    };
                    return true;
                case OperatorType.In:
                    if (Scanner.TryScan('(') == false)
                        ThrowParseException("parsing In, expected '('");

                    var list = new List<ValueToken>();
                    do
                    {
                        if (Scanner.TryScan(')'))
                            break;

                        if (list.Count != 0)
                            if (Scanner.TryScan(',') == false)
                                ThrowParseException("parsing In expression, expected ','");

                        if (ParseValue(out var inVal) == false)
                            ThrowParseException("parsing In, expected a value");

                        if (list.Count > 0)
                            if (list[0].Type != inVal.Type)
                                ThrowQueryException(
                                    $"Invalid In expression, all values must have the same type, expected {list[0].Type} but got {inVal.Type}");
                        list.Add(inVal);
                    } while (true);

                    op = new QueryExpression
                    {
                        Field = field,
                        Type = OperatorType.In,
                        Values = list
                    };

                    return true;
                default:
                    if (ParseValue(out var val) == false)
                        ThrowParseException($"parsing {type} expression, expected a value");

                    op = new QueryExpression
                    {
                        Field = field,
                        Type = type,
                        Value = val
                    };
                    return true;
            }
        }

        private void ThrowParseException(string msg)
        {
            var sb = new StringBuilder()
                .Append(Scanner.Column)
                .Append(":")
                .Append(Scanner.Line)
                .Append(" ")
                .Append(msg)
                .Append(" but got");

            if (Scanner.NextToken())
                sb.Append(": ")
                    .Append(Scanner.CurrentToken);
            else
                sb.Append(" to the end of the query");


            throw new ParseException(sb.ToString());
        }

        private void ThrowQueryException(string msg)
        {
            var sb = new StringBuilder()
                .Append(Scanner.Column)
                .Append(":")
                .Append(Scanner.Line)
                .Append(" ")
                .Append(msg);

            throw new ParseException(sb.ToString());
        }

        public bool ParseValue(out ValueToken val)
        {
            var numberToken = Scanner.TryNumber();
            if (numberToken != null)
            {

                val = new ValueToken
                {
                    TokenStart = Scanner.TokenStart,
                    TokenLength = Scanner.TokenLength,
                    Type = numberToken.Value == NumberToken.Long ? ValueTokenType.Long : ValueTokenType.Double
                };
                return true;
            }
            if (Scanner.TryString())
            {
                val = new ValueToken
                {
                    TokenStart = Scanner.TokenStart,
                    TokenLength = Scanner.TokenLength,
                    Type = ValueTokenType.String,
                    EscapeChars = Scanner.EscapeChars
                };
                return true;
            }
            int tokenStart, tokenLength;
            if (ParseParameter(out tokenStart, out tokenLength))
            {
                val = new ValueToken
                {
                    TokenStart = tokenStart,
                    TokenLength = tokenLength,
                    Type = ValueTokenType.Parameter
                };
                return true;
            }
            val = null;
            return false;
        }

        public bool ParseField(out FieldToken token)
        {
            var tokenStart = -1;
            var tokenLength = 0;
            var escapeChars = 0;
            while (true)
            {
                if (Scanner.TryIdentifier() == false)
                    if (Scanner.TryString())
                    {
                        escapeChars += Scanner.EscapeChars;
                    }
                    else
                    {
                        token = null;
                        return false;
                    }
                if (tokenStart == -1)
                    tokenStart = Scanner.TokenStart;
                tokenLength += Scanner.TokenLength;

                if (Scanner.TryScan("[]"))
                    tokenLength += 2;

                if (Scanner.TryScan('.') == false)
                    break;

                tokenLength += 1;
            }

            token = new FieldToken
            {
                EscapeChars = escapeChars,
                TokenLength = tokenLength,
                TokenStart = tokenStart
            };
            return true;
        }

        public class ParseException : Exception
        {
            public ParseException(string msg) : base(msg)
            {
            }
        }
    }
}