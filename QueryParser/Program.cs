using System;

namespace QueryParser
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            try
            {
                var t = new ComplexQueries();
                t.CanParseFullQueries(q: @"
FROM (Users, IsActive = true)
WHERE Age BETWEEN 21 AND 30
ORDER BY Age DESC, Name ASC
", json: "");
            }
            catch (Exception e) {
                Console.WriteLine(e);
            }
        }
    }
}