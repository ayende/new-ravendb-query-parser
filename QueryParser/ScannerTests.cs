using Xunit;

namespace QueryParser
{
    public class ScannerTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("  \t ")]
        public void EmptyQueriesShouldJustReturnEndOfInput(string q)
        {
            var qs = new QueryScanner();
            qs.Init(q);

            Assert.False(qs.TryIdentifier());
        }

        [Theory]
        [InlineData("hello", 0, 5)]
        [InlineData(" name = ", 1, 4)]
        [InlineData("some_thing ", 0, 10)]
        public void IdentifierShouldBeFound(string q, int start, int len)
        {
            var qs = new QueryScanner();
            qs.Init(q);

            Assert.True(qs.TryIdentifier());
            Assert.Equal(start, qs.TokenStart);
            Assert.Equal(len, qs.TokenLength);
        }


        [Theory]
        [InlineData(" 'hel lo' ", 0)]
        [InlineData(" \"he \" ", 0)]
        [InlineData(" 'we''ll' ", 1)]
        public void ParseStringLiterals(string q, int escape)
        {
            var qs = new QueryScanner();
            qs.Init(q);

            Assert.True(qs.TryString());
            Assert.Equal(escape, qs.EscapeChars);
        }

        [Theory]
        [InlineData("1", 1L)]
        [InlineData("1.0", 1.0D)]
        [InlineData("1234 ", 1234L)]
        [InlineData(" -1234 ", -1234L)]
        [InlineData(" 1.3", 1.3D)]
        [InlineData(" -1.32", -1.32D)]
        public void ParseNumbers(string q, object expected)
        {
            var qs = new QueryScanner();
            qs.Init(q);

            var result = qs.TryNumber();
            Assert.NotNull(result);
            if (result == NumberToken.Double)
                Assert.Equal((double) expected, double.Parse(q.Substring(qs.TokenStart, qs.TokenLength)));
            else
                Assert.Equal((long) expected, long.Parse(q.Substring(qs.TokenStart, qs.TokenLength)));
        }

        [Theory]
        [InlineData("hello there", 6, 5)]
        public void CanScanConsecutiveIdentifiers(string q, int start, int len)
        {
            var qs = new QueryScanner();
            qs.Init(q);

            Assert.True(qs.TryIdentifier());
            Assert.True(qs.TryIdentifier());
            Assert.Equal(start, qs.TokenStart);
            Assert.Equal(len, qs.TokenLength);
        }
    }
}