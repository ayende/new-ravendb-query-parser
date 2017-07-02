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
FROM Users WHERE search(Name, 'oren')
", json: "");
            }
            catch (Exception e) {
                Console.WriteLine(e);
            }
        }
    }
}