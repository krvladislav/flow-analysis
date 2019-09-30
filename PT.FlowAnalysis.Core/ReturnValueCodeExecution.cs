using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace PT.FlowAnalysis.Core
{
    internal class ReturnValueCodeExecution
    {
        internal static IEnumerable<object> ExecuteCodeForAllCases(MethodInfo evaluateMethodInfo, int paramCount)
        {
            if (paramCount > sizeof(long) * 8 - 2)
            {
                throw new NotImplementedException($"Code execution available only for {sizeof(long) * 8 - 2} parameters maximum");
            }

            var returnValues = new ConcurrentDictionary<object, bool>();

            Parallel.For(0, (long)Math.Pow(2, paramCount), argVector =>
            {
                object value;
                if (paramCount == 0)
                {
                    value = evaluateMethodInfo.Invoke(null, new object[] { null });
                }
                else
                {
                    var args = new bool[paramCount];
                    var boolArgVector = Convert.ToString(argVector, 2).Select(arg => arg == '1').ToArray();
                    boolArgVector.CopyTo(args, 0);
                    value = evaluateMethodInfo.Invoke(null, new object[] { args });
                }
                returnValues[value] = true;
            });

            return returnValues.Keys;
        }
    }
}