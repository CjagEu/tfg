using System.Collections.Generic;
using TradingMotion.SDKv2.Markets.Charts;
using TradingMotion.SDKv2.Markets.Orders;
using TradingMotion.SDKv2.Markets.Indicators.OverlapStudies;
using TradingMotion.SDKv2.Algorithms;
using TradingMotion.SDKv2.Algorithms.InputParameters;
using TradingMotion.SDKv2.Markets.Indicators.Momentum;

namespace aroon_stochastic_shorts
{
    /// <summary> 
    /// Aroon Stochastic Shorts Strategy
    /// </summary> 
    /// <remarks> 
    /// TODO
    /// </remarks> 
    public class aroon_stochastic_shorts : Strategy
    {

        /// <summary>
        /// Strategy required constructor
        /// </summary>
        /// <param Name="mainChart">The Chart over the Strategy will run</param>
        /// <param Name="secondaryCharts">Secondary charts that the Strategy can use</param>
        public aroon_stochastic_shorts(Chart mainChart, List<Chart> secondaryCharts)
            : base(mainChart, secondaryCharts)
        {

        }

        /// <summary>
        /// Strategy Name
        /// </summary>
        /// <returns>The complete name of the strategy</returns>
        public override string Name
        {
            get { return "Aroon Stochastic Shorts Strategy"; }
        }

        /// <summary>
        /// Security filter that ensures the OpenPosition will be closed at the end of the trading session.
        /// </summary>
        /// <returns>
        /// True if the opened position must be closed automatically on session's close, false otherwise
        /// </returns>
        public override bool ForceCloseIntradayPosition
        {
            get { return false; }
        }

        /// <summary>
        /// Security filter that sets a maximum open position level, and ensures that the strategy will never exceeds it
        /// </summary>
        /// <returns>
        /// The maximum opened lots allowed (any side)
        /// </returns>
        public override uint MaxOpenPosition
        {
            get { return 1; }
        }

        /// <summary>
        /// Flag that indicates if the strategy uses advanced Order management or standard
        /// </summary>
        /// <returns>
        /// True if strategy uses advanced Order management. 
        /// This means that the strategy uses the advanced methods (InsertOrder/CancelOrder/ModifyOrder) in opposite of the simple ones (Buy/Sell/ExitLong/ExitShort).
        /// </returns>
        public override bool UsesAdvancedOrderManagement
        {
            get { return true; }
        }

        /// <summary>
        /// Creates the set of exposed Parameters for the strategy
        /// </summary>
        /// <returns>The exposed Parameters collection</returns>
        public override InputParameterList SetInputParameters()
        {
            return new InputParameterList
            {
                new InputParameter("Aroon Period", 25),

                new InputParameter("K Line", 13),
                new InputParameter("D Line", 3),
                new InputParameter("Stochastic Upper Line", 80),
                new InputParameter("Stochastic Lower Line", 20),

                new InputParameter("Factor Multiplier", 4),
            };
        }

        /// <summary>
        /// Callback executed when the strategy starts executing. This is the right place
        /// to create the Indicators that the strategy will use.
        /// </summary>
        public override void OnInitialize()
        {
            log.Debug("AroonStochasticLongs onInitialize()");

            var indAroon = new AroonIndicator(Bars.Bars, (int)GetInputParameter("Aroon Period") * (int)GetInputParameter("Factor Multiplier"));

            var indStochastic = new StochasticIndicator(
                Bars.Bars,
                (int)GetInputParameter("K Line"),
                (int)GetInputParameter("D Line"),
                TradingMotion.SDKv2.Markets.Indicators.MovingAverageType.Sma,
                (int)GetInputParameter("D Line"),
                TradingMotion.SDKv2.Markets.Indicators.MovingAverageType.Sma
            );

            AddIndicator("Aroon", indAroon);
            AddIndicator("Stochastic", indStochastic);

        }

        /// <summary>
        /// Callback executed for every new Bar. This is the right place
        /// to check your Indicators/trading rules and place the orders accordingly.
        /// </summary>
        public override void OnNewBar()
        {
            var indAroon = (AroonIndicator)GetIndicator("Aroon");
            var indStochastic = (StochasticIndicator)GetIndicator("Stochastic");

            /* Estrategia conservadora:
             *      Filtro de tendencia: Linea Up de Aroon mayor que 75
             *      Trigger: Estocástico mayor que su Upper Line
             */
            if (GetOpenPosition() == 0)
            {
                /*if (indAroon.GetAroonUp()[0] >= 75)
                {
                    if (indStochastic.GetD()[0] < (int)GetInputParameter("Stochastic Lower Line") &&
                        indStochastic.GetD()[2] > indStochastic.GetD()[1] && indStochastic.GetD()[1] < indStochastic.GetD()[0])
                    {
                        Order buyOrder = new MarketOrder(OrderSide.Buy, 1, "Local minimum, open long");
                        this.InsertOrder(buyOrder);
                    }
                }*/

                if (indAroon.GetAroonUp()[0] >= 75)
                {
                    if (indStochastic.GetD()[0] > indStochastic.GetUpperLine()[0])
                    {
                        Order buyOrder = new MarketOrder(OrderSide.Buy, 1, "Trend confirmed, open long");
                        this.InsertOrder(buyOrder);
                    }
                }


            }
            else if (GetOpenPosition() != 0)
            {
                /*if (indStochastic.GetD()[2] < indStochastic.GetD()[1] && indStochastic.GetD()[1] > indStochastic.GetD()[0])
                {
                    Order sellOrder = new MarketOrder(OrderSide.Sell, 1, "Local maximum, close long");
                    this.InsertOrder(sellOrder);
                }*/

                if (indStochastic.GetD()[0] < 50 && indAroon.GetAroonDown()[0] >= 75)
                {
                    Order sellOrder = new MarketOrder(OrderSide.Sell, 1, "Trend ended, close long");
                    this.InsertOrder(sellOrder);
                }
            }
        }
    }
}
