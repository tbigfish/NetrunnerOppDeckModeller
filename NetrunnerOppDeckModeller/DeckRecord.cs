using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetRunnerDBScrapper
{
    public class DeckRecord
    {
        public int ID { get; set; }

        public string DeckName { get; set; }

        public Dictionary<int, int> CardCount = new Dictionary<int, int>();

        public DeckRecord(int id, string name)
        {
            this.ID = id;
            this.DeckName = name;
        }

        public override string ToString()
        {
            return string.Format("{0}, ({1})", DeckName, ID);
        }

        public void AddCard(int id, int count)
        {
            CardCount.Add(id, count);
        }
    }
}
