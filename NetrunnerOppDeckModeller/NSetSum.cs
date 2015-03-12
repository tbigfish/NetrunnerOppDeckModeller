using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetRunnerDBScrapper
{
    public class NSetSum
    {
        public Card A { get; private set; }

        public Card B { get; private set; }

        public Card C { get; private set; }

        public float AontoB { get { return (((float)Count / (float)A.OccuranceCount) * 100); } }

        public float BontoA { get { return (((float)Count / (float)B.OccuranceCount) * 100); } }

        public string CardAName
        {
            get
            {
                return A.Name;
            }
        }

        public string CardBName
        {
            get
            {
                return B.Name;
            }
        }

        public string IdentityName
        {
            get
            {
                if (C == null)
                {
                    return "n/a";
                }
                else
                {
                    return C.Name;
                }
            }
        }

        public int Count { get; private set; }

        public NSetSum(Card a, Card b, int val)
        {
            A = a;
            B = b;
            Count = val;
        }

        public NSetSum(Card a, Card b, Card identity, int val)
        {
            A = a;
            B = b;
            C = identity;
            Count = val;
        }
    }
}
