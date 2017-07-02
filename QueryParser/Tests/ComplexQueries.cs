using System;
using System.IO;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using Xunit;

namespace QueryParser
{
    public class ComplexQueries
    {
        [Theory]
        [InlineData("FROM Users", "{\"From\":{\"Index\":false,\"Source\":\"Users\"}}")]
        [InlineData("FROM (Users, IsActive = true)", "{\"From\":{\"Index\":false,\"Source\":\"Users\",\"Filter\":{\"Type\":\"Equal\",\"Field\":\"IsActive\",\"Value\":\"IsActive\"}}}")]
        [InlineData(@"SELECT Age FROM (Users, IsActive = true)","{\"Select\":[{\"Expression\":\"Age\"}],\"From\":{\"Index\":false,\"Source\":\"Users\",\"Filter\":{\"Type\":\"Equal\",\"Field\":\"IsActive\",\"Value\":\"IsActive\"}}}")]
        [InlineData(@"FROM (Users, IsActive = true)
WHERE Age BETWEEN 21 AND 30", "{\"From\":{\"Index\":false,\"Source\":\"Users\",\"Filter\":{\"Type\":\"Equal\",\"Field\":\"IsActive\",\"Value\":\"IsActive\"}},\"Where\":{\"Type\":\"Between\",\"Field\":\"Age\",\"Min\":21,\"Max\":30}}")]
        [InlineData(@"FROM (Users, IsActive = true)
WHERE Age BETWEEN 21 AND 30
ORDER BY Age DESC, Name ASC", "{\"From\":{\"Index\":false,\"Source\":\"Users\",\"Filter\":{\"Type\":\"Equal\",\"Field\":\"IsActive\",\"Value\":\"IsActive\"}},\"Where\":{\"Type\":\"Between\",\"Field\":\"Age\",\"Min\":21,\"Max\":30},\"OrderBy\":[{\"Field\":\"Age\",\"Ascending\":false},{\"Field\":\"Name\",\"Ascending\":true}]}")]
        [InlineData(@"
SELECT sum(Age), Name as Username
FROM Users
WHERE boost(Age > 15, 2) 
ORDER BY LastName
", "{\"Select\":[{\"Expression\":{\"Type\":\"Method\",\"Method\":\"sum\",\"Arguments\":[{\"Field\":\"Age\"}]}},{\"Expression\":\"Name\",\"Alias\":\"Username\"}],\"From\":{\"Index\":false,\"Source\":\"Users\"},\"Where\":{\"Type\":\"Method\",\"Method\":\"boost\",\"Arguments\":[{\"Type\":\"GreaterThen\",\"Field\":\"Age\",\"Value\":\"15\"},2]},\"OrderBy\":[{\"Field\":\"LastName\",\"Ascending\":true}]}")]
        
        public void CanParseFullQueries(string q, string json)
        {
            var parser = new QueryParser();
            parser.Init(q);

            var query = parser.Parse();
            var output = new StringWriter();
            query.ToJsonAst(new JsonTextWriter(output));
            var actual = output.GetStringBuilder().ToString();
            Assert.Equal(json, actual);
        }
    }
}