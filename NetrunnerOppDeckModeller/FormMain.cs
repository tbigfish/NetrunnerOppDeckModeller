using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;
using System.Xml;
using HtmlAgilityPack;
using System.IO;
using System.Threading;

namespace NetRunnerDBScrapper
{
    public partial class FormMain : Form
    {
        private static string NetRunnerDBSearchURL = "http://netrunnerdb.com/en/decklists/find/{0}?faction=&author=&title=&sort=date&packs%5B%5D=1&packs%5B%5D=8&packs%5B%5D=21&packs%5B%5D=28&packs%5B%5D=2&packs%5B%5D=3&packs%5B%5D=4&packs%5B%5D=5&packs%5B%5D=6&packs%5B%5D=7&packs%5B%5D=9&packs%5B%5D=10&packs%5B%5D=11&packs%5B%5D=12&packs%5B%5D=19&packs%5B%5D=18&packs%5B%5D=22&packs%5B%5D=23&packs%5B%5D=24&packs%5B%5D=25&packs%5B%5D=26&packs%5B%5D=27";
        private static string NetRunnerDBCardURL = "http://netrunnerdb.com/en/card/{0}";
        private static string NetRunnerDBDeckURL = "http://netrunnerdb.com/en/decklist/{0}";
        private static string MAINDATAPATH = "D:\\UserData\\Dev\\NetrunnerOppDeckModeller\\NetrunnerOppDeckModeller\\";
        private static int MAX_CARD_ID = 09000;
        private static int NUM_DECK_PAGES_SCRAPE = 50;

        private delegate void DELEGATE_W_STRING(string data);
        private delegate void DELEGATE_W_BOOL(bool data);
        private delegate void DELEGATE_W_NOWT();
        private delegate void DELEGATE_W_LSTRING_STRING(List<Card> data, string message);
        private DELEGATE_W_STRING updateStatusDelegate;
        private DELEGATE_W_BOOL enableDisableControlsDelegate;
        private DELEGATE_W_BOOL enableDisableNSetControlsDelegate;
        //private DELEGATE_W_NOWT refreshNSetDGDelegate;
        private DELEGATE_W_NOWT refreshCardMetaDataDGDelegate;
        private DELEGATE_W_LSTRING_STRING showDecklistDelegate;

        private BindingSortableList<Card> _currentDeckList = new BindingSortableList<Card>();
        private NSetCollection _nSetCollection = null;
        
        /// <summary>
        /// An enum describing the deck predictio method
        /// </summary>
        private enum DeckPredictionModeEnum 
        { 
            //Ordered chronologically
            PREDICTION_MODE_BEST_GUESS, 
            PREDICTION_MODE_CARD_MULTIPLICITY, 
            PREDICTION_MODE_INFLUENCE_PRIORITISED,
            PREDICTION_MODE_INFLUENCE_FILTERED
        }

        /// <summary>
        /// Default constructor
        /// </summary>
        public FormMain()
        {
            InitializeComponent();
            this.dataGridViewCardData.AutoGenerateColumns = false;
            this.dataGridViewCurrentDecklist.AutoGenerateColumns = false;
            //this.dataGridViewNSetData.AutoGenerateColumns = false;
            updateStatusDelegate = new DELEGATE_W_STRING(_updateStatus);
            enableDisableControlsDelegate = new DELEGATE_W_BOOL(_enableDisableControls);
            enableDisableNSetControlsDelegate = new DELEGATE_W_BOOL(_enableDisableNSetControls);
            //refreshNSetDGDelegate = new DELEGATE_W_NOWT(_refreshNSetDataGridView);
            refreshCardMetaDataDGDelegate = new DELEGATE_W_NOWT(_refreshCardMetaDataDGDelegate);
            showDecklistDelegate = new DELEGATE_W_LSTRING_STRING(_showDecklist);
        }

        /// <summary>
        /// Stops the HTMLAgilityPack from screwing up the parser when we do a datarip
        /// </summary>
        /// <param name="input">string to format</param>
        /// <returns>Correctly formatted string</returns>
        private string FixHtmlAgilityPackBeingShit(string input)
        {
            input = input.Replace("Ã©", "é");
            input = input.Replace("Å«", "ū");
            input = input.Replace("Ã", "à");
            input = input.Replace("Å", "ō");

            input = input.Replace("\"", "\"\"");

            return input;
        }

        /// <summary>
        /// Updates the status string on the bottom left of the main screen
        /// </summary>
        /// <param name="status">Text to set</param>
        private void _updateStatus(string status)
        {
            this.toolStripStatusLabel1.Text = status;
        }

        /// <summary>
        /// Updates (Invokes?) the status string on the bottom left of the main screen
        /// </summary>
        /// <param name="status">Text to set</param>
        private void UpdateStatus(string status)
        {
            if (InvokeRequired)
            {
                this.Invoke(updateStatusDelegate, new object[] { status });
            }
            else
            {
                _updateStatus(status);
            }
        }

        /// <summary>
        /// Sets the enabled status of the controls in both groupboxes
        /// </summary>
        /// <param name="enabled">Status to set</param>
        private void _enableDisableControls(bool enabled)
        {
            this.groupBoxNSetGeneration.Enabled = enabled;
            this.groupBoxDeckPrediction.Enabled = enabled;

            UpdateStatus("Ready");
        }

        /// <summary>
        /// Sets (Invokes?) the enabled status of the controls in both groupboxes
        /// </summary>
        /// <param name="enabled">Status to set</param>
        private void EnableDisableControls(bool enabled)
        {
            if (InvokeRequired)
            {
                this.Invoke(enableDisableControlsDelegate, new object[] { enabled });
            }
            else
            {
                _enableDisableControls(enabled);
            }
        }

        /// <summary>
        /// Sets the enabled status of the controls in the NSet groupbox
        /// </summary>
        /// <param name="enabled">Status to set</param>
        private void _enableDisableNSetControls(bool enabled)
        {
            if((!Card.Loaded) || (!Decklist.Loaded))
            {
                //Cards/Decks failed to load, abort
                this.Close();
                return;
            }

            this.groupBoxNSetGeneration.Enabled = enabled;
            RefreshCardDataGridView();
            this.labelNumDecks.Text = "Num Decks: " + Decklist.DECKLISTLIST.Count();
            UpdateStatus("Ready");
        }

        /// <summary>
        /// Sets (Invokes?) the enabled status of the controls in the NSet groupbox
        /// </summary>
        /// <param name="enabled">Status to set</param>
        private void EnableDisableNSetControls(bool enabled)
        {
            if (InvokeRequired)
            {
                this.Invoke(enableDisableNSetControlsDelegate, new object[] { enabled });
            }
            else
            {
                _enableDisableNSetControls(enabled);
            }
        }

        /// <summary>
        /// Main method to generate 3-sets (which is a 2-set with an identity attached)
        /// </summary>
        /// <param name="minComments">Minimum number of comments each decklist requires to be included in this generation process</param>
        /// <param name="minFavourites">Minimum number of favourites each decklist requires to be included in this generation process</param>
        /// <param name="minLikes">Minimum number of likes each decklist requires to be included in this generation process</param>
        /// <param name="distinctNSets">Call Distinct() on the decklist before generation begins</param>
        /// <param name="selfNSets">All creation of n-Sets with duplicate items</param>
        private void GenerateTriSets(int minComments, int minFavourites, int minLikes, bool distinctNSets, bool selfNSets)
        {
            float counter = 0;
            _nSetCollection = new NSetCollection(3);

            foreach (Card card in Card.CARDLIST.Values)
            {
                card.OccuranceCount = 0;
                card.DeckInclusionCount = 0;
            }

            var decklistsToConsider = Decklist.DECKLISTLIST.Values.Where(x => (x.NumComments >= minComments) && (x.NumFavourites >= minFavourites) && (x.NumLikes >= minLikes));

            foreach (Decklist decklist in decklistsToConsider)
            {
                List<Card> actualCardList = new List<Card>(decklist.CardList);

                //Inc counters
                decklist.Identity.OccuranceCount++; //Need to do this here, as the Identity isn't added to this list like in the Bigrams code
                decklist.Identity.DeckInclusionCount++;

                foreach (Card card in decklist.CardList)
                {
                    card.OccuranceCount++;
                }

                foreach (Card card in actualCardList.Distinct())
                {
                    card.DeckInclusionCount++;
                }
                //End Inc Counters;

                if (distinctNSets)
                {
                    actualCardList = actualCardList.Distinct().ToList();
                }

                UpdateStatus(string.Format("Creating n-Sets from Deck {0} ({1}%)", decklist.ID, ((counter / (float)decklistsToConsider.Count()) * 100).ToString("0.00")));

                foreach (Card cardA in actualCardList)
                {
                    foreach (Card cardB in actualCardList)
                    {
                        if (selfNSets || (cardA != cardB))
                        {
                            _nSetCollection.AddStatistic(decklist.Identity, cardA, cardB, cardB.CostsInfluence(decklist.Identity.Faction), (short)decklist.CardList.Count(x => x.ID == cardB.ID));
                        }
                    }
                }

                counter++;
            }

            RefreshCardMetaDataGridView();
            EnableDisableControls(true);
        }

        /// <summary>
        /// Main method to generate 2-set
        /// </summary>
        /// <param name="minComments">Minimum number of comments each decklist requires to be included in this generation process</param>
        /// <param name="minFavourites">Minimum number of favourites each decklist requires to be included in this generation process</param>
        /// <param name="minLikes">Minimum number of likes each decklist requires to be included in this generation process</param>
        /// <param name="distinctNSets">Call Distinct() on the decklist before generation begins</param>
        /// <param name="selfNSets">All creation of n-Sets with duplicate items</param>
        private void GenerateBiSets(int minComments, int minFavourites, int minLikes, bool distinctNSets, bool selfNSets)
        {
            float counter = 0;
            _nSetCollection = new NSetCollection(2);

            foreach (Card card in Card.CARDLIST.Values)
            {
                card.OccuranceCount = 0;
                card.DeckInclusionCount = 0;
            }

            var decklistsToConsider = Decklist.DECKLISTLIST.Values.Where(x => (x.NumComments >= minComments) && (x.NumFavourites >= minFavourites) && (x.NumLikes >= minLikes));

            foreach (Decklist decklist in decklistsToConsider)
            {
                List<Card> actualCardList = new List<Card>(decklist.CardList);
                actualCardList.Add(decklist.Identity);

                //Inc counters
                foreach (Card card in actualCardList)
                {
                    card.OccuranceCount++;
                }

                foreach (Card card in actualCardList.Distinct())
                {
                    card.DeckInclusionCount++;
                }
                //End Inc Counters;

                if (distinctNSets)
                {
                    //Distinct NSets
                    actualCardList = actualCardList.Distinct().ToList();
                }

                UpdateStatus(string.Format("Creating n-Sets from Deck {0} ({1}%)", decklist.ID, ((counter / (float)decklistsToConsider.Count()) * 100).ToString("0.00")));

                foreach (Card cardA in actualCardList)
                {
                    foreach (Card cardB in actualCardList)
                    {
                        if (selfNSets || (cardA != cardB))
                        {
                            _nSetCollection.AddStatistic(decklist.Identity, cardA, cardB, cardB.CostsInfluence(decklist.Identity.Faction), (short)decklist.CardList.Count(x => x.ID == cardB.ID));
                        }
                    }
                }

                counter++;
            }

            RefreshCardMetaDataGridView();
            EnableDisableControls(true);
        }

        /// <summary>
        /// Refresh the CardMetaDataDG
        /// </summary>
        private void _refreshCardMetaDataDGDelegate()
        {
            this.dataGridViewCardMetaData.DataSource = null;
            this.dataGridViewCardMetaData.DataSource = new BindingSortableList<Card>(Card.CARDLIST.Values);
        }

        /// <summary>
        /// Refresh (Invoke?) the CardMetaDataDG
        /// </summary>
        private void RefreshCardMetaDataGridView()
        {
            if (this.InvokeRequired)
            {
                this.Invoke(refreshCardMetaDataDGDelegate, new object[] { }); ;
            }
            else
            {
                _refreshCardMetaDataDGDelegate();
            }
        }

        /// <summary>
        /// Perform a card data scrape of the NetrunnerDB website. Downloads data on all cards.
        /// </summary>
        private void CardDataScrape()
        {
            System.IO.StreamWriter writer = System.IO.File.CreateText(MAINDATAPATH + "NetRunnerCardData.csv");
            writer.WriteLine("ID,Name,Type,Faction,Influence,Agenda");

            for (int i = 1001; i < MAX_CARD_ID; i++)
            {
                UpdateStatus("Pulling Card " + i);
                string idStr = i.ToString().PadLeft(5, '0');
                string url = string.Format(NetRunnerDBCardURL, idStr);
                HttpWebRequest request = WebRequest.CreateHttp(url);
                string currentLine = idStr + ",";

                try
                {
                    HttpWebResponse response = (HttpWebResponse)request.GetResponse();

                    HtmlAgilityPack.HtmlDocument doc = new HtmlAgilityPack.HtmlDocument();
                    doc.Load(response.GetResponseStream());

                    //Card Name
                    HtmlNode name = doc.DocumentNode.SelectSingleNode("//a[@class='card-title']");

                    if (name != null)
                    {
                        currentLine += "\"" + FixHtmlAgilityPackBeingShit(System.Web.HttpUtility.HtmlDecode(name.InnerText)) + "\",";
                    }
                    else
                    {
                        //Try the previewer
                        name = doc.DocumentNode.SelectSingleNode("//a[@class='card-title card-preview']");

                        if (name != null)
                        {
                            currentLine += "\"" + FixHtmlAgilityPackBeingShit(System.Web.HttpUtility.HtmlDecode(name.InnerText)) + "\",";
                        }
                        else
                        {
                            throw new ApplicationException("DATA ERROR!");
                        }
                    }

                    //Card Type
                    Card.CardTypeEnum type = (Card.CardTypeEnum)Enum.Parse(typeof(Card.CardTypeEnum), doc.DocumentNode.SelectSingleNode("//span[@class='card-type']").InnerText.Trim());
                    currentLine += type.ToString() + ",";

                    //Faction
                    string factionData = doc.DocumentNode.SelectSingleNode("//div[@class='card-illustrator']//small").InnerText;
                    factionData = factionData.Substring(0, factionData.IndexOf("&")).Trim().Replace("-", "");

                    if (factionData.Contains(" "))
                    {
                        factionData = factionData.Substring(0, factionData.IndexOf(" ")).Trim();
                    }

                    Card.FactionEnum faction = (Card.FactionEnum)Enum.Parse(typeof(Card.FactionEnum), factionData);
                    currentLine += faction.ToString() + ",";

                    //Influence
                    string influence = doc.DocumentNode.SelectSingleNode("//span[@class='card-prop']").InnerText;

                    if (influence.Contains("Influence:"))
                    {
                        influence = influence.Substring(influence.IndexOf("Influence:") + 10).Trim();

                        if (influence.StartsWith("&"))
                        {
                            //No influence on this card?
                            currentLine += "0,";
                        }
                        else
                        {
                            Int32.Parse(influence);

                            currentLine += influence + ",";
                        }
                    }
                    else
                    {
                        //No influence on this card?
                        currentLine += "0,";
                    }

                    //Agenda points
                    string agendaPoints = doc.DocumentNode.SelectSingleNode("//span[@class='card-prop']").InnerText;

                    if (agendaPoints.Contains("Score:"))
                    {
                        agendaPoints = agendaPoints.Substring(influence.IndexOf("Score:") + 6).Trim();

                        if (agendaPoints.StartsWith("&"))
                        {
                            //No influence on this card?
                            currentLine += "0";
                        }
                        else
                        {
                            Int32.Parse(agendaPoints);

                            currentLine += agendaPoints;
                        }
                    }
                    else
                    {
                        //No influence on this card?
                        currentLine += "0";
                    }

                    writer.WriteLine(currentLine);
                }
                catch (System.Net.WebException)
                {
                    //writer.WriteLine(i + ",INVALID");

                    //We've reached the end of a set, so skip to the next 1000 - NetRunnerDB is weird
                    i = (((int)(((float)i) / 1000) + 1) * 1000);
                }
            }

            writer.Flush();
            writer.Close();

            UpdateStatus("Data Pull Complete!");
            EnableDisableControls(true);
        }

        /// <summary>
        /// Perform a deck data scrape of the NetrunnerDB website. Downloads data from the number of pages specified by NUM_DECK_PAGES_SCRAPE.
        /// </summary>
        private void DeckDataScrape()
        {
            List<Decklist> decks = new List<Decklist>();

            for (int i = 1; i <= NUM_DECK_PAGES_SCRAPE; i++)
            {
                this.UpdateStatus("Ripping decks from page " + i);
                HttpWebRequest request = WebRequest.CreateHttp(string.Format(NetRunnerDBSearchURL, i));
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();

                HtmlAgilityPack.HtmlDocument doc = new HtmlAgilityPack.HtmlDocument();
                doc.Load(response.GetResponseStream());

                foreach (HtmlNode link in doc.DocumentNode.SelectNodes("//a[@class='decklist-name'][@href]"))
                {
                    string deckId = link.Attributes["href"].Value;
                    deckId = deckId.Substring(deckId.IndexOf("/en/decklist/") + 13);
                    deckId = deckId.Substring(0, deckId.IndexOf("/"));

                    decks.Add(new Decklist(Int32.Parse(deckId), FixHtmlAgilityPackBeingShit(System.Web.HttpUtility.HtmlDecode(link.InnerText))));
                }
            }

            int deckCounter = 0;

            //Then get each of the decks
            foreach (Decklist dRec in decks)
            {
                this.UpdateStatus(string.Format("Processing Deck {0}/{1} ({2})", new object[] { deckCounter, decks.Count(), dRec.Name }));

                HttpWebRequest request = WebRequest.CreateHttp(string.Format(NetRunnerDBDeckURL, dRec.ID));
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();

                System.IO.StreamReader reader = new System.IO.StreamReader(response.GetResponseStream());
                string rawPageData = reader.ReadToEnd();

                //chop chop
                string data = rawPageData.Substring(rawPageData.IndexOf("cards\":[{\"card_code\"") + 8);
                data = data.Substring(0, data.IndexOf("]}"));

                //"cards":[{"card_code":"01007","qty":"2"},{"card_code":"01011","qty":"1"},{"card_code":"01018","qty":"2"},{"card_code":"01034","qty":"3"},{"card_code":"01043","qty":"3"},{"card_code":"01044","qty":"3"},{"card_code":"01045","qty":"1"},{"card_code":"01050","qty":"3"},{"card_code":"02009","qty":"2"},{"card_code":"02041","qty":"1"},{"card_code":"02046","qty":"1"},{"card_code":"02087","qty":"3"},{"card_code":"02107","qty":"2"},{"card_code":"03033","qty":"3"},{"card_code":"03035","qty":"3"},{"card_code":"03036","qty":"2"},{"card_code":"03041","qty":"3"},{"card_code":"03042","qty":"3"}]}

                string[] cards = data.Split(new string[] { "card_code\":\"", "},{", "\",\"qty\":\"", "\"", "{", "}" }, StringSplitOptions.RemoveEmptyEntries);

                for (int i = 0; i < cards.Length; i += 2)
                {
                    //i = cardId, i+1 = num

                    //Some sort of weird shit going on where sometimes there are 4 identities???  
                    int num = Int32.Parse(cards[i + 1]);

                    if ((num > 3) || (num < 1))
                    {
                        num = 1;
                    }

                    dRec.AddCard(Card.GetCard(Int32.Parse(cards[i])), num);
                }

                data = rawPageData.Substring(rawPageData.IndexOf("nbvotes\":\"") + 10);
                dRec.NumLikes = Int32.Parse(data.Substring(0, data.IndexOf("\"")));

                data = rawPageData.Substring(rawPageData.IndexOf("nbfavorites\":\"") + 14);
                dRec.NumFavourites = Int32.Parse(data.Substring(0, data.IndexOf("\"")));

                data = rawPageData.Substring(rawPageData.IndexOf("nbcomments\":\"") + 13);
                dRec.NumComments = Int32.Parse(data.Substring(0, data.IndexOf("\"")));
                deckCounter++;
            }

            //Validate the decks?

            System.IO.StreamWriter writer = System.IO.File.CreateText(MAINDATAPATH + "NetRunnerDeckData.csv");
            writer.WriteLine("DeckID,DeckName,CardID,Quantity");

            foreach (Decklist dRec in decks)
            {
                //Get the identity
                Card identity = dRec.CardList.Single(x => x.CardType == Card.CardTypeEnum.Identity);

                writer.WriteLine(string.Format("{0},\"{1}\",{2},{3},{4},{5},{6}", new object[] { dRec.ID, dRec.Name, identity.ID, 1, dRec.NumComments, dRec.NumFavourites, dRec.NumLikes }));

                foreach (var cardGroup in dRec.CardList.Where(x => x.CardType != Card.CardTypeEnum.Identity).GroupBy(x => x.ID))
                {
                    writer.WriteLine(string.Format("{0},\"{1}\",{2},{3},{4},{5},{6}", new object[] { dRec.ID, dRec.Name, cardGroup.First().ID, cardGroup.Count(), dRec.NumComments, dRec.NumFavourites, dRec.NumLikes }));
                }
            }

            writer.Flush();
            writer.Close();

            UpdateStatus("Data Pull Complete!");
            EnableDisableControls(true);
        }

        /// <summary>
        /// Gets the amount of unspent influence from the list stored in _currentDeckList
        /// </summary>
        /// <returns>An int representing the amount of unspent influence in _currentDeckList</returns>
        private int GetRemainingInfluenceFromCurrentDeck()
        {
            return Decklist.GetRemainingInfluence(this._currentDeckList.Single(x => x.CardType == Card.CardTypeEnum.Identity), this._currentDeckList.ToList());
        }

        /// <summary>
        /// Refreshes the DG holding the current decklist
        /// </summary>
        private void RefreshCurrentDecklist()
        {
            this.SuspendLayout();
            this.dataGridViewCurrentDecklist.Rows.Clear();

            foreach (var cardGroup in this._currentDeckList.GroupBy(x => x.ID))
            {
                this.dataGridViewCurrentDecklist.Rows.Add(new object[] { "X", cardGroup.First().Name, _currentDeckList.Count(x => x.ID == cardGroup.Key), null });
                this.dataGridViewCurrentDecklist.Rows[this.dataGridViewCurrentDecklist.Rows.Count - 1].Tag = cardGroup.Key;
            }

            //Update remaining influence display
            this.toolStripMenuItemRemainingInfluence.Text = "Remaining Influence: ";
            this.toolStripMenuItemRemainingInfluence.ForeColor = Color.Black;

            if (this._currentDeckList.Any(x => x.CardType == Card.CardTypeEnum.Identity))
            {
                Card identity = this._currentDeckList.Single(x => x.CardType == Card.CardTypeEnum.Identity);

                int sum = GetRemainingInfluenceFromCurrentDeck();

                if (sum < 0)
                {
                    this.toolStripMenuItemRemainingInfluence.ForeColor = Color.Red;
                }

                toolStripMenuItemRemainingInfluence.Text += sum;

                if(identity.IsCorp)
                {
                    int[] agendaReq = Decklist.GetRequiredAgendaPoints(this._currentDeckList.Count(x => x.CardType != Card.CardTypeEnum.Identity));
                    int agendaCurr = Decklist.GetAgendaPoints(this._currentDeckList.ToList());

                    if ((agendaReq[0] == agendaCurr) || (agendaReq[1] == agendaCurr))
                    {
                        this.toolStripMenuItemCurrentAgendaPoints.Text = string.Format("Current Agenda Points: {0}", agendaCurr);
                    }
                    else
                    {
                        this.toolStripMenuItemCurrentAgendaPoints.Text = string.Format("Current Agenda Points: {0} [Req: {1}/{2}]", new object[] { agendaCurr, agendaReq[0], agendaReq[1] });
                    }
                }
                else
                {
                    this.toolStripMenuItemCurrentAgendaPoints.Text = string.Empty;
                }
            }
            else
            {
                this.toolStripMenuItemCurrentAgendaPoints.Text = string.Empty;
                this.toolStripMenuItemRemainingInfluence.Text += "-";
            }

            this.ResumeLayout();
        }

        /// <summary>
        /// Gets all cards which could be added to the currentDecklist.
        /// </summary>
        /// <param name="currentDecklist"></param>
        /// <returns></returns>
        private List<int> GetAllowedCards(List<Card> currentDecklist)
        {
            return GetAllowedCards(currentDecklist, new List<Card>());
        }

        /// <summary>
        /// Gets all cards which could be added to the currentDecklist, skipping over banned cards.
        /// </summary>
        /// <param name="currentDecklist">Current decklist</param>
        /// <param name="extBannedCards">Cards which will not be returned</param>
        /// <returns>A list of ints representing Cards which could be added.</returns>
        private List<int> GetAllowedCards(List<Card> currentDecklist, List<Card> extBannedCards)
        {
            Card identity = currentDecklist.Single(x => x.CardType == Card.CardTypeEnum.Identity);
            int influenceLeft = GetRemainingInfluenceFromCurrentDeck();
            List<Card> bannedCards = new List<Card>();
            bannedCards.AddRange(extBannedCards);
            bannedCards.AddRange(currentDecklist.GroupBy(x => x.ID).Where(x =>
                ((x.Count() >= x.First().MaxNumPerDeck) //We already have max num in deck
                || (x.First().IsCorp != identity.IsCorp) //Not the wrong side
                || ((x.First().Faction != identity.Faction) && (x.First().Faction != Card.FactionEnum.Neutral) && (x.First().Influence > influenceLeft)) //Influence spent
                ) 
                ).SelectMany(x => x));
            bannedCards = bannedCards.Distinct().ToList();

            return Card.CARDLIST.Values.Where(x => !bannedCards.Contains(x)).Select(x => x.ID).ToList();
        }

        /// <summary>
        /// Gets a Dictionary of Predictions, each with a value representing the magnitude of supporting data. 
        /// </summary>
        /// <param name="knownCards">The observed cards</param>
        /// <param name="bannedCards">Cards which are banned from prediction</param>
        /// <param name="byPercentage">Whether n-Sets should be considered by percentage of the whole set or by magnitude</param>
        /// <returns></returns>
        private Dictionary<Card, int> GetPredictions(List<Card> knownCards, List<Card> bannedCards, bool byPercentage)
        {
            return GetPredictions(knownCards, bannedCards, byPercentage, null);
        }

        /// <summary>
        /// Gets a Dictionary of Predictions, each with a value representing the magnitude of supporting data. 
        /// </summary>
        /// <param name="knownCards">The observed cards</param>
        /// <param name="bannedCards">Cards which are banned from prediction</param>
        /// <param name="byPercentage">Whether n-Sets should be considered by percentage of the whole set or by magnitude</param>
        /// <param name="influenceFilter">True = return only cards that used influence, False = return only cards that didn't use influence, null = return all</param>
        /// <returns></returns>
        private Dictionary<Card, int> GetPredictions(List<Card> knownCards, List<Card> bannedCards, bool byPercentage, bool? influenceFilter)
        {
            Dictionary<Card, int> retVal = new Dictionary<Card, int>();
            Card identity = knownCards.Single(x => x.CardType == Card.CardTypeEnum.Identity);

            if (knownCards.Any(x => x.CardType == Card.CardTypeEnum.Identity))
            {
                List<int> availableCards = GetAllowedCards(knownCards, bannedCards);

                foreach (Card card in knownCards)
                {
                    foreach (Set otherCard in this._nSetCollection.GetAllMatches(card, identity, influenceFilter))
                    {
                        List<Card> otherCardKeys = otherCard.GetOtherSets(card.ID);

                        foreach (Card otherCardKey in otherCardKeys.Where(x => availableCards.Contains(x.ID)))
                        {
                            if (retVal.ContainsKey(otherCardKey))
                            {
                                retVal[otherCardKey]++;
                            }
                            else
                            {
                                retVal.Add(otherCardKey, 1);
                            }
                        }
                    }
                }
            }

            if (byPercentage)
            {
                float identityCounter = (float)(this._nSetCollection.GetCount(identity));

                //Turn these stats into percentages instead
                foreach (Card card in retVal.Keys.ToList())
                {
                    retVal[card] = (int)((((float)retVal[card]) / identityCounter) * 100);
                }
            }

            if(retVal.Count() == 0)
            {
                //We have a problem, as we have no records of use
                if(influenceFilter.HasValue)
                {
                    //Try the search again, but remove the influence restriction.
                    return GetPredictions(knownCards, bannedCards, byPercentage, null); //In VERY rare cases might cause a loop, but the scale of this project prevents a fix right now. 
                }
            }

            return retVal;
        }

        /// <summary>
        /// OnLoad event. Sets various controls to starting values.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Form1_Load(object sender, EventArgs e)
        {
            this.comboBoxStatsMode.SelectedIndex = 0;
            this.comboBoxnValue.SelectedIndex = 1; //Trigrams by default

            foreach(var predictionMode in Enum.GetNames(typeof(DeckPredictionModeEnum)))
            {
                this.comboBoxPredictionMethod.Items.Add(predictionMode);
            }

            this.comboBoxPredictionMethod.SelectedIndex = 0;
            this.toolStripComboBoxTypeFilter.SelectedIndex = 0;
            this.dataGridViewCardMetaData.AutoGenerateColumns = false;
        }

        /// <summary>
        /// Refreshes the main Card DG
        /// </summary>
        private void RefreshCardDataGridView()
        {
            this.dataGridViewCardData.DataSource = null;
            BindingSortableList<Card> cardData = null;
            string searchString = this.toolStripTextBoxSearchFilter.Text.Trim().ToUpper();
            Card.CardTypeEnum searchType = Card.CardTypeEnum.INVALID;
            Card identity = null;
            
            if (_currentDeckList.Any(x => x.CardType == Card.CardTypeEnum.Identity))
            {
                identity = _currentDeckList.Single(x => x.CardType == Card.CardTypeEnum.Identity);
            }

            if ((toolStripComboBoxTypeFilter.SelectedItem != null) && (toolStripComboBoxTypeFilter.SelectedIndex > 0))
            {
                searchType = (Card.CardTypeEnum)Enum.Parse(typeof(Card.CardTypeEnum), this.toolStripComboBoxTypeFilter.SelectedItem.ToString());
            }

            //Massive LINQ query of doom to select the correct cards to display
            var existingCardGroups = _currentDeckList.GroupBy(x => x.ID);

            cardData = new BindingSortableList<Card>(Card.CARDLIST.Values.Where(x =>

                //The first four lines depend on their being an identity in the deck
                (
                    (identity == null) //We have no identity in the deck OR
                    || 
                    (  
                        (x.CardType != Card.CardTypeEnum.Identity) //We've already added an Identity
                        && (x.IsCorp == identity.IsCorp) //Restrict by current side
                        && (!(identity.IsCorp) || (x.CardType != Card.CardTypeEnum.Agenda) || (x.CardType == Card.CardTypeEnum.Agenda && (x.Faction == identity.Faction || x.Faction == Card.FactionEnum.Neutral))) //We can't add off-faction agendas
                        && (!(existingCardGroups.Any(y => (y.Key == x.ID) && (y.Count() >= y.First().MaxNumPerDeck))))
                    )) //We already have max num in this deck
                
                //User Interface Search parameters
                && ((this.toolStripTextBoxSearchFilter.Text.Trim().Length == 0) || (x.Name.ToUpper().Contains(searchString) || (x.Faction.ToString().ToUpper().Contains(searchString)) || (x.CardType.ToString().ToUpper().Contains(searchString)))) //Do text search
                && ((searchType == Card.CardTypeEnum.INVALID) || (searchType == x.CardType)) //Restrict by search type

                ).ToList());

            this.dataGridViewCardData.DataSource = cardData;
        }

        /// <summary>
        /// OnShown event. Loads the stored Data.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Form1_Shown(object sender, EventArgs e)
        {
            EnableDisableControls(false);

            if(!Directory.Exists(MAINDATAPATH))
            {
                MessageBox.Show("Datapath is invalid or not found - '" + MAINDATAPATH + "'. Please move data files to this location, recompile the app with a new path, or give me more than 5 days to develop this application.", "Main Data Repository not found!", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
                this.Close();
                return;
            }

            Thread newT = new Thread(new ThreadStart(LoadData));
            newT.Start();
        }

        /// <summary>
        /// Loads all Card and Deck data
        /// </summary>
        private void LoadData()
        {
            UpdateStatus("Loading Data...");
            Card.LoadCardData(MAINDATAPATH + "NetRunnerCardData.csv");
            Decklist.LoadDeckData(MAINDATAPATH + "NetRunnerDeckData.csv");
            EnableDisableNSetControls(true);
        }

        /// <summary>
        /// OnMouseDown event for the DragDrop process
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dataGridViewCardData_MouseDown(object sender, MouseEventArgs e)
        {
            DataGridView.HitTestInfo info = dataGridViewCardData.HitTest(e.X, e.Y);

            if (info.RowIndex >= 0)
            {
                this.dataGridViewCardData.ClearSelection();
                this.dataGridViewCardData.Rows[info.RowIndex].Selected = true;
                this.dataGridViewCardData.DoDragDrop(((Card)this.dataGridViewCardData.Rows[info.RowIndex].DataBoundItem).ID, DragDropEffects.All);
            }
        }

        /// <summary>
        /// OnDragEnter event for the DragDrop process
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dataGridViewCurrentDecklist_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(int)))
            {
                e.Effect = DragDropEffects.Copy;
            }
            else if (e.Data.GetDataPresent(typeof(string)) && TextDataIsValid((string)e.Data.GetData(typeof(string))))
            {
                e.Effect = DragDropEffects.Copy;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        /// <summary>
        /// Determines if text dropped on the Current Decklist DG represents a valid decklist
        /// </summary>
        /// <param name="data">A string (hopefully) representing a valid decklist</param>
        /// <returns>True if the string represents a valid decklist</returns>
        private bool TextDataIsValid(string data)
        {
            int[] dontCare;
            return GetCardIdsFromText(data, out dontCare);
        }

        /// <summary>
        /// Attempts to extract a list of cards from block of csv
        /// </summary>
        /// <param name="data">A series of CSV representing card IDs</param>
        /// <param name="rData">The output data</param>
        /// <returns>True if the string represents a valid decklist</returns>
        private bool GetCardIdsFromText(string data, out int[] rData)
        {
            bool foundIdentity = false;
            string[] splits = data.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);
            rData = new int[splits.Length];

            if (splits.Length == 0)
            {
                return false;
            }

            int cardId = 0;
            int counter = 0;

            foreach (string s in splits)
            {
                if (!Int32.TryParse(s, out cardId))
                {
                    //invalid
                    return false;
                }

                if (!Card.CARDLIST.ContainsKey(cardId))
                {
                    return false;
                }
                else if(!foundIdentity)
                {
                    foundIdentity = (Card.CARDLIST[cardId].CardType == Card.CardTypeEnum.Identity);
                }

                rData[counter] = cardId;
                counter++;
            }

            return foundIdentity;
        }

        /// <summary>
        /// OnDragDrop event for the DragDrop process
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dataGridViewCurrentDecklist_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(int)))
            {
                Card droppedCard = Card.GetCard((int)e.Data.GetData(typeof(int)));

                if ((!this._currentDeckList.Any(x => x.CardType == Card.CardTypeEnum.Identity))
                    && (droppedCard.CardType != Card.CardTypeEnum.Identity))
                {
                    //We don't have an identity, and this isn't one, so don't add it
                }
                else if ((droppedCard.CardType == Card.CardTypeEnum.Identity)
                    && (this._currentDeckList.Any(x => x.CardType == Card.CardTypeEnum.Identity)))
                {
                    //We already have an identity
                }
                else if (this._currentDeckList.Count(x => x.ID == droppedCard.ID) > 2)
                {
                    //Don't add this card, we've exceed the max
                }
                else
                {
                    this._currentDeckList.Add(droppedCard);
                    this.RefreshCurrentDecklist();
                    this.RefreshCardDataGridView();
                }
            }
            else if (e.Data.GetDataPresent(typeof(string)))
            {
                int[] cardList; 
                    
                if(GetCardIdsFromText((string)e.Data.GetData(typeof(string)), out cardList))
                {
                    //Clear the current decklist
                    this._currentDeckList.Clear();

                    foreach(int i in cardList)
                    {
                        this._currentDeckList.Add(Card.CARDLIST[i]);
                    }
                }

                RefreshCurrentDecklist();
                this.RefreshCardDataGridView();
            }
        }

        /// <summary>
        /// Card to process the "delete" button on the CurrentDecklist
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dataGridViewCurrentDecklist_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if ((e.ColumnIndex == 0) && (e.RowIndex >= 0))
            {
                //Delete this row from the current deck
                Card rowCard = Card.GetCard((int)this.dataGridViewCurrentDecklist.Rows[e.RowIndex].Tag);

                this._currentDeckList.Remove(rowCard);
                this.RefreshCurrentDecklist();
                this.RefreshCardDataGridView();
            }
        }

        /// <summary>
        /// Refresh the CardDataDG because the search text changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripTextBoxSearchFilter_TextChanged(object sender, EventArgs e)
        {
            RefreshCardDataGridView();
        }

        /// <summary>
        /// Button to begin set generation
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonGenNSets_Click(object sender, EventArgs e)
        {
            EnableDisableControls(false);

            if (this.comboBoxnValue.SelectedIndex == 0)
            {
                Thread newThread = new Thread(() => GenerateBiSets((int)this.numericUpDownNumComments.Value, (int)this.numericUpDownNumFavs.Value, (int)this.numericUpDownNumLikes.Value, this.checkBoxDistinctNSets.Checked, this.checkBoxSelfNSets.Checked));
                newThread.Start();
            }
            else
            {
                Thread newThread = new Thread(() => GenerateTriSets((int)this.numericUpDownNumComments.Value, (int)this.numericUpDownNumFavs.Value, (int)this.numericUpDownNumLikes.Value, this.checkBoxDistinctNSets.Checked, this.checkBoxSelfNSets.Checked));
                newThread.Start();
            }
        }

        /// <summary>
        /// Update the display showing how many decklists will be included in the nSet generation, as we changed one of the filters
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void numericUpDownNumStats_ValueChanged(object sender, EventArgs e)
        {
            this.labelNumDecks.Text = "Num Decks: " + Decklist.DECKLISTLIST.Count(x => (x.Value.NumComments >= this.numericUpDownNumComments.Value) && (x.Value.NumFavourites >= this.numericUpDownNumFavs.Value) && (x.Value.NumLikes >= this.numericUpDownNumLikes.Value));
        }

        private void buttonPredictDeck_Click(object sender, EventArgs e)
        {
            if (_nSetCollection == null)
            {
                MessageBox.Show("Please generate nSets before attempting a predict");
                return;
            }

            if (this._currentDeckList.Any(x => x.CardType == Card.CardTypeEnum.Identity))
            {
                EnableDisableControls(false);
                this.toolStripStatusLabel1.Text = "Predicting Deck...";

                bool usePercentages = (comboBoxStatsMode.SelectedIndex == 1);
                int deckSize = (int)numericUpDownDeckSize.Value;
                DeckPredictionModeEnum predictionMode = (DeckPredictionModeEnum)Enum.Parse(typeof(DeckPredictionModeEnum), this.comboBoxPredictionMethod.SelectedItem.ToString());


                Thread newThread = new Thread(() => PredictDeck(predictionMode, _currentDeckList.ToList(), usePercentages, deckSize));
                newThread.Start();
            }
            else
            {
                MessageBox.Show("Please add an identity to the deck before attempting a predict");
            }
        }

        /// <summary>
        /// Creates, populates and displays a FormDecklist
        /// </summary>
        /// <param name="cardList">The list of cards to populate with</param>
        /// <param name="message">Any relevant error message</param>
        private void _showDecklist(List<Card> cardList, string message)
        {
            FormDecklist newForm = new FormDecklist();
            List<Card> originalDecklist = _currentDeckList.ToList();
            newForm.SetText(GetDeckString(ref cardList, ref originalDecklist) + "\r\n" + message);
            newForm.ShowDialog();

            this.EnableDisableControls(true);
        }

        /// <summary>
        /// (Invoke?) Creates, populates and displays a FormDecklist
        /// </summary>
        /// <param name="cardList">The list of cards to populate with</param>
        /// <param name="message">Any relevant error message</param>
        private void ShowDecklist(List<Card> cardList, string message)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(showDecklistDelegate, new object[] { cardList, message });
            }
            else
            {
                _showDecklist(cardList, message);
            }
        }

        /// <summary>
        /// The first gen of prediction algorithm (Prediction Method: Best Guess)
        /// </summary>
        /// <param name="observedCards">Cards already observed in the decklist</param>
        /// <param name="usePercentages">Whether we should consider n-Sets by magnitude or by percentage of all n-Sets</param>
        /// <param name="deckSize">The deck size we need to predict</param>
        /// <param name="message">Out for any error message that may occur</param>
        /// <returns>A list of cards representing the predicted deck</returns>
        private List<Card> PredictDeck_Default(List<Card> observedCards, bool usePercentages, int deckSize, out string message)
        {
            List<Card> predictedDeck = new List<Card>();
            message = string.Empty;
            predictedDeck.AddRange(observedCards);
            var predictions = GetPredictions(observedCards, new List<Card>(), usePercentages).OrderByDescending(x => x.Value);

            //If we're corp, add agendas first
            Card identity = predictedDeck.Single(x => x.CardType == Card.CardTypeEnum.Identity);

            if(identity.IsCorp)
            {
                int[] requiredAgendaPoints = Decklist.GetRequiredAgendaPoints(deckSize);
                int currentAgendaPoints = Decklist.GetAgendaPoints(observedCards);
                int neededAgendaPoints = requiredAgendaPoints[0] - currentAgendaPoints;

                if(currentAgendaPoints > requiredAgendaPoints[1])
                {
                    //The deck already has too many agendas!
                    message = "This deck contains too many agenda points for the specified deck size, could not complete deck.";
                    return predictedDeck;
                }

                while(neededAgendaPoints > 0)
                {
                    UpdateStatus(string.Format("Adding card {0}/{1}", predictedDeck.Count(), deckSize));
                    List<int> allowedCards = GetAllowedCards(predictedDeck);
                    Card newAgenda = predictions.Select(x => x.Key).Where(x => (x.AgendaPoints > 0) && (x.AgendaPoints < (neededAgendaPoints + 1))).First(x => allowedCards.Contains(x.ID));
                    predictedDeck.Add(newAgenda);
                    neededAgendaPoints -= newAgenda.AgendaPoints;
                }
            }

            while (predictedDeck.Count < (deckSize+1)) // Includes Identity
            {
                UpdateStatus(string.Format("Adding card {0}/{1}", predictedDeck.Count(), deckSize));
                List<int> allowedCards = GetAllowedCards(predictedDeck);
                predictedDeck.Add(predictions.Where(x => x.Key.AgendaPoints == 0).Select(x => x.Key).First(x => allowedCards.Contains(x.ID))); //Ensure we don't add any more Agendas
            }

            return predictedDeck;
        }

        /// <summary>
        /// The second gen of prediction algorithm (Prediction Method: Card Multiplicity)
        /// </summary>
        /// <param name="observedCards">Cards already observed in the decklist</param>
        /// <param name="usePercentages">Whether we should consider n-Sets by magnitude or by percentage of all n-Sets</param>
        /// <param name="deckSize">The deck size we need to predict</param>
        /// <param name="message">Out for any error message that may occur</param>
        /// <returns>A list of cards representing the predicted deck</returns>
        private List<Card> PredictDeck_CardMultiplicity(List<Card> observedCards, bool usePercentages, int deckSize, out string message)
        {
            List<Card> predictedDeck = new List<Card>();
            message = string.Empty;
            predictedDeck.AddRange(observedCards);
            List<Card> bannedCards = new List<Card>();
            Card identity = predictedDeck.Single(x => x.CardType == Card.CardTypeEnum.Identity);

            var predictions = GetPredictions(predictedDeck, bannedCards, usePercentages).OrderByDescending(x => x.Value);

            //Fix agenda points first if we're a corp
            if(identity.IsCorp)
            {
                int[] requiredAgendaPoints = Decklist.GetRequiredAgendaPoints(deckSize);
                int currentAgendaPoints = Decklist.GetAgendaPoints(observedCards);
                int neededAgendaPoints = requiredAgendaPoints[0] - currentAgendaPoints;

                if(currentAgendaPoints > requiredAgendaPoints[1])
                {
                    //The deck already has too many agendas!
                    message = "This deck contains too many agenda points for the specified deck size, could not complete deck.";
                    return predictedDeck;
                }

                while(neededAgendaPoints > 0)
                {
                    UpdateStatus(string.Format("Adding card {0}/{1}", predictedDeck.Count()-1, deckSize));
                    List<int> allowedCards = GetAllowedCards(predictedDeck);
                    Card newAgenda = predictions.Select(x => x.Key).Where(x => (x.AgendaPoints > 0) && (x.AgendaPoints < (neededAgendaPoints + 1))).First(x => allowedCards.Contains(x.ID));
                    predictedDeck.Add(newAgenda);
                    neededAgendaPoints -= newAgenda.AgendaPoints;
                }
            }

            //Foreach card ID in the deck that has a count fewer than MAX, see if we think there should be more of those cards
            foreach(var cardGrouping in predictedDeck.GroupBy(x => x.ID).Where(x => (x.First().CardType != Card.CardTypeEnum.Identity) && (x.First().AgendaPoints == 0) && (x.Count() < x.First().MaxNumPerDeck)).ToList()) //The ToList here is required so we don't screw up the iterator by modifying the underlying set
            {
                Card card = cardGrouping.First();

                //Make some educated guess based on the multiplicity of the card as to whether we should add another one.
                int numCardsToAdd = ((int)Math.Round(card.Multiplicity)) - predictedDeck.Count(x => x.ID == card.ID);

                for(int i = 0; i < numCardsToAdd; i++)
                {
                    predictedDeck.Add(card);
                }
            }

            //Add all current cards to the banned list so we don't consider adding them again
            bannedCards.AddRange(predictedDeck.Distinct());

            //Fill the rest of the deck out
            while(predictedDeck.Count() < (deckSize+1)) //include identity
            {
                //Add a card that isn't on the banned list
                Card newPrediction = predictions.Where(x => !bannedCards.Contains(x.Key) && (x.Key.AgendaPoints == 0)).First().Key;

                //Check for multiplicity of that card
                int numCardsToAdd = ((int)Math.Round(newPrediction.Multiplicity)) - predictedDeck.Count(x => x.ID == newPrediction.ID);

                //Make sure this doesn't cause us to exceed the deck count
                if(numCardsToAdd > ((deckSize + 1) - predictedDeck.Count()))
                {
                    numCardsToAdd = ((deckSize + 1) - predictedDeck.Count());
                }

                int influenceCost = (newPrediction.CostsInfluence(identity.Faction)) ? newPrediction.Influence : 0;
                int remainingInfluence = 100; //Arbitrarily large number

                if(influenceCost > 0)
                {
                    //Only need to do this math if the card costs influence.
                    remainingInfluence = Decklist.GetRemainingInfluence(identity, predictedDeck);
                }

                while((numCardsToAdd > 0) && (remainingInfluence >= influenceCost))
                {
                    UpdateStatus(string.Format("Adding card {0}/{1}", predictedDeck.Count()-1, deckSize));
                    predictedDeck.Add(newPrediction);
                    remainingInfluence -= influenceCost;
                    numCardsToAdd--;
                }

                //Add it to the banned list if not already there (just prevents dupes)
                if (!bannedCards.Contains(newPrediction))
                {
                    bannedCards.Add(newPrediction);
                }
            }

            return predictedDeck;
        }

        /// <summary>
        /// The third gen of prediction algorithm (Prediction Method: Influence Prioritised)
        /// </summary>
        /// <param name="observedCards">Cards already observed in the decklist</param>
        /// <param name="usePercentages">Whether we should consider n-Sets by magnitude or by percentage of all n-Sets</param>
        /// <param name="deckSize">The deck size we need to predict</param>
        /// <param name="message">Out for any error message that may occur</param>
        /// <returns>A list of cards representing the predicted deck</returns>
        private List<Card> PredictDeck_InfluencePrioritised(List<Card> observedCards, bool usePercentages, int deckSize, out string message)
        {
            List<Card> predictedDeck = new List<Card>();
            message = string.Empty;
            predictedDeck.AddRange(observedCards);
            List<Card> bannedCards = new List<Card>();
            Card identity = predictedDeck.Single(x => x.CardType == Card.CardTypeEnum.Identity);

            var predictions = GetPredictions(predictedDeck, bannedCards, usePercentages).OrderByDescending(x => x.Value);

            //Fix agenda points first if we're a corp
            if (identity.IsCorp)
            {
                int[] requiredAgendaPoints = Decklist.GetRequiredAgendaPoints(deckSize);
                int currentAgendaPoints = Decklist.GetAgendaPoints(observedCards);
                int neededAgendaPoints = requiredAgendaPoints[0] - currentAgendaPoints;

                if (currentAgendaPoints > requiredAgendaPoints[1])
                {
                    //The deck already has too many agendas!
                    message = "This deck contains too many agenda points for the specified deck size, could not complete deck.";
                    return predictedDeck;
                }

                while (neededAgendaPoints > 0)
                {
                    UpdateStatus(string.Format("Adding card {0}/{1}", predictedDeck.Count()-1, deckSize));
                    List<int> allowedCards = GetAllowedCards(predictedDeck);
                    Card newAgenda = predictions.Select(x => x.Key).Where(x => (x.AgendaPoints > 0) && (x.AgendaPoints < (neededAgendaPoints + 1))).First(x => allowedCards.Contains(x.ID));
                    predictedDeck.Add(newAgenda);
                    neededAgendaPoints -= newAgenda.AgendaPoints;
                }
            }

            //Foreach card ID in the deck that has a count fewer than MAX, see if we think there should be more of those cards
            foreach (var cardGrouping in predictedDeck.GroupBy(x => x.ID).Where(x => (x.First().CardType != Card.CardTypeEnum.Identity) && (x.First().AgendaPoints == 0) && (x.Count() < x.First().MaxNumPerDeck)).ToList()) //The ToList here is required so we don't screw up the iterator by modifying the underlying set
            {
                Card card = cardGrouping.First();

                //Make some educated guess based on the multiplicity of the card as to whether we should add another one.
                int numCardsToAdd = ((int)Math.Round(card.Multiplicity)) - predictedDeck.Count(x => x.ID == card.ID);

                for (int i = 0; i < numCardsToAdd; i++)
                {
                    predictedDeck.Add(card);
                }
            }

            //Add all current cards to the banned list so we don't consider adding them again
            bannedCards.AddRange(predictedDeck.Distinct());
            int remainingInfluence = Decklist.GetRemainingInfluence(identity, predictedDeck);

            //Fill the rest of the deck out
            while (predictedDeck.Count() < (deckSize + 1)) //include identity
            {
                Card newPrediction = null;

                //Prioritise spending influence
                if (remainingInfluence >= 2)
                {
                    //Try to find a card we can spend our influence on.
                    newPrediction = predictions.Where(x => (x.Key.CostsInfluence(identity.Faction) && (x.Key.Influence < remainingInfluence)) && !bannedCards.Contains(x.Key) && (x.Key.AgendaPoints == 0)).First().Key;
                }

                if(newPrediction == null)
                {
                    //Couldn't find a good influence spend, so bung in a different card instead
                    newPrediction = predictions.Where(x => !bannedCards.Contains(x.Key) && (x.Key.AgendaPoints == 0)).First().Key;
                }

                //Add a card that isn't on the banned list

                //Check for multiplicity of that card
                int numCardsToAdd = ((int)Math.Round(newPrediction.Multiplicity)) - predictedDeck.Count(x => x.ID == newPrediction.ID);

                //Make sure this doesn't cause us to exceed the deck count
                if (numCardsToAdd > ((deckSize + 1) - predictedDeck.Count()))
                {
                    numCardsToAdd = ((deckSize + 1) - predictedDeck.Count());
                }

                int influenceCost = (newPrediction.CostsInfluence(identity.Faction)) ? newPrediction.Influence : 0;

                while ((numCardsToAdd > 0) && (remainingInfluence >= influenceCost))
                {
                    UpdateStatus(string.Format("Adding card {0}/{1}", predictedDeck.Count()-1, deckSize));
                    predictedDeck.Add(newPrediction);
                    remainingInfluence -= influenceCost;
                    numCardsToAdd--;
                }

                //Add it to the banned list if not already there (just prevents dupes)
                if (!bannedCards.Contains(newPrediction))
                {
                    bannedCards.Add(newPrediction);
                }
            }

            return predictedDeck;
        }

        /// <summary>
        /// The fourth gen of prediction algorithm (Prediction Method: Influence Filtered)
        /// </summary>
        /// <param name="observedCards">Cards already observed in the decklist</param>
        /// <param name="usePercentages">Whether we should consider n-Sets by magnitude or by percentage of all n-Sets</param>
        /// <param name="deckSize">The deck size we need to predict</param>
        /// <param name="message">Out for any error message that may occur</param>
        /// <returns>A list of cards representing the predicted deck</returns>
        private List<Card> PredictDeck_InfluenceFiltered(List<Card> observedCards, bool usePercentages, int deckSize, out string message)
        {
            List<Card> predictedDeck = new List<Card>();
            message = string.Empty;
            predictedDeck.AddRange(observedCards);
            List<Card> bannedCards = new List<Card>();
            Card identity = predictedDeck.Single(x => x.CardType == Card.CardTypeEnum.Identity);

            //Only use n-sets that weren't splashes for the Agenda fill
            var predictions = GetPredictions(predictedDeck, bannedCards, usePercentages, false).OrderByDescending(x => x.Value);

            //Fix agenda points first if we're a corp
            if (identity.IsCorp)
            {
                int[] requiredAgendaPoints = Decklist.GetRequiredAgendaPoints(deckSize);
                int currentAgendaPoints = Decklist.GetAgendaPoints(observedCards);
                int neededAgendaPoints = requiredAgendaPoints[0] - currentAgendaPoints;

                if (currentAgendaPoints > requiredAgendaPoints[1])
                {
                    //The deck already has too many agendas!
                    message = "This deck contains too many agenda points for the specified deck size, could not complete deck.";
                    return predictedDeck;
                }

                while (neededAgendaPoints > 0)
                {
                    UpdateStatus(string.Format("Adding card {0}/{1}", predictedDeck.Count() - 1, deckSize));
                    List<int> allowedCards = GetAllowedCards(predictedDeck);
                    Card newAgenda = predictions.Select(x => x.Key).Where(x => (x.AgendaPoints > 0) && (x.AgendaPoints < (neededAgendaPoints + 1))).First(x => allowedCards.Contains(x.ID));
                    predictedDeck.Add(newAgenda);
                    neededAgendaPoints -= newAgenda.AgendaPoints;
                }
            }

            //Foreach card ID in the deck that has a count fewer than MAX, see if we think there should be more of those cards
            foreach (var cardGrouping in predictedDeck.GroupBy(x => x.ID).Where(x => (x.First().CardType != Card.CardTypeEnum.Identity) && (x.First().AgendaPoints == 0) && (x.Count() < x.First().MaxNumPerDeck)).ToList()) //The ToList here is required so we don't screw up the iterator by modifying the underlying set
            {
                Card card = cardGrouping.First();

                //Make some educated guess based on the multiplicity of the card as to whether we should add another one.
                int numCardsToAdd = ((int)Math.Round(card.Multiplicity)) - predictedDeck.Count(x => x.ID == card.ID);

                for (int i = 0; i < numCardsToAdd; i++)
                {
                    predictedDeck.Add(card);
                }
            }

            //Add all current cards to the banned list so we don't consider adding them again
            bannedCards.AddRange(predictedDeck);
            bannedCards = bannedCards.Distinct().ToList();
            int remainingInfluence = Decklist.GetRemainingInfluence(identity, predictedDeck);
            bool firstNonInfCard = true;

            //We'll do splashes first - Only use n-sets that are based on splashes for filling influence
            predictions = GetPredictions(predictedDeck, bannedCards, usePercentages, true).OrderByDescending(x => x.Value);

            //Fill the rest of the deck out
            while (predictedDeck.Count() < (deckSize + 1)) //include identity
            {
                Card newPrediction = null;

                //Prioritise spending influence
                if (remainingInfluence >= 2)
                {
#if DEBUG
                    var DEBUG_predicitions = predictions.ToDictionary(x => x.Key);
#endif
                    //Try to find a card we can spend our influence on.
                    newPrediction = predictions.Where(x => (x.Key.CostsInfluence(identity.Faction) && (x.Key.Influence < remainingInfluence)) && !bannedCards.Contains(x.Key) && (x.Key.AgendaPoints == 0)).First().Key;
                }

                if (newPrediction == null)
                {
                    //A one off refresh of predictions because we're done added influence splashes now
                    if(firstNonInfCard)
                    {
                        //Regenerate predictions from non-splash n-sets
                        predictions = GetPredictions(predictedDeck, bannedCards, usePercentages, false).OrderByDescending(x => x.Value);
                        remainingInfluence = 0; //Hard set this to 0 so we don't even try to splash anymore
                        firstNonInfCard = false;//Don't run this code block again, as we shouldn't need to.
                    }


                    //Couldn't find a good influence spend, so bung in a different card instead
                    newPrediction = predictions.Where(x => !bannedCards.Contains(x.Key) && (x.Key.AgendaPoints == 0)).First().Key;
                }

                //Add a card that isn't on the banned list

                //Check for multiplicity of that card
                int numCardsToAdd = ((int)Math.Round(newPrediction.Multiplicity)) - predictedDeck.Count(x => x.ID == newPrediction.ID);

                //Make sure this doesn't cause us to exceed the deck count
                if (numCardsToAdd > ((deckSize + 1) - predictedDeck.Count()))
                {
                    numCardsToAdd = ((deckSize + 1) - predictedDeck.Count());
                }

                int influenceCost = (newPrediction.CostsInfluence(identity.Faction)) ? newPrediction.Influence : 0;

                while ((numCardsToAdd > 0) && (remainingInfluence >= influenceCost))
                {
                    UpdateStatus(string.Format("Adding card {0}/{1}", predictedDeck.Count() - 1, deckSize));
                    predictedDeck.Add(newPrediction);
                    remainingInfluence -= influenceCost;
                    numCardsToAdd--;
                }

                //Add it to the banned list if not already there (just prevents dupes)
                if (!bannedCards.Contains(newPrediction))
                {
                    bannedCards.Add(newPrediction);
                }
            }

            return predictedDeck;
        }

        /// <summary>
        /// Switch method for PredictionMode
        /// </summary>
        /// <param name="mode">The prediction mode to use</param>
        /// <param name="observedCards">Cards already observed in the decklist</param>
        /// <param name="usePercentages">Whether we should consider n-Sets by magnitude or by percentage of all n-Sets</param>
        /// <param name="deckSize">The deck size we need to predict</param>
        /// <param name="message">Out for any error message that may occur</param>
        /// <returns>A list of cards representing the predicted deck</returns>
        private void PredictDeck(DeckPredictionModeEnum mode, List<Card> observedCards, bool usePercentages, int deckSize)
        {
            List<Card> predictedDeck = null;
            string message = string.Empty;

            switch (mode)
            {
                case DeckPredictionModeEnum.PREDICTION_MODE_BEST_GUESS:
                    predictedDeck = PredictDeck_Default(observedCards, usePercentages, deckSize, out message);
                    break;
                case DeckPredictionModeEnum.PREDICTION_MODE_CARD_MULTIPLICITY:
                    predictedDeck = PredictDeck_CardMultiplicity(observedCards, usePercentages, deckSize, out message);
                    break;
                case DeckPredictionModeEnum.PREDICTION_MODE_INFLUENCE_PRIORITISED:
                    predictedDeck = PredictDeck_InfluencePrioritised(observedCards, usePercentages, deckSize, out message);
                    break;
                case DeckPredictionModeEnum.PREDICTION_MODE_INFLUENCE_FILTERED:
                    predictedDeck = PredictDeck_InfluenceFiltered(observedCards, usePercentages, deckSize, out message);
                    break;
                default:
                    throw new NotImplementedException("Unknown Prediction Method");
            }

            ShowDecklist(predictedDeck, message);
        }

        /// <summary>
        /// Gets a string representing the content of a decklist
        /// </summary>
        /// <param name="cardlist">The cards in the decklist</param>
        /// <param name="initialList">The observed cards in the decklist</param>
        /// <returns>A string representing the content of a decklist</returns>
        private string GetDeckString(ref List<Card> cardlist, ref List<Card> initialList)
        {
            StringBuilder sb = new StringBuilder();
            Card identity = cardlist.Single(x => x.CardType == Card.CardTypeEnum.Identity);
            int influenceSpend = cardlist.Where(x => (x.CardType != Card.CardTypeEnum.Identity) && (x.CostsInfluence(identity.Faction))).Sum(x => x.Influence);

            //Add the identity
            sb.AppendLine(GetDecklistLine(identity, identity, 1, 1));

            foreach (var cardGroup in cardlist.Where(x => x.CardType != Card.CardTypeEnum.Identity).GroupBy(x => x.ID).OrderBy(x => x.First().Name))
            {
                sb.AppendLine(GetDecklistLine(identity, cardGroup.First(), cardGroup.Count(), _currentDeckList.Count(x => x.ID == cardGroup.Key)));
            }

            sb.AppendLine();
            if (identity.IsCorp)
            {
                int[] reqAgendaPoints = Decklist.GetRequiredAgendaPoints(cardlist.Count() - 1);
                sb.AppendLine(string.Format("Cards: {0}, Influence Spent: {1}/{2}, Agenda Points: {3}/[{4}-{5}]", new object[] { cardlist.Count(x => x.CardType != Card.CardTypeEnum.Identity), influenceSpend, identity.Influence, Decklist.GetAgendaPoints(cardlist), reqAgendaPoints[0], reqAgendaPoints[1] }));
            }
            else
            {
                sb.AppendLine(string.Format("Cards: {0}, Influence Spent: {1}/{2}", new object[] { cardlist.Count(x => x.CardType != Card.CardTypeEnum.Identity), influenceSpend, identity.Influence }));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Helper function used by GetDeckString(ref string, ref List<Card>)
        /// </summary>
        /// <param name="identity">The deck's identity card</param>
        /// <param name="currentCard">The card to write this line for</param>
        /// <param name="numPresent">The number of currentCard in the deck</param>
        /// <param name="numObserved">The number of currentCard observed in the deck</param>
        /// <returns></returns>
        private static string GetDecklistLine(Card identity, Card currentCard, int numPresent, int numObserved)
        {
            string line = string.Empty;

            if (identity == currentCard)
            {
                line = currentCard.Name;
            }
            else
            {
                //It's not an identity, so write the quantity
                line = string.Format("{0} x {1}", numPresent, currentCard.Name);
            }

            if (currentCard.CostsInfluence(identity.Faction))
            {
                //Do Latex formatting stuff to show bulletpoints
                line += " (*@$";
                for (int i = 0; i < currentCard.Influence; i++)
                {
                    line += "\\bullet";
                }

                line += "$@*)";
            }

            if (numObserved > 0)
            {
                //Some are observed, so mark this with squarebrackets
                line += string.Format(" [{0}]", numObserved);
            }
            return line;
        }

        /// <summary>
        /// Menu Item to scrape card data
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cardDataScrapeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DialogResult res = MessageBox.Show("Running data scrape operations will overwrite the shipped data included in this app. Not recommended!", "WARNING: Overwriting Data!", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);

            if (res == System.Windows.Forms.DialogResult.OK)
            {
                EnableDisableControls(false);
                Thread newT = new Thread(new ThreadStart(CardDataScrape));
                newT.Start();
            }
        }

        /// <summary>
        /// Menu item to scrape deck data
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void deckDataScapeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DialogResult res = MessageBox.Show("Running data scrape operations will overwrite the shipped data included in this app. Not recommended!", "WARNING: Overwriting Data!", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);

            if (res == System.Windows.Forms.DialogResult.OK)
            {
                EnableDisableControls(false);
                Thread newT = new Thread(new ThreadStart(DeckDataScrape));
                newT.Start();
            }
        }
    }
}
