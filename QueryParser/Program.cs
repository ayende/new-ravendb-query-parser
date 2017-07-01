using System;

namespace QueryParser
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var t = new ParserTests();
            t.ParseAndWrite(q: "Name ='Oren'", o: "boost()");
        }
    }
}