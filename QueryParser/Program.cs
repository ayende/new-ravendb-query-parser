using System;

namespace QueryParser
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var t = new ComplexQueries();
            t.CanParseFullQueries(q: @"
SELECT Age, Name as Username
WHERE boost(Age > 15, 2) --AND State IN ('Active', 'Admin') AND LoggedIN BETWEEN 12 AND 48
ORDER BY LastName
", json: "");
        }
    }
}