using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Money_Management
{
    public static class LArrayExtensions
    {
        public static LArray WinTrade(this LArray array)
        {
            if (array.Count == 1)
            {
                throw new NewArray();
            }
            array.RemoveAt(array.Count - 1);
            array.RemoveAt(0);
            if (array.Count == 0)
            {
                throw new NewArray();
            }
            return array;
        }

        public static LArray LoseTrade(this LArray array)
        {
            if (array.Count == 1)
            {
                array.Add(array.Last<double>());
                return array;
            }
            array.Add(CalcBet(array));
            return array;
        }

        public static double CalcBet(this LArray array)
        {
            if (array.Count == 1)
            {
                return array.ToArray()[0];
            }
            return array.ToArray()[0] + array.ToArray()[array.Count - 1];
        }

        public static void AnalyzeBet(this LArray array)
        {
            var bet = CalcBet(array);

            if (bet >= array.Balance * .2 / array.Pairs) throw new NewArray();
        }

        public static void Print(this LArray array)
        {
            foreach (var i in array)
            {
                Console.Write(i + " ");
            }
            Console.ReadLine();
        }

    }
}
