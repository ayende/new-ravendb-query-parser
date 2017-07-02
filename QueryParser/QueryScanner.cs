using System;
using System.Net;

namespace QueryParser
{
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

        public bool Identifier(bool skipWhitspace = true)
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

        public bool String()
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

    }
}