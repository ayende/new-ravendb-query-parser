using Newtonsoft.Json;
using System;
using System.IO;

namespace QueryParser
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            while (true)
            {
                var read = Console.ReadLine();

                var parser = new QueryParser();
                parser.Init(read);

                var query = parser.Parse();
                var output = new StringWriter();
                query.ToJsonAst(new JsonTextWriter(output)
                {
                    Formatting = Formatting.Indented
                });
                var actual = output.GetStringBuilder().ToString();
                Console.WriteLine(actual);

                Console.WriteLine(query.ToString());
            }
        }
    }
}