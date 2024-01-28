using System.Collections.Generic;
using TradingMotion.SDKv2.Markets.Charts;
using TradingMotion.SDKv2.Markets.Orders;
using TradingMotion.SDKv2.Markets.Indicators.OverlapStudies;
using TradingMotion.SDKv2.Algorithms;
using TradingMotion.SDKv2.Algorithms.InputParameters;
using System;
using TradingMotion.SDKv2.Markets.Indicators.Volatility;

namespace ma_shorts
{
    /// <summary> 
    /// TradingMotion SDK Golden Cross Strategy
    /// </summary> 
    /// <remarks> 
    /// The Golden Cross Strategy uses two moving averages, one with short period (called Fast) and the other with a longer period (called Slow).
    /// When the fast avg crosses the slow avg from below it is called the "Golden Cross" and it is considered as a signal for a following bullish trend.
    /// The strategy will open a Long position right after a "Golden Cross", and will go flat when the fast average crosses below the slow one.
    /// </remarks> 
    public class ma_shorts : Strategy
    {
        Order buyOrder, sellOrder, StopOrder, exitShortOrder;
        double stoplossInicial;
        bool breakevenFlag;
        int profitMultiplier;

        /// <summary>
        /// Strategy required constructor
        /// </summary>
        /// <param Name="mainChart">The Chart over the Strategy will run</param>
        /// <param Name="secondaryCharts">Secondary charts that the Strategy can use</param>
        public ma_shorts(Chart mainChart, List<Chart> secondaryCharts)
            : base(mainChart, secondaryCharts)
        {

        }

        /// <summary>
        /// Strategy Name
        /// </summary>
        /// <returns>The complete name of the strategy</returns>
        public override string Name
        {
            get { return "MA Shorts Strategy"; }
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
                new InputParameter("Long Moving Average Period", 50),
                new InputParameter("Slow Moving Average Period", 20),
                new InputParameter("Fast Moving Average Period", 5),
                new InputParameter("Stoploss Ticks", 0.50D),
        };
        }

        /// <summary>
        /// Callback executed when the strategy starts executing. This is the right place
        /// to create the Indicators that the strategy will use.
        /// </summary>
        public override void OnInitialize()
        {
            log.Debug("MA Shorts onInitialize()");

            var indLongSMA = new SMAIndicator(Bars.Close, (int)GetInputParameter("Long Moving Average Period"));
            var indSlowSMA = new SMAIndicator(Bars.Close, (int)GetInputParameter("Slow Moving Average Period"));
            var indFastSMA = new SMAIndicator(Bars.Close, (int)GetInputParameter("Fast Moving Average Period"));

            AddIndicator("Long SMA", indLongSMA);
            AddIndicator("Slow SMA", indSlowSMA);
            AddIndicator("Fast SMA", indFastSMA);
        }

        /// <summary>
        /// Callback executed for every new Bar. This is the right place
        /// to check your Indicators/trading rules and place the orders accordingly.
        /// </summary>
        public override void OnNewBar()
        {
            var indFastSma = (SMAIndicator)GetIndicator("Fast SMA");
            var indSlowSma = (SMAIndicator)GetIndicator("Slow SMA");
            var indLongSma = (SMAIndicator)GetIndicator("Long SMA");

            //if (GetOpenPosition() == 0)
            //{
            //    if (indFastSma.GetAvSimple()[1] < indSlowSma.GetAvSimple()[1] && indFastSma.GetAvSimple()[0] >= indSlowSma.GetAvSimple()[0])
            //    {
            //        buyOrder = new MarketOrder(OrderSide.Buy, 1, "Trend confirmed, open long");
            //        this.InsertOrder(buyOrder);
            //    }
            //}
            //else if(GetOpenPosition() != 0)
            //{
            //    if (indFastSma.GetAvSimple()[1] > indSlowSma.GetAvSimple()[1] && indFastSma.GetAvSimple()[0] <= indSlowSma.GetAvSimple()[0])
            //    {
            //        sellOrder = new MarketOrder(OrderSide.Sell, 1, "Trend ended, close long");
            //        this.InsertOrder(sellOrder);
            //    }
            //}

            if (GetOpenPosition() == 0)
            {
                if (indLongSma.GetAvSimple()[0] > indSlowSma.GetAvSimple()[0] && indSlowSma.GetAvSimple()[0] > indFastSma.GetAvSimple()[0])
                {
                    sellOrder = new MarketOrder(OrderSide.Sell, 1, "Trend confirmed, open short");
                    // trailingStopOrder = new StopOrder(OrderSide.Sell, 1, this.Bars.Close[0] - stopMargin, "Trailing stop long exit");

                    stoplossInicial = Bars.Close[0] + (Bars.Close[0] * ((double)GetInputParameter("Stoploss Ticks") / 100));                        //* GetMainChart().Symbol.TickSize; // TODO
                    StopOrder = new StopOrder(OrderSide.Buy, 1, stoplossInicial, "StopLoss triggered");

                    this.InsertOrder(sellOrder);
                    this.InsertOrder(StopOrder);

                    breakevenFlag = false;

                }
            }
            else if (GetOpenPosition() != 0)
            {
                //Precio sube 2%, stoplossinicial a BE
                if (porcentajeMovimientoPrecio(sellOrder.FillPrice) > (double)GetInputParameter("Stoploss Ticks") && !breakevenFlag)
                {
                    StopOrder.Price = sellOrder.FillPrice - (GetMainChart().Symbol.TickSize * 100);
                    StopOrder.Label = "Breakeven triggered ******************";
                    this.ModifyOrder(StopOrder);
                    breakevenFlag = true;
                    profitMultiplier = 2;
                }
                else
                {
                    //if (porcentajeMovimientoPrecio(StopOrder.Price) >= (double)GetInputParameter("Stoploss Ticks") * profitMultiplier)
                    //{
                    //    // Cancelling the order and closing the position
                    //    exitLongOrder = new MarketOrder(OrderSide.Sell, 1, "Exit long position");

                    //    this.InsertOrder(exitLongOrder);
                    //    this.CancelOrder(StopOrder);
                    //}
                    if (Bars.Close[0] >= indSlowSma.GetAvSimple()[0])
                    {
                        // Cancelling the order and closing the position
                        exitShortOrder = new MarketOrder(OrderSide.Buy, 1, "Exit short position");

                        this.InsertOrder(exitShortOrder);
                        this.CancelOrder(StopOrder);
                    }
                }
            }
        }


        // Devuelve en porcentaje cuánto se ha movido el precio desde la entrada.
        protected double porcentajeMovimientoPrecio(double precioOrigen)
        {
            double porcentaje = 0;

            // Calcular la variación porcentual del precio con respecto a la entrada.
            if (Bars.Close[0] > precioOrigen)
            {
                // Precio actual por encima del precio de entrada.
                porcentaje = ((Bars.Close[0] / precioOrigen) - 1) * 100;
            }
            else if (Bars.Close[0] < precioOrigen)
            {
                // Precio actual por debajo del precio de entrada.
                porcentaje = ((Bars.Close[0] / precioOrigen) - 1) * 100;
            }

            return porcentaje;
        }


        // Implementación de un trailing stop para la estrategia
        protected void ajustarStopLoss(double siguienteNivelStop)
        {
            /* Cálculo del siguiente nivel propuesto para StopLoss */
            siguienteNivelStop = StopOrder.Price + (StopOrder.Price * (int)GetInputParameter("Stoploss Ticks") / 100D);
            /* Si el precio avanza más de X "Ticks", muevo SL [Por ejemplo Ticks=50 -> 0.50% de subida] */
            if ((this.Bars.Close[0] / siguienteNivelStop) - 1 >= (int)GetInputParameter("Stoploss Ticks") / 100D)
            {
                StopOrder.Price = Math.Truncate(siguienteNivelStop);
                StopOrder.Label = "Saltó StopLoss desplazado";
                this.ModifyOrder(StopOrder);
            }
        }
    }
}
