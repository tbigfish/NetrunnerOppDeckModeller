using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetRunnerDBScrapper
{
    public class CardStats
    {
        public Card ParentCard = null;
        public Card SubjectCard = null;

        public Dictionary<int, int> Record = new Dictionary<int, int>();

        public CardStats(Card ParentCard, Card SubjectCard)
        {
            this.ParentCard = ParentCard;
            this.SubjectCard = SubjectCard;
        }

        public void Add(int deckId, int count)
        {
            //Check if already have this connection
            if (!Record.ContainsKey(deckId))
            {
                Record.Add(deckId, count);
            }
        }

        public int GetSumOfAppearances()
        {
            int retVal = 0;

            foreach(int value in Record.Values)
            {
                retVal += value;
            }

            return retVal;
        }

        public int GetCountOfAppearances()
        {
            return Record.Values.Count;
        }

        public float GetAverageCount()
        {
            float count = ((float)GetCountOfAppearances());

            if(count <= 0)
            {
                return 0;
            }

            float total = (float)GetSumOfAppearances();

            return total / count;
        }

        public override string ToString()
        {
            return string.Format("{0} > {1} (Total: {2}, Av: {3})", new object[] { ParentCard, SubjectCard, GetSumOfAppearances(), GetAverageCount() });
        }
    }
}
