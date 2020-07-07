using OkonkwoOandaV20.TradeLibrary.REST;
using System;
using OkonkwoOandaV20.Framework;
using OkonkwoOandaV20.TradeLibrary.REST.OrderRequest;
using System.Collections.Generic;
using System.Threading;
using static OkonkwoOandaV20.TradeLibrary.REST.Rest20;
using Money_Management;
using System.IO;

namespace Trade
{
    class Program
    {
        static LArray betArray = null;
        static void Main(string[] args)
        {
            SetApiCredentials();
            StartTransactionsStream();
            betArray = new LArray(4, (double)GetAccountAsync(AccountID).Result.balance, .1);

            while (0 < 1)
            {
                if (!Utilities.IsMarketHalted().Result)
                {
                    var units = (long)GetLotSize();
                    if (CalculateMargin(units) > (double)GetAccountAsync(AccountID).Result.balance * .75)
                    {
                        betArray = new LArray(4, (double)GetAccountAsync(AccountID).Result.balance, .1);
                        continue;
                    }
                    else
                    {
                        var order = new MarketOrderRequest(GetAccountInstrumentsAsync(AccountID, new AccountInstrumentsParameters() { instruments = new List<string>() { "EUR_USD" } }).Result[0]);
                        Thread.Sleep(10000);
                        var response = Rest20.PostOrderAsync(AccountID, order).Result;

                        while ((double)GetTradeAsync(AccountID, response.lastTransactionID).Result.unrealizedPL < betArray.CalcBet() && (double)GetTradeAsync(AccountID, response.lastTransactionID).Result.unrealizedPL > -betArray.CalcBet())
                        {
                            Thread.Sleep(1000);
                        }

                        if ((double)GetTradeAsync(AccountID, response.lastTransactionID).Result.unrealizedPL > betArray.CalcBet())
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
                        else
                        {
                            betArray.LoseTrade();
                        }
                    }
                }
                else
                {
                    Thread.Sleep(900000);
                    continue;
                }
            }

        }

        public static double GetLotSize()
        {
            var spread = GetSpread();
            var bet = betArray.CalcBet();

            while (spread > 10 || spread < 3)
            {
                Thread.Sleep(900000);
                spread = GetSpread();
            }

            var atr = GetHourlyATR();

            if (atr > 0)
            {
                return bet / (spread * 3.25);
            }
            else
            {
                return -(bet / (spread * 3.25));
            }
        }

        public static double GetHourlyATR()
        {
            var param = new InstrumentCandlesParameters() { price = "M", granularity = "H1", smooth = false, count = 14 };
            var candles = GetInstrumentCandlesAsync("EUR_USD", param).Result.ToArray();
            return (double)(candles[candles.Length - 2].mid.c - candles[0].mid.c);
        }

        public static double GetSpread()
        {
            var param = new PricingParameters() { instruments = new List<string>() { "EUR_USD" } };
            var price = GetPricingAsync(AccountID, param).Result[0];
            return (double)(price.asks[0].price - price.bids[0].price);
        }

        public static double CalculateMargin(long units)
        {
            var param = new PricingParameters { instruments = new List<string> { "EUR_USD" } };
            return (double)GetPricingAsync(AccountID, param).Result.ToArray()[0].bids.ToArray()[0].price * units * .02;  
        }

        public static string AccountID { get; set; }

        #region Transactions
        static void SetApiCredentials()
        {
            WriteNewLine("Setting your V20 credentials ...");

            AccountID = "101-001-14918521-001";
            var environment = EEnvironment.Practice;
            var token = "5814e7cef9981369aefcb5d1354ea38e-fdc8d9d6082a5abbb02a3eedca940252";

            Credentials.SetCredentials(environment, token, AccountID);

            WriteNewLine("Nice! Credentials are set.");
        }

        #region transactions stream
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
        #endregion

        static void WriteNewLine(string message)
        {
            Console.WriteLine($"\n{message}");
        }
        #endregion

    }
}
