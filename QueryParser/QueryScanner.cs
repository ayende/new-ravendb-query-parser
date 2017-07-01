using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using Newtonsoft.Json;

namespace QueryParser
{
    public class QueryExpression
    {
        public FieldToken Field;
        public ValueToken First, Second; // between

        public QueryExpression Left;
        public QueryExpression Right;
        public OperatorType Type;

        public ValueToken Value; // =, <, >, <=, >=
        public List<ValueToken> Values; // in 
        public List<object> Arguments; // method

        [ThreadStatic] private static StringBuilder _tempBuffer;

        private static string Extract(string q, int tokenStart, int tokenLength, int escapeChars)
        {
            if (escapeChars == 0)
            {
                return q.Substring(tokenStart, tokenLength);
            }
            var tmp = _tempBuffer ?? (_tempBuffer = new StringBuilder());
            tmp.Capacity = Math.Max(tmp.Capacity, tokenLength);
            var qouteChar = q[tokenStart];
            for (int i = tokenStart; i < tokenLength; i++)
            {
                if (q[i] != qouteChar)
                    tmp.Append(q[i]);
            }
            return tmp.ToString();
        }

        private static void WriteValue(string q, JsonWriter writer, int tokenStart, int tokenLength, int escapeChars,
            bool raw = false)
        {
            if (escapeChars == 0)
            {
                if (tokenLength != 0)
                {
                    if (q[tokenStart] == '"' || q[tokenStart] == '\'')
                    {
                        // skip quotes
                        writer.WriteValue(q.Substring(tokenStart + 1, tokenLength - 1));
                        return;
                    }
                }
                if (raw)
                    writer.WriteRawValue(q.Substring(tokenStart, tokenLength));
                else
                    writer.WriteValue(q.Substring(tokenStart, tokenLength));
                return;
            }
            var tmp = _tempBuffer ?? (_tempBuffer = new StringBuilder());
            tmp.Capacity = Math.Max(tmp.Capacity, tokenLength);
            var qouteChar = q[tokenStart];
            for (int i = tokenStart + 1; i < tokenLength - 1; i++)
            {
                if (q[i] != qouteChar)
                    tmp.Append(q[i]);
            }
            writer.WriteValue(tmp.ToString());
        }

        public void ToString(string query, TextWriter writer)
        {
            switch (Type)
            {
                case OperatorType.Equal:
                case OperatorType.LessThen:
                case OperatorType.GreaterThen:
                case OperatorType.LessThenEqual:
                case OperatorType.GreaterThenEqual:
                    writer.Write(Extract(query, Field.TokenStart, Field.TokenLength, Field.EscapeChars));
                    switch (Type)
                    {
                        case OperatorType.Equal:
                            writer.Write(" = ");
                            break;
                        case OperatorType.LessThen:
                            writer.Write(" < ");
                            break;
                        case OperatorType.GreaterThen:
                            writer.Write(" > ");
                            break;
                        case OperatorType.LessThenEqual:
                            writer.Write(" <= ");
                            break;
                        case OperatorType.GreaterThenEqual:
                            writer.Write(" >= ");
                            break;
                        default:
                            ThrowInvalidType(Type);
                            break;
                    }
                    writer.Write(Extract(query, Value.TokenStart, Value.TokenLength, Value.EscapeChars));
                    break;
                case OperatorType.Between:
                    writer.Write(Extract(query, Field.TokenStart, Field.TokenLength, Field.EscapeChars));
                    writer.Write(" BETWEEN ");
                    writer.Write(Extract(query, First.TokenStart, First.TokenLength, First.EscapeChars));
                    writer.Write(" AND ");
                    writer.Write(Extract(query, Second.TokenStart, Second.TokenLength, Second.EscapeChars));
                    break;
                case OperatorType.In:
                    writer.Write(Extract(query, Field.TokenStart, Field.TokenLength, Field.EscapeChars));
                    writer.Write(" IN (");
                    for (var i = 0; i < Values.Count; i++)
                    {
                        var value = Values[i];
                        if (i != 0)
                            writer.Write(", ");
                        writer.Write(Extract(query, value.TokenStart, value.TokenLength, value.EscapeChars));
                    }
                    writer.Write(")");
                    break;
                case OperatorType.And:
                case OperatorType.AndNot:
                case OperatorType.Or:
                case OperatorType.OrNot:
                    writer.Write("(");
                    Left.ToString(query, writer);
                    switch (Type)
                    {
                        case OperatorType.And:
                            writer.Write(" AND ");
                            break;
                        case OperatorType.AndNot:
                            writer.Write(" AND NOT ");
                            break;
                        case OperatorType.Or:
                            writer.Write(" OR ");
                            break;
                        case OperatorType.OrNot:
                            writer.Write(" OR NOT ");
                            break;
                    }
                    Right.ToString(query, writer);
                    writer.Write(")");
                    break;
                case OperatorType.Method:
                    writer.Write(Extract(query, Field.TokenStart, Field.TokenLength, Field.EscapeChars));
                    writer.Write("(");

                    for (int i = 0; i < Arguments.Count; i++)
                    {
                        var arg = Arguments[i];
                        if (i != 0)
                            writer.Write(", ");
                        if (arg is QueryExpression qe)
                        {
                            qe.ToString(query, writer);
                        }
                        else
                        {
                            var val = (ValueToken) arg;
                            writer.Write(Extract(query, val.TokenStart, val.TokenLength, val.EscapeChars));
                        }
                    }
                    writer.Write(")");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public void ToJsonAst(string query, JsonWriter writer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("Type");
            writer.WriteValue(Type.ToString());
            switch (Type)
            {
                case OperatorType.Equal:
                case OperatorType.LessThen:
                case OperatorType.GreaterThen:
                case OperatorType.LessThenEqual:
                case OperatorType.GreaterThenEqual:
                    writer.WritePropertyName("Field");
                    WriteValue(query, writer, Field.TokenStart, Field.TokenLength, Field.EscapeChars);
                    writer.WritePropertyName("Value");
                    WriteValue(query, writer, Value.TokenStart, Value.TokenLength, Value.EscapeChars,
                        Value.Type == ValueTokenType.String);
                    break;
                case OperatorType.Between:
                    writer.WritePropertyName("Field");
                    WriteValue(query, writer, Field.TokenStart, Field.TokenLength, Field.EscapeChars);
                    writer.WritePropertyName("Min");
                    WriteValue(query, writer, First.TokenStart, First.TokenLength, First.EscapeChars, 
                        First.Type == ValueTokenType.Double||First.Type==ValueTokenType.Long);
                    writer.WritePropertyName("Max");
                    WriteValue(query, writer, Second.TokenStart, Second.TokenLength, Second.EscapeChars, 
                        Second.Type == ValueTokenType.Double||Second.Type==ValueTokenType.Long);
                    break;
                case OperatorType.In:
                    writer.WritePropertyName("Field");
                    WriteValue(query, writer, Field.TokenStart, Field.TokenLength, Field.EscapeChars);
                    writer.WritePropertyName("Values");
                    writer.WriteStartArray();
                    foreach (var value in Values)
                    {
                        WriteValue(query, writer, value.TokenStart, value.TokenLength, value.EscapeChars, 
                            value.Type == ValueTokenType.Double||value.Type==ValueTokenType.Long);
                    }
                    writer.WriteEndArray();
                    break;
                case OperatorType.And:
                case OperatorType.AndNot:
                case OperatorType.Or:
                case OperatorType.OrNot:
                    writer.WritePropertyName("Left");
                    Left.ToJsonAst(query, writer);
                    writer.WritePropertyName("Right");
                    Right.ToJsonAst(query, writer);
                    break;
                case OperatorType.Method:
                    writer.WritePropertyName("Method");
                    WriteValue(query, writer, Field.TokenStart, Field.TokenLength, Field.EscapeChars);
                    writer.WritePropertyName("Arguments");
                    writer.WriteStartArray();
                    foreach (var arg in Arguments)
                    {
                        if (arg is QueryExpression qe)
                        {
                            qe.ToJsonAst(query, writer);
                        }
                        else
                        {
                            var val = (ValueToken) arg;
                            WriteValue(query, writer, val.TokenStart, val.TokenLength, val.EscapeChars, 
                                val.Type == ValueTokenType.Double||val.Type==ValueTokenType.Long);
                        }
                    }
                    writer.WriteEndArray();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            writer.WriteEndObject();
        }

        private static void ThrowInvalidType(OperatorType type)
        {
            throw new ArgumentOutOfRangeException(type.ToString());
        }
    }

    public enum OperatorType
    {
        Equal,
        LessThen,
        GreaterThen,
        LessThenEqual,
        GreaterThenEqual,
        Between,
        In,
        Method,
        And,
        AndNot,
        Or,
        OrNot
    }

    public class ValueToken
    {
        public int TokenLength;
        public int TokenStart;
        public int EscapeChars;
        public ValueTokenType Type;
    }

    public enum ValueTokenType
    {
        Parameter,
        Long,
        Double,
        String
    }

    public class FieldToken
    {
        public int EscapeChars;
        public int TokenLength;
        public int TokenStart;
    }

    public class ParameterToken
    {
        public int TokenLength;
        public int TokenStart;
    }

    public class QueryScanner
    {
        private int _pos;
        private string _q;
        public int Column, Line;
        public int EscapeChars;
        public int TokenStart, TokenLength;


        public int Position => _pos;
        public string CurrentToken => _q.Substring(TokenStart, TokenLength);

        public void Init(string q)
        {
            _q = q ?? string.Empty;
            _pos = 0;
            Column = 1;
            Line = 1;
            TokenStart = 0;
            TokenLength = 0;
        }

        public bool NextToken()
        {
            if (SkipWhitespace() == false)
                return false;
            TokenStart = _pos;

            for (; _pos < _q.Length; _pos++)
                if (char.IsWhiteSpace(_q[_pos]))
                    break;
            TokenLength = _pos - TokenStart;
            return true;
        }

        public NumberToken? TryNumber()
        {
            if (SkipWhitespace() == false)
                return null;

            if (char.IsDigit(_q[_pos]) == false && _q[_pos] != '-')
                return null;

            var result = NumberToken.Long;
            TokenStart = _pos;
            var i = _pos + 1;
            for (; i < _q.Length; i++)
            {
                var c = _q[i];
                if (c >= '0' && c <= '9')
                    continue;

                if (c != '.')
                    break;

                if (result == NumberToken.Double)
                    return null; // we already saw this, so can't allow double periods
                result = NumberToken.Double;
            }

            Column += i - _pos;
            _pos = i;

            TokenLength = _pos - TokenStart;
            return result;
        }

        public bool TryIdentifier(bool skipWhitspace = true)
        {
            if (SkipWhitespace(skipWhitspace) == false)
                return false;
            if (char.IsLetter(_q[_pos]) == false)
                return false;
            TokenStart = _pos;
            _pos++;
            for (; _pos < _q.Length; _pos++)
                if (char.IsLower(_q[_pos]) == false &&
                    _q[_pos] != '_')
                    break;
            TokenLength = _pos - TokenStart;
            Column += TokenLength;
            return true;
        }

        private bool SkipWhitespace(bool skipWhitespace = true)
        {
            if (skipWhitespace == false)
                return _pos < _q.Length;

            for (; _pos < _q.Length; _pos++, Column++)
                switch (_q[_pos])
                {
                    case ' ':
                    case '\t':
                    case '\r':
                        continue;
                    case '\n':
                        Line++;
                        Column = 1;
                        break;
                    case '-': // -- comment to end of line / input
                    {
                        if (_pos + 1 < _q.Length || _q[_pos + 1] != '-')
                            return true;
                        _pos += 2;
                        for (; _pos < _q.Length; _pos++)
                            if (_q[_pos] == '\n')
                                goto case '\n';
                        return false; // end of input
                    }
                    default:
                        return true;
                }
            return false;
        }

        public bool TryScan(char[] possibleMatches, out char match)
        {
            if (SkipWhitespace() == false)
            {
                match = '\0';
                return false;
            }

            var ch = _q[_pos];
            for (var i = 0; i < possibleMatches.Length; i++)
                if (ch == possibleMatches[i])
                {
                    _pos++;
                    Column++;
                    match = ch;
                    return true;
                }

            match = '\0';
            return false;
        }

        public bool TryScan(char match, bool skipWhitespace = true)
        {
            if (SkipWhitespace(skipWhitespace) == false)
                return false;


            if (_q[_pos] != match)
                return false;
            _pos++;
            Column++;
            return true;
        }

        public bool TryScan(string match, bool skipWhitespace = true)
        {
            if (SkipWhitespace(skipWhitespace) == false)
                return false;

            if (match.Length + _pos >= _q.Length)
                return false;

            if (string.Compare(_q, _pos, match, 0, match.Length, StringComparison.OrdinalIgnoreCase) != 0)
                return false;

            _pos += match.Length;
            Column += match.Length;
            return true;
        }

        public bool TryScan(string[] matches, out string found)
        {
            if (SkipWhitespace() == false)
            {
                found = null;
                return false;
            }

            foreach (var match in matches)
            {
                if (match.Length + _pos >= _q.Length)
                    continue;

                if (string.Compare(_q, _pos, match, 0, match.Length, StringComparison.OrdinalIgnoreCase) != 0)
                    continue;

                _pos += match.Length;
                Column += match.Length;
                found = match;
                return true;
            }

            found = null;
            return false;
        }

        public bool TryString()
        {
            EscapeChars = 0;
            if (SkipWhitespace() == false)
                return false;

            var quoteChar = _q[_pos];

            if (quoteChar != '"' && quoteChar != '\'')
                return false;
            TokenStart = _pos;
            var i = _pos + 1;
            for (; i < _q.Length; i++)
            {
                if (_q[i] != quoteChar)
                    continue;

                if (i + 1 < _q.Length && _q[i + 1] == quoteChar)
                {
                    i++; // escape char
                    EscapeChars++;
                    continue;
                }
                Column += i + 1 - _pos;

                _pos = i + 1;
                TokenLength = _pos - TokenStart;
                return true;
            }

            return false;
        }

        public void Back(int i)
        {
            Column -= i;
            _pos -= i;
        }
    }
}