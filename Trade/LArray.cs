using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Money_Management
{
    public class LArray : List<double>
    {
        public int Lines { get; set; }
        public double Balance { get; set; }
        public double PercentGain { get; set; }
        public int Pairs { get; set; }

        public LArray(int lines, double balance, double percentGain = 1, int pairs = 1)
        {
            Lines = lines;
            Balance = balance;
            PercentGain = percentGain;
            Pairs = pairs;

            CreateInitial();
        }

        public void CreateInitial()
        {
            var bet = Balance * (PercentGain / Pairs / Lines) / 100;

            for (int i = 0; i < Lines; i ++)
            {
                this.Add(bet);
            }
        }
    }
}
