using PT.FlowAnalysis.Core;
using System;
using System.IO;

namespace PT.FlowAnalysis
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.Error.WriteLine("usage: <path.cs>");
                return 1;
            }

            string source;
            try
            {
                source = File.ReadAllText(args[0]);
            }
            catch
            {
                Console.Error.WriteLine("wrong source path!");
                return 1;
            }

            SolverResult result;
            try
            {
                result = ProblemSolver.Run(source);
            }
            catch
            {
                Console.Error.WriteLine("wrong source content");
                return 1;
            }

            if (!result.Success)
            {
                if (result.CompilationFailures != null)
                {
                    foreach (var diagnostic in result.CompilationFailures)
                    {
                        Console.Error.WriteLine(diagnostic);
                    }
                }
                else
                {
                    Console.Error.WriteLine("analysis fails");
                }

                return 1;
            }

            Console.WriteLine("[" + string.Join(", ", result.ReturnValues) + "].");

            return 0;
        }
    }
}
