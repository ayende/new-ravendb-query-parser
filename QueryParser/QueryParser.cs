using System;
using System.Collections.Generic;
using System.Text;

namespace QueryParser
{
    public class QueryParser
    {
        private static readonly string[] OperatorStartMatches = {"<", ">", ">=", "<=", "=", "==", "BETWEEN", "IN", "("};
        private static readonly string[] BinaryOperators = {"OR", "AND"};
        private static readonly string[] TrueFalse = {"true", "false"};
        private static readonly string[] OrderByOptions = {"ASC", "DESC", "ASCENDING", "DESCENDING"};


        private int _depth;
        private NextTokenOptions _state = NextTokenOptions.Parenthesis;

        private int _statePos;

        public QueryScanner Scanner = new QueryScanner();

        public void Init(string q)
        {
            _depth = 0;
            Scanner.Init(q);
        }

        public Query Parse()
        {
            var q = new Query();
            if (Scanner.TryScan("SELECT"))
                q.Select = SelectClause();

            q.From = FromClause();

            if (Scanner.TryScan("WHERE") && Expression(out q.Where) == false)
                ThrowParseException("Unable to parse WHERE clause");

            if (Scanner.TryScan("ORDER BY"))
                q.OrderBy = OrderBy();

            if (Scanner.NextToken())
                ThrowParseException("Expected end of query");

            return q;
        }

        private List<ValueTuple<FieldToken, bool>> OrderBy()
        {
            var orderBy = new List<(FieldToken Field, bool Ascending)>();
            do
            {
                if (Field(out var field) == false)
                    ThrowParseException("Unable to get field for ORDER BY");

                var asc = true;

                if (Scanner.TryScan(OrderByOptions, out var match))
                {
                    if (match == "desc" || match == "descending")
                        asc = false;
                }

                orderBy.Add((field, asc));

                if (Scanner.TryScan(",") == false)
                    break;
            } while (true);
            return orderBy;
        }

        private List<ValueTuple<FieldToken, FieldToken>> SelectClause()
        {
            var select = new List<(FieldToken Field, FieldToken Id)>();

            do
            {
                if (AliasField(out var field, out var alias) == false)
                    ThrowParseException("Unable to get field for SELECT");

                @select.Add((field, alias));

                if (Scanner.TryScan(",") == false)
                    break;
            } while (true);
            return @select;
        }

        private (FieldToken From, QueryExpression Filter, bool Index) FromClause()
        {
            if (Scanner.TryScan("FROM") == false)
                ThrowParseException("Expected FROM clause");

            FieldToken field;
            if (Scanner.TryScan('(')) // FROM ( Collection, filter )
            {
                if (!Scanner.Identifier() && !Scanner.String())
                    ThrowParseException("Expected FROM source");

                field = new FieldToken
                {
                    TokenLength = Scanner.TokenLength,
                    TokenStart = Scanner.TokenStart,
                    EscapeChars = Scanner.EscapeChars
                };
                
                if(Scanner.TryScan(',') == false)
                    ThrowParseException("Expected COMMA in filtered FORM clause after source");

                if(Expression(out var filter) == false)
                    ThrowParseException("Expected filter in filtered FORM clause");
                
                if(Scanner.TryScan(')') == false)
                    ThrowParseException("Expected closing parenthesis in filtered FORM clause after filter");

                return (field, filter, false);
            }

            if (Scanner.TryScan("INDEX"))
            {
                if (!Scanner.Identifier() && !Scanner.String())
                    ThrowParseException("Expected FROM INDEX source");
                
                field = new FieldToken
                {
                    TokenLength = Scanner.TokenLength,
                    TokenStart = Scanner.TokenStart,
                    EscapeChars = Scanner.EscapeChars
                };
                
                return (field, null, false);
            }
            
            if (!Scanner.Identifier() && !Scanner.String())
                ThrowParseException("Expected FROM source");
            
            field = new FieldToken
            {
                TokenLength = Scanner.TokenLength,
                TokenStart = Scanner.TokenStart,
                EscapeChars = Scanner.EscapeChars
            };

            return (field, null, false);
        }

        private bool AliasField(out FieldToken field, out FieldToken alias)
        {
            if (Field(out field) == false)
            {
                alias = null;
                return false;
            }

            if (Scanner.TryScan("AS") == false)
            {
                alias = null;
                return true;
            }

            if (Field(out alias) == false)
                ThrowParseException("Expected field alias after AS in SELECT");

            return true;
        }

        internal bool Parameter(out int tokenStart, out int tokenLength)
        {
            if (Scanner.TryScan(':') == false)
            {
                tokenStart = 0;
                tokenLength = 0;
                return false;
            }

            tokenStart = Scanner.TokenStart;
            tokenLength = 1;

            if (Scanner.Identifier(false) == false)
                ThrowParseException("Expected parameter name");

            tokenLength += Scanner.TokenLength;
            return true;
        }

        internal bool Expression(out QueryExpression op)
        {
            if (++_depth > 128)
                ThrowQueryException("Query is too complex, over 128 nested clauses are not allowed");
            if (Scanner.Position != _statePos)
            {
                _statePos = Scanner.Position;
                _state = NextTokenOptions.Parenthesis;
            }
            var result = Binary(out op);
            _depth--;
            return result;
        }

        private bool Binary(out QueryExpression op)
        {
            switch (_state)
            {
                case NextTokenOptions.Parenthesis:
                    if (Parenthesis(out op) == false)
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


            if (Scanner.TryScan(BinaryOperators, out var found) == false)
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

        private bool Parenthesis(out QueryExpression op)
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

        private bool Operator(out QueryExpression op)
        {
            if (Field(out var field) == false)
            {
                op = null;
                return false;
            }

            if (Scanner.TryScan(OperatorStartMatches, out var match) == false)
                ThrowParseException("Invalid operator expected any of (In, Between, =, <, >, <=, >=)");

            OperatorType type;

            switch (match)
            {
                case "<":
                    type = OperatorType.LessThen;
                    break;
                case ">":
                    type = OperatorType.GreaterThen;
                    break;
                case "<=":
                    type = OperatorType.LessThenEqual;
                    break;
                case ">=":
                    type = OperatorType.GreaterThenEqual;
                    break;
                case "=":
                case "==":
                    type = OperatorType.Equal;
                    break;
                case "BETWEEN":
                    type = OperatorType.Between;
                    break;
                case "IN":
                    type = OperatorType.In;
                    break;
                case "(":
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

                        if (Value(out var argVal))
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
                    if (Value(out var fst) == false)
                        ThrowParseException("parsing Between, expected value (1st)");
                    if (Scanner.TryScan("and") == false)
                        ThrowParseException("parsing Between, expected AND");
                    if (Value(out var snd) == false)
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

                        if (Value(out var inVal) == false)
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
                    if (Value(out var val) == false)
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

        private bool Value(out ValueToken val)
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
            if (Scanner.String())
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
            if (Scanner.TryScan(TrueFalse, out var match))
            {
                val = new ValueToken
                {
                    TokenStart = Scanner.TokenStart,
                    TokenLength = Scanner.TokenLength,
                    Type = match == "true" ? ValueTokenType.True : ValueTokenType.False,
                };
                return true;
            }
            int tokenStart, tokenLength;
            if (Parameter(out tokenStart, out tokenLength))
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

        internal bool Field(out FieldToken token)
        {
            var tokenStart = -1;
            var tokenLength = 0;
            var escapeChars = 0;
            while (true)
            {
                if (Scanner.Identifier() == false)
                    if (Scanner.String())
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

        private enum NextTokenOptions
        {
            Parenthesis,
            BinaryOp
        }

        public class ParseException : Exception
        {
            public ParseException(string msg) : base(msg)
            {
            }
        }
    }
}