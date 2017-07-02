using System;
using System.IO;
using Newtonsoft.Json;
using Xunit;

namespace QueryParser
{
    public class ComplexQueries
    {
        [Theory]
        [InlineData("Name = 'Oren'", "{\"Type\":\"Equal\",\"Field\":\"Name\",\"Value\":\"Oren'\"}")]
        public void CanParseFullQueries(string q, string json)
        {
            var parser = new QueryParser();
            parser.Init(q);

            var query = parser.Parse();
            var output = new StringWriter();
            query.ToJsonAst(new JsonTextWriter(output));
            var actual = output.GetStringBuilder().ToString();
            Console.WriteLine(actual.Replace("\"","\\\""));
            Assert.Equal(json, actual);
        }
    }
}