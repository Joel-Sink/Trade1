using OkonkwoOandaV20.TradeLibrary.REST;
using System;
using OkonkwoOandaV20.Framework;
using OkonkwoOandaV20.TradeLibrary.REST.OrderRequest;
using System.Collections.Generic;
using System.Threading;
using static OkonkwoOandaV20.TradeLibrary.REST.Rest20;
using Money_Management;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;

namespace Trade
{
    class Program
    {
        static LArray betArray = null;
        static Dictionary<string, double> atr;
        static void Main(string[] args)
        {
            string lastpair = "";
            SetApiCredentials();
            StartTransactionsStream();

            #region CloseAllTrades
            if (GetOpenTradesAsync(AccountID).Result.Count != 0)
            {
                foreach (var i in GetOpenTradesAsync(AccountID).Result)
                {
                    try
                    {
                        Console.WriteLine(i.id);
                        CloseOrder(i.id).Wait();
                    }
                    catch
                    {
                    }
                }
            }
            #endregion


            #region CreateArray
            string[] fileresults;

            betArray = new LArray(4, (double)GetAccountAsync(AccountID).Result.balance, .1);

            try
            {
                fileresults = File.ReadAllLines(@"C:\Users\jsink\source\repos\Trade\Trade\betarray.txt");
                if (fileresults.Length > 0)
                {
                    betArray.Clear();

                    foreach (var i in fileresults)
                        betArray.Add(double.Parse(i));
                }
            }
            catch
            {
                
            }
            #endregion

            string pair;
            double bet;
            double units;
            long trade = 0;
            Stopwatch watch; 

            while (0 < 1)
            {
                try
                {
                    #region Numbers
                    while (0 < 1)
                    {
                        GetHourlyATR();
                        pair = atr.Where(i => !lastpair.Contains(i.Key)).Aggregate((l, r) => 
                        {
                            if (l.Value < 0 && r.Value > 0)
                            {
                                return -l.Value > r.Value ? l : r;
                            }
                            else if (l.Value > 0 && r.Value < 0)
                            {
                                return l.Value > -r.Value ? l : r;
                            }
                            else if (l.Value < 0 && r.Value < 0)
                            {
                                return -l.Value > -r.Value ? l : r;
                            }
                            else
                            {
                                return l.Value > r.Value ? l : r;
                            }
                        }).Key;
                        bet = betArray.CalcBet();
                        try
                        {
                            units = GetLotSize(pair, bet);
                        }
                        catch
                        {
                            Console.WriteLine("Checking For Smaller Spread");
                            lastpair += pair;
                            continue;
                        }

                        double margin = CalculateMargin((long)units, pair);

                        if (margin > (double)GetAccountAsync(AccountID).Result.balance * .75)
                        {
                            betArray = new LArray(4, (double)GetAccountAsync(AccountID).Result.balance, .1);
                            continue;
                        }
                        break;
                    }
                    lastpair = pair;
                    #endregion

                    #region OpenTrade
                    while (0 < 1)
                    {
                        try
                        {
                            trade = (long)PlaceOrder((long)units, pair).Result;
                            break;
                        }
                        catch
                        {
                            Console.WriteLine("Error!");
                            Thread.Sleep(600000000);
                        }
                    }
                    Console.WriteLine("units: " + units + " bet:" + betArray.CalcBet() + " risk:" + (GetSpread(pair) * 3.25) + " pair:" + pair);
                    watch = Stopwatch.StartNew();
                    #endregion

                    #region CloseTrade
                    while (0 < 1)
                    {
                        if ((double)GetTradeAsync(AccountID, trade).Result.unrealizedPL > betArray.CalcBet())
                        {
                            try
                            {
                                betArray.WinTrade();
                            }
                            catch
                            {
                                betArray = new LArray(4, (double)GetAccountAsync(AccountID).Result.balance, .1);
                            }
                        }
                        else if ((double)GetTradeAsync(AccountID, trade).Result.unrealizedPL < -betArray.CalcBet())
                        {
                            betArray.LoseTrade();
                        }
                        else
                        {
                            continue;
                        }
                        try
                        {
                            CloseOrder(trade).Wait();
                        }
                        catch
                        {
                            Console.WriteLine("Error!");
                            Thread.Sleep(600000000);
                        }
                        watch.Stop();
                        Console.WriteLine((double)GetTradeAsync(AccountID, trade).Result.realizedPL + " " + pair + " " + (watch.ElapsedMilliseconds / 60000));
                        break;
                    }
                    List<string> betarraynums = new List<string>();
                    foreach (var i in betArray)
                    {
                        betarraynums.Add(i.ToString());
                    }
                    File.WriteAllLines(@"C:\Users\jsink\source\repos\Trade\Trade\betarray.txt", betarraynums);
                }
                catch
                {
                    Console.WriteLine("Error!");
                    Thread.Sleep(600000000);
                }
                #endregion
            }
        }
        public static string AccountID { get; set; }

        #region Trade
        static async Task<long?> PlaceOrder(long units, string pair)
        {
            var instrument = (await GetAccountInstrumentsAsync(AccountID, 
                new AccountInstrumentsParameters { instruments = new List<string>() { pair } })).First();

            var request = new MarketOrderRequest(instrument)
            {
                units = units
            };

            PostOrderResponse response = null;
            
            response = await PostOrderAsync(AccountID, request);
            return response?.orderFillTransaction?.tradeOpened?.tradeID;
        }   

        static async Task<long?> CloseOrder(long trade)
        {
            var request = await PutTradeCloseAsync(AccountID, trade);

            return request.lastTransactionID;

        }
        #endregion

        #region PreTradeCalcs
        public static double GetLotSize(string pair, double bet)
        {
            var risk = GetSpread(pair);

            if ((risk > .001 && !pair.Contains("JPY")) || (risk > .1 && pair.Contains("JPY")))
            {
                throw new Exception();
            }
            if (atr[pair] > 0)
            {
                risk *= 3.25;
            }
            else
            {
                risk *= -3.25;
            }

            var basec = pair.Split('_')[0];

            if (basec.Equals("USD"))
            {
                var param = new PricingParameters() { instruments = new List<string>() { pair } };
                return bet * (double)GetPricingAsync(AccountID, param).Result[0].asks[0].price / risk;
            }
            else
            {
                var upair = GetAccountInstrumentsAsync(AccountID).Result.Where(i => i.name.Contains("USD") && i.name.Contains(basec)).ToList()[0];
                var param = new PricingParameters() { instruments = new List<string>() { pair } };
                var param1 = new PricingParameters() { instruments = new List<string>() { upair.name } };

                var price = (double)GetPricingAsync(AccountID, param).Result[0].asks[0].price;
                var price1 = (double)GetPricingAsync(AccountID, param1).Result[0].asks[0].price;

                var counter = upair.name.Split('_')[1];
                if (counter.Equals("USD"))
                {
                    return 1 / price1 * price * bet / risk;  
                }
                else
                {
                    return price1 * price * bet / risk;
                }

            }


        }

        public static void GetHourlyATR()
        {
            atr = new Dictionary<string, double>();
            foreach (var i in GetAccountInstrumentsAsync(AccountID).Result.Where(i => !i.name.Contains("HKD") 
            && !i.name.Contains("MXN") && !i.name.Contains("PLN") && !i.name.Contains("DKK") && !i.name.Contains("HUF") && !i.name.Contains("SEK") 
            && !i.name.Contains("CZK") && !i.name.Contains("TRY") && !i.name.Contains("NRK")))
            {
                var param = new InstrumentCandlesParameters() { price = "M", granularity = "H1", smooth = false, count = 14 };
                var candles = GetInstrumentCandlesAsync(i.name, param).Result.ToArray();
                var pairatr = (double)(candles[candles.Length - 2].mid.c - candles[0].mid.c);
                if (i.name.Contains("JPY"))
                {
                    pairatr /= 100;
                }

                atr.Add(i.name, pairatr);
            }
        }

        public static double GetSpread(string pair)
        {
            var param = new PricingParameters() { instruments = new List<string>() { pair } };
            var price = GetPricingAsync(AccountID, param).Result[0];
            return (double)(price.asks[0].price - price.bids[0].price);
        }

        public static double CalculateMargin(long units, string pair)
        {
            var marginreq = (double)GetAccountInstrumentsAsync(AccountID).Result.Where(i => i.name.Equals(pair)).ToList()[0].marginRate;

            var basec = pair.Split('_')[0];

            if (!basec.Equals("USD"))
            {
                var upair = GetAccountInstrumentsAsync(AccountID).Result.Where(i => i.name.Contains("USD") && i.name.Contains(basec)).ToList()[0];

                var counter = upair.name.Split('_')[1];

                if (counter.Equals("USD"))
                {
                    var param = new PricingParameters() { instruments = new List<string>() { upair.name } };
                    return (double)GetPricingAsync(AccountID, param).Result[0].asks[0].price * units * marginreq;
                }
                else
                {
                    var param = new PricingParameters() { instruments = new List<string>() { upair.name } };
                    return 1 / (double)GetPricingAsync(AccountID, param).Result[0].asks[0].price * units * marginreq;
                }
            }
            else
            {
                return units * marginreq;
            }

        }

        #endregion

        #region Transactions Stream
        static void SetApiCredentials()
        {
            WriteNewLine("Setting your V20 credentials ...");

            AccountID = "101-001-14918521-001";
            var environment = EEnvironment.Practice;
            var token = "5814e7cef9981369aefcb5d1354ea38e-fdc8d9d6082a5abbb02a3eedca940252";

            Credentials.SetCredentials(environment, token, AccountID);

            WriteNewLine("Nice! Credentials are set.");
        }

        static Semaphore _transactionReceived;
        static TransactionsSession _transactionsSession;

        static void StartTransactionsStream()
        {
            WriteNewLine("Starting transactions stream ...");

            _transactionsSession = new TransactionsSession(AccountID);
            _transactionReceived = new Semaphore(0, 100);
            _transactionsSession.DataReceived += OnTransactionReceived;

            _transactionsSession.StartSession();

            bool success = _transactionReceived.WaitOne(10000);

            if (success)
                WriteNewLine("Good news!. Transactions stream is functioning.");
            else
                WriteNewLine("Bad news!. Transactions stream is not functioning.");
        }

        protected static void OnTransactionReceived(TransactionsStreamResponse data)
        {
            if (!data.IsHeartbeat())
                WriteNewLine("V20 notification - New account transaction: " + data.transaction.type);

            _transactionReceived.Release();
        }

        static void StopTransactionsStream()
        {
            _transactionsSession.StopSession();
        }

        static void WriteNewLine(string message)
        {
            Console.WriteLine($"\n{message}");
        }
        #endregion

    }
}
