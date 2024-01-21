using System.Collections.Generic;
using TradingMotion.SDKv2.Markets.Charts;
using TradingMotion.SDKv2.Markets.Orders;
using TradingMotion.SDKv2.Markets.Indicators.OverlapStudies;
using TradingMotion.SDKv2.Algorithms;
using TradingMotion.SDKv2.Algorithms.InputParameters;
using TradingMotion.SDKv2.Markets.Indicators.Momentum;

namespace aroon_shorts
{
    /// <summary> 
    /// TradingMotion SDK Golden Cross Strategy
    /// </summary> 
    /// <remarks> 
    /// The Golden Cross Strategy uses two moving averages, one with short period (called Fast) and the other with a longer period (called Slow).
    /// When the fast avg crosses the slow avg from below it is called the "Golden Cross" and it is considered as a signal for a following bullish trend.
    /// The strategy will open a Long position right after a "Golden Cross", and will go flat when the fast average crosses below the slow one.
    /// </remarks> 
    public class aroon_shorts : Strategy
    {
        Order buyOrder, sellOrder, stopLossOrder;
        bool canOpenPosition = false;
        bool canClosePosition = false;
        double stopLoss = 0D;
        double siguienteNivelStop = 0D;

        /// <summary>
        /// Strategy required constructor
        /// </summary>
        /// <param Name="mainChart">The Chart over the Strategy will run</param>
        /// <param Name="secondaryCharts">Secondary charts that the Strategy can use</param>
        public aroon_shorts(Chart mainChart, List<Chart> secondaryCharts)
            : base(mainChart, secondaryCharts)
        {

        }

        /// <summary>
        /// Strategy Name
        /// </summary>
        /// <returns>The complete name of the strategy</returns>
        public override string Name
        {
            get { return "Aroon Shorts Strategy"; }
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
        /// True if strategy uses advanced Order management. This means that the strategy uses the advanced methods (InsertOrder/CancelOrder/ModifyOrder) in opposite of the simple ones (Buy/Sell/ExitLong/ExitShort).
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

                //new InputParameter("Wait Window", 5),

                //new InputParameter("Porcentaje SL", -2D),
                //new InputParameter("Porcentaje TP", 5D),

                //new InputParameter("Ticks", 50),
            };
        }

        /// <summary>
        /// Callback executed when the strategy starts executing. This is the right place
        /// to create the Indicators that the strategy will use.
        /// </summary>
        public override void OnInitialize()
        {
            log.Debug("Aroon Shorts onInitialize()");

            var indAroon = new AroonIndicator(Bars.Bars, (int)GetInputParameter("Aroon Period"));

            AddIndicator("Aroon", indAroon);
        }

        /// <summary>
        /// Callback executed for every new Bar. This is the right place
        /// to check your Indicators/trading rules and place the orders accordingly.
        /// </summary>
        public override void OnNewBar()
        {
            var indAroon = (AroonIndicator)GetIndicator("Aroon");

            /* Condiciones de entrada:
             *      Línea Down > 80 durante N días.
             *      Línea Up < 30.   
             *      
             * Condiciones de salida:
             *      Línea Up > 80 durante N días.
             *      Línea Down < 30.
             */
            if (GetOpenPosition() == 0)
            {
                /* Si durante N días la línea Aroon Down se ha mantenido por encima de 80, abrir posición. */
                int counter = 0;
                for (int i = 3; i >= 1; i--)
                {
                    if (indAroon.GetAroonDown()[i] >= 80)
                    {
                        counter++;
                    }
                }
                if (counter == 3)
                {
                    canOpenPosition = true;
                }
                //if (canOpenPosition)
                //{
                //    buyOrder = new MarketOrder(OrderSide.Buy, 1, "Trend confirmed, open long");
                //    this.InsertOrder(buyOrder);
                //    canOpenPosition = false;
                //}
                if (canOpenPosition && indAroon.GetAroonDown()[0] >= 80 && indAroon.GetAroonUp()[0] <= 30)
                {
                    sellOrder = new MarketOrder(OrderSide.Sell, 1, "Trend confirmed, open short");
                    this.InsertOrder(sellOrder);
                    canOpenPosition = false;

                    //stopLoss = Math.Truncate(GetFilledOrders()[0].FillPrice - (GetFilledOrders()[0].FillPrice * ((int)GetInputParameter("Ticks") / 10000D)));
                    //stopLossOrder = new StopOrder(OrderSide.Sell, 1, stopLoss, "Saltó StopLoss inicial");
                    //this.InsertOrder(stopLossOrder);
                }
            }
            else if (GetOpenPosition() != 0)
            {
                /* Si durante N días la línea Aroon Up se ha mantenido por encima de 80, cerrar posición. */
                //for (int i = 3; i >= 1; i--)
                //{
                //    if (indAroon.GetAroonUp()[i] >= 80)
                //    {
                //        canClosePosition = true;
                //    }
                //}
                //if (canClosePosition && indAroon.GetAroonUp()[0] >= 80 && indAroon.GetAroonDown()[0] <= 30)
                //{
                //    buyOrder = new MarketOrder(OrderSide.Buy, 1, "Uptrend finished confirmed, close short");
                //    this.InsertOrder(buyOrder);
                //    canClosePosition = false;
                //}
                
            /* Condición directa para salir de shorts ya que el mercado cae rápido siempre.*/
            if (indAroon.GetAroonUp()[0] == 30)
                {
                    buyOrder = new MarketOrder(OrderSide.Buy, 1, "Uptrend finished confirmed, close short");
                    this.InsertOrder(buyOrder);
                }
            }
        }
    }
}
