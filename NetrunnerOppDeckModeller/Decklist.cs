using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetRunnerDBScrapper
{
    public class Decklist
    {
        public static Dictionary<int, Decklist> DECKLISTLIST = new Dictionary<int, Decklist>();
        private static bool DECKLISTS_LOADED = false;

        public int ID { get; set; }

        public string Name { get; set; }

        public Card Identity { get; set; }

        public int NumFavourites { get; set; }

        public int NumLikes { get; set; }

        public int NumComments { get; set; }

        public List<Card> CardList { get; private set; }

        public static bool Loaded
        {
            get
            {
                return DECKLISTS_LOADED;
            }
        }

        public Decklist(int id, string name)
        {
            this.ID = id;
            this.Name = name;
            CardList = new List<Card>();
        }

        public static int[] GetRequiredAgendaPoints(int deckSize)
        {
            //40 to 44 cards requires 18 or 19 agenda points.
            //(Note: Identities in this set have a 45 card minimum)
            //- 45 to 49 cards requires 20 or 21 agenda points.
            //- 50 to 54 cards requires 22 or 23 agenda points.
            //For decks larger than this, add 2 additional agenda points to
            //the 54 card deck requirements each time the number of cards
            //in the deck reaches a multiple of 5 (55, 60, 65, etc.).

            if (deckSize < 40)
            {
                return new int[2] { 18, 19 };
            }
            else
            {
                int[] retVal = new int[2] { 18, 19 };
                int extraPoints = ((deckSize - 40) / 5) * 2;
                retVal[0] += extraPoints;
                retVal[1] += extraPoints;

                return retVal;
            }
        }

        public static void AddDeck(Decklist deck)
        {
            if(DECKLISTLIST.ContainsKey(deck.ID))
            {
                throw new ApplicationException("Duplicate Deck Add Attempt! - " + deck.ID);
            }

            DECKLISTLIST.Add(deck.ID, deck);
        }

        public static int GetRemainingInfluence(Card identity, List<Card> decklist)
        {
            //Special card that tweaks player influence
            if (identity.ID == 3029)
            {
                //The first copy of each program in this deck does not count against your influence limit.
                int influenceSpentOnNonPrograms = decklist.Where(x => (x.CardType != Card.CardTypeEnum.Identity) && (x.Faction != identity.Faction) && (x.CardType != Card.CardTypeEnum.Program)).Sum(x => x.Influence);
                int influenceSpentOnPrograms = 0;

                foreach(var offFactionProgramGroup in decklist.Where(x => (x.CardType == Card.CardTypeEnum.Program) && (x.Faction != identity.Faction)).GroupBy(x => x.ID))
                {
                    influenceSpentOnPrograms += (offFactionProgramGroup.Sum(x => x.Influence) - offFactionProgramGroup.First().Influence); //Count all but the first one
                }

                return influenceSpentOnNonPrograms + influenceSpentOnPrograms;
            }
            else
            {
                return identity.Influence - decklist.Where(x => (x.CardType != Card.CardTypeEnum.Identity) && (x.Faction != identity.Faction)).Sum(x => x.Influence);
            }
        }

        public int RemainingInfluence
        {
            get
            {
                return GetRemainingInfluence(this.Identity, this.CardList);
            }
        }

        public int GetAgendaPoints()
        {
            return Decklist.GetAgendaPoints(this.CardList);
        }

        public static int GetAgendaPoints(List<Card> decklist)
        {
            return decklist.Sum(x => x.AgendaPoints);
        }

        public void AddCard(Card card, int count)
        {
            if(card == null)
            {
                throw new ApplicationException("NULL passed to AddCard()");
            }

            int resultantCount = count;

            if(CardList.Any(x => x == card))
            {
                resultantCount += CardList.Count(x => x == card);
            }

            for (int i = 0; i < count; i++)
            {
                CardList.Add(card);
            }
        }

        public static void LoadDeckData(string decklistDataFilePath)
        {
            if (!Decklist.DECKLISTS_LOADED)
            {
                string datapath = decklistDataFilePath;

                if (!System.IO.File.Exists(datapath))
                {
                    System.Diagnostics.Debug.Assert(false);
                    return;
                }

                Microsoft.VisualBasic.FileIO.TextFieldParser reader = new Microsoft.VisualBasic.FileIO.TextFieldParser(datapath);
                reader.HasFieldsEnclosedInQuotes = true;
                reader.SetDelimiters(",");

                string currentline = reader.ReadLine();

                string[] fields;
                Decklist currentDeckList = null;

                while (!reader.EndOfData)
                {
                    fields = reader.ReadFields();
                    int deckId, cardId, quantity, comments, favourites, likes = -1;
                    string deckname = fields[1];

                    if (!Int32.TryParse(fields[0], out deckId)
                        || !Int32.TryParse(fields[2], out cardId)
                        || !Int32.TryParse(fields[3], out quantity)
                        || !Int32.TryParse(fields[4], out comments)
                        || !Int32.TryParse(fields[5], out favourites)
                        || !Int32.TryParse(fields[6], out likes))
                    {
                        throw new ApplicationException(string.Format("Parsing error - {0},{1},{2},{3},{4},{5},{6}", new object[] { fields[0], fields[1], fields[2], fields[3], fields[4], fields[5], fields[6] }));
                    }

                    if ((currentDeckList == null) || (currentDeckList.ID != deckId))
                    {
                        //Next deck
                        currentDeckList = new Decklist(deckId, deckname);
                        Decklist.AddDeck(currentDeckList);

                        //The first card is the identity
                        currentDeckList.Identity = Card.GetCard(cardId);
                        currentDeckList.NumComments = comments;
                        currentDeckList.NumFavourites = favourites;
                        currentDeckList.NumLikes = likes;
                    }
                    else
                    {
                        //Add this card
                        currentDeckList.AddCard(Card.GetCard(cardId), quantity);
                    }

                    //System.Diagnostics.Debug.Write(deckId + "," + cardId + "\r\n");
                }

                reader.Close();
                DECKLISTS_LOADED = true;
            }
        }
    }
}
