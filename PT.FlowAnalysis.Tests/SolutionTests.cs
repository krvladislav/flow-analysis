using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PT.FlowAnalysis.Core;
using System.Linq;

namespace PT.FlowAnalysis.Tests
{
    [TestClass]
    public class SolutionTests
    {
        [TestMethod]
        public void Test_Sample1()
        {
            var code = @"
﻿using System;

namespace ConsoleApp
{
    class Program
    {
        static void Main()
        {
            // выведет 1
            Console.WriteLine( Evaluate(false, false, false, false) );

            // выведет 6
            Console.WriteLine( Evaluate(false, false, false, true) );

            //...
        }

        static int Evaluate(params bool[] parameters)
        {
            int x;
            x = 1;
            if (parameters[0])
            {
                x = 2;
                if (parameters[1])
                {
                    x = 3;
                }
                x = 4;
                if (parameters[2])
                {
                    x = 5;
                }
            }
            if (parameters[3])
            {
                x = 6;
            }

            return x;
        }
    }
}

";

            var result = ProblemSolver.Run(code);

            CollectionAssert.AreEquivalent(new[] { 1, 4, 5, 6 }, result.ReturnValues.ToArray());
        }

        [TestMethod]
        public void Test_Sample2()
        {
            var code = @"
﻿﻿using System;

namespace ConsoleApp
{
    class Program
    {
        static void Main()
        {
            // выведет 1
            Console.WriteLine( Evaluate(false, false, false, false) );

            // выведет 6
            Console.WriteLine( Evaluate(false, false, false, true) );

            //...
        }

        static int Evaluate(params bool[] parameters)
        {
            int x;
            x = 1;
            if (parameters[0])
            {
                x = 2;
                if (parameters[1])
                {
                    x = 3;
                }
                x = 4;
                if (parameters[2])
                {
                    if (parameters[3])
                    {
                        x = 6;
                    }
                    if (parameters[4])
                    {
                        x = 5;
                    }
                }
            }

            return x;
        }
    }
}
";

            var result = ProblemSolver.Run(code);

            CollectionAssert.AreEquivalent(new[] { 1, 4, 5, 6 }, result.ReturnValues.ToArray());
        }


        [TestMethod]
        public void Test_AvoiDeadConditionSameIf_StaticAnalysis()
        {
            var code = @"
﻿﻿using System;

namespace ConsoleApp
{
    class Program
    {
        static void Main() {}
        
        static int Evaluate(params bool[] parameters)
        {
            int x;
            x = 1;
            if (parameters[0] && !parameters[0])
            {
                x = 2;
                if (parameters[1])
                {
                    x = 3;
                }
            }
            return x;
        }
    }
}
";

            var result = ProblemSolver.Run(code, AnalysisType.Static);

            Assert.AreEqual(AnalysisType.Static, result.UsedAnalysisType);
            CollectionAssert.AreEquivalent(new[] { 1 }, result.ReturnValues.ToArray());
        }


        [TestMethod]
        public void Test_AvoiDeadEqualsComparison_StaticAnalysis()
        {
            var code = @"
﻿﻿using System;

namespace ConsoleApp
{
    class Program
    {
        static void Main() {}
        
        static int Evaluate(params bool[] parameters)
        {
            int x;
            x = 1;
            if (parameters[0])
            {
                x = 2;
                if (parameters[0] == true && parameters[0] == false)
                {
                    x = 3;
                }
            }
            return x;
        }
    }
}
";

            var result = ProblemSolver.Run(code, AnalysisType.Static);

            Assert.AreEqual(AnalysisType.Static, result.UsedAnalysisType);
            CollectionAssert.AreEquivalent(new[] { 1, 2 }, result.ReturnValues.ToArray());
        }

        [TestMethod]
        public void Test_AvoiDeadConditionNestedIf_StaticAnalysis()
        {
            var code = @"
﻿﻿using System;

namespace ConsoleApp
{
    class Program
    {
        static void Main() {}
        
        static int Evaluate(params bool[] parameters)
        {
            int x;
            x = 1;
            if (parameters[0])
            {
                x = 2;
                if (parameters[1])
                {
                    x = 3;
                    if (!parameters[0])
                    {
                        x = 4;
                    }
                }
            }
            return x;
        }
    }
}
";

            var result = ProblemSolver.Run(code, AnalysisType.Static);

            Assert.AreEqual(AnalysisType.Static, result.UsedAnalysisType);
            CollectionAssert.AreEquivalent(new[] { 1, 2, 3 }, result.ReturnValues.ToArray());
        }

        [TestMethod]
        public void Test_ExpressionWithArithmetic()
        {
            var code = @"
﻿﻿using System;

namespace ConsoleApp
{
    class Program
    {
        static void Main() {}
        
        static int Evaluate(params bool[] parameters)
        {
            int x;
            x = 1;
            if (parameters[30])
            {
                x = 2;
                if (x + 1 - 1 == 2)
                {
                    x = 3;
                    if (parameters[31])
                    {
                        x = 4;
                    }
                }
            }
            return x;
        }
    }
}
";

            var result = ProblemSolver.Run(code, dryRun: true);

            Assert.AreEqual(AnalysisType.Dynamic, result.UsedAnalysisType);
        }

        [TestMethod]
        public void Test_ExpressionWithoutArithmetic()
        {
            var code = @"
﻿﻿using System;

namespace ConsoleApp
{
    class Program
    {
        static void Main() {}
        
        static int Evaluate(params bool[] parameters)
        {
            int x;
            x = 1;
            if (parameters[30])
            {
                x = 2;
                if (x == 2)
                {
                    x = 3;
                    if (parameters[31])
                    {
                        x = 4;
                    }
                }
            }
            return x;
        }
    }
}
";

            var result = ProblemSolver.Run(code);

            Assert.AreEqual(AnalysisType.Static, result.UsedAnalysisType);
            CollectionAssert.AreEquivalent(new[] { 1, 3, 4 }, result.ReturnValues.ToArray());
        }

        [TestMethod]
        public void Test_ConstantExpression()
        {
            var code = @"
﻿﻿using System;

namespace ConsoleApp
{
    class Program
    {
        static void Main() {}
        
        static int Evaluate(params bool[] parameters)
        {
            int x;
            x = 1;
            if (parameters[22])
            {
                x = 2;
                if (parameters[23 - 1])
                {
                    x = 3;
                }
            }
            return x;
        }
    }
}
";

            var result = ProblemSolver.Run(code);
            CollectionAssert.AreEquivalent(new[] { 1, 3 }, result.ReturnValues.ToArray());
        }

        [TestMethod]
        public void Test_NotConstantParameterAccess()
        {
            var code = @"
﻿﻿using System;

namespace ConsoleApp
{
    class Program
    {
        static void Main() {}
        
        static int Evaluate(params bool[] parameters)
        {
            int x;
            x = 1;
            if (parameters[22])
            {
                x = 2;
                if (parameters[24 - x])
                {
                    x = 3;
                }
            }
            return x;
        }
    }
}
";

            var result = ProblemSolver.Run(code, dryRun: true);
            Assert.AreEqual(AnalysisType.Dynamic, result.UsedAnalysisType);
        }


        [TestMethod]
        public void Test_ParamCountThreshold()
        {
            var code = @"
﻿﻿using System;

namespace ConsoleApp
{
    class Program
    {
        static void Main() {}
        
        static int Evaluate(params bool[] parameters)
        {
            int x;
            x = 1;
            if (parameters[0])
            {
                if (parameters[20])
                {
                    x = 2;
                }
            }
            return x;
        }
    }
}
";

            var result = ProblemSolver.Run(code);

            Assert.AreEqual(AnalysisType.Static, result.UsedAnalysisType);
        }

        [TestMethod]
        public void Test_NoParams_StaticAnalysis()
        {
            var code = @"
﻿﻿using System;

namespace ConsoleApp
{
    class Program
    {
        static void Main() {}
        
        static int Evaluate(params bool[] parameters)
        {
            int x;
            x = 1;
            return x;
        }
    }
}
";

            var result = ProblemSolver.Run(code, AnalysisType.Static);

            Assert.AreEqual(AnalysisType.Static, result.UsedAnalysisType);
            CollectionAssert.AreEquivalent(new [] { 1 }, result.ReturnValues.ToArray());
        }

        [TestMethod]
        public void Test_NoParams_DynamicAnalysis()
        {
            var code = @"
﻿﻿using System;

namespace ConsoleApp
{
    class Program
    {
        static void Main() {}
        
        static int Evaluate(params bool[] parameters)
        {
            int x;
            x = 1;
            return x;
        }
    }
}
";

            var result = ProblemSolver.Run(code, AnalysisType.Dynamic);

            Assert.AreEqual(AnalysisType.Dynamic, result.UsedAnalysisType);
            CollectionAssert.AreEquivalent(new [] { 1 }, result.ReturnValues.ToArray());
        }



        [TestMethod]
        public void Test_LiteralsBound10Exceeded_StaticAnalysis()
        {
            var code = @"
﻿﻿using System;

namespace ConsoleApp
{
    class Program
    {
        static void Main() {}
        
        static int Evaluate(params bool[] parameters)
        {
            int x;
            x = 1;
            if (parameters[0])
            {
                x = 2;
            }
            if (parameters[1])
            {
                x = 3;
            }
            if (parameters[2])
            {
                x = 4;
            }
            if (parameters[3])
            {
                x = 5;
            }
            if (parameters[4])
            {
                x = 6;
            }
            if (parameters[5])
            {
                x = 7;
            }
            if (parameters[6])
            {
                x = 8;
            }
            if (parameters[7])
            {
                x = 9;
            }
            if (parameters[8])
            {
                x = 10;
            }
            if (parameters[9])
            {
                x = 11;
            }
            return x;
        }
    }
}
";

            var result = ProblemSolver.Run(code, AnalysisType.Static);
            Assert.IsFalse(result.Success);
        }
    }
}
