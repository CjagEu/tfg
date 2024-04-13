using System.Collections.Generic;
using TradingMotion.SDKv2.Markets.Charts;
using TradingMotion.SDKv2.Markets.Orders;
using TradingMotion.SDKv2.Markets.Indicators.OverlapStudies;
using TradingMotion.SDKv2.Markets.Indicators.Momentum;
using TradingMotion.SDKv2.Algorithms;
using TradingMotion.SDKv2.Algorithms.InputParameters;
using System;

namespace stochastic_rsi_longs
{
    /// <summary> 
    /// TradingMotion SDK Golden Cross Strategy
    /// </summary> 
    /// <remarks> 
    /// The Golden Cross Strategy uses two moving averages, one with short period (called Fast) and the other with a longer period (called Slow).
    /// When the fast avg crosses the slow avg from below it is called the "Golden Cross" and it is considered as a signal for a following bullish trend.
    /// The strategy will open a Long position right after a "Golden Cross", and will go flat when the fast average crosses below the slow one.
    /// </remarks> 
    public class stochastic_rsi_longs : Strategy
    {
        Order buyOrder, sellOrder, StopOrder, takeProfitOrder;
        double stoplossInicial, takeprofitlevel;
        bool breakevenFlag;

        /// <summary>
        /// Strategy required constructor
        /// </summary>
        /// <param Name="mainChart">The Chart over the Strategy will run</param>
        /// <param Name="secondaryCharts">Secondary charts that the Strategy can use</param>
        public stochastic_rsi_longs(Chart mainChart, List<Chart> secondaryCharts)
            : base(mainChart, secondaryCharts)
        {

        }

        /// <summary>
        /// Strategy Name
        /// </summary>
        /// <returns>The complete name of the strategy</returns>
        public override string Name
        {
            get { return "Stochastic RSI Longs Strategy"; }
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
                new InputParameter("timePeriod", 14),
                new InputParameter("fastKPeriod", 4),
                new InputParameter("fastDPeriod", 4),
                new InputParameter("RSI LowerLine", 40),
                new InputParameter("RSI UpperLine", 90),

                new InputParameter("Filter Moving Average Period", 99),

                new InputParameter("Filter ADX Period", 14),
                new InputParameter("ADX Level", 20),
     
                new InputParameter("Stoploss Ticks", 2.0D),
                new InputParameter("Breakeven Ticks", 2.0D),

                new InputParameter("Ratio", 1.0D)
            };
        }

        /// <summary>
        /// Callback executed when the strategy starts executing. This is the right place
        /// to create the Indicators that the strategy will use.
        /// </summary>
        public override void OnInitialize()
        {
            log.Debug("StochasticRSILongs onInitialize()");

            var indStochasticRSI = new StochasticRSIIndicator(
                source: Bars.Bars, timePeriod: (int)GetInputParameter("timePeriod"),
                fastKPeriod: (int)GetInputParameter("fastKPeriod"), 
                fastDPeriod: (int)GetInputParameter("fastDPeriod"),
                fastDMAType: TradingMotion.SDKv2.Markets.Indicators.MovingAverageType.Sma);
            var indFilterSMA = new SMAIndicator(Bars.Close, (int)GetInputParameter("Filter Moving Average Period"));
            var indFilterADX = new ADXIndicator(source: Bars.Bars, timePeriod: (int)GetInputParameter("Filter ADX Period"));

            AddIndicator("Filter SMA", indFilterSMA);
            AddIndicator("StochasticRSI", indStochasticRSI);
            AddIndicator("Filter ADX", indFilterADX);
        }

        /// <summary>
        /// Callback executed for every new Bar. This is the right place
        /// to check your Indicators/trading rules and place the orders accordingly.
        /// </summary>
        public override void OnNewBar()
        {
            var indStochasticRSI = (StochasticRSIIndicator)GetIndicator("StochasticRSI");
            var indFilterSMA = (SMAIndicator)GetIndicator("Filter SMA");
            var indFilterADX = (ADXIndicator)GetIndicator("Filter ADX");

            if (GetOpenPosition() == 0)
            {
                // Filtro de SMA de tendencia
                if (indFilterSMA.GetAvSimple()[0] < Bars.Close[0])
                {
                    //Filtro de ADX de volatilidad (Creciente? y mayor que cierto nivel)
                    //if (indFilterADX.GetADX()[0] > (int)GetInputParameter("ADX Level"))
                    //{
                        // Trigger SRSI cruza RSI LowerLine
                        if (indStochasticRSI.GetD()[1] < (int)GetInputParameter("RSI LowerLine") && indStochasticRSI.GetD()[0] >= (int)GetInputParameter("RSI LowerLine"))
                        {
                            buyOrder = new MarketOrder(OrderSide.Buy, 1, "Trend confirmed, open long");
                            //stoplossInicial = Bars.Close[0] - (Bars.Close[0] * ((double)GetInputParameter("Stoploss Ticks") / 100));         //* GetMainChart().Symbol.TickSize;
               
                            this.InsertOrder(buyOrder);

                            stoplossInicial = buyOrder.FillPrice - (buyOrder.FillPrice * ((double)GetInputParameter("Stoploss Ticks") / 100));
                            //stoplossInicial = precioValido(indFilterSMA.GetAvSimple()[0]);
                            StopOrder = new StopOrder(OrderSide.Sell, 1, stoplossInicial, "StopLoss triggered");            
                            this.InsertOrder(StopOrder);

                            breakevenFlag = false;
                        }
                    //}
                }
            }
            else if (GetOpenPosition() != 0)
            {
                //                StopOrder.Price = indFilterSMA.GetAvSimple()[0];
                //                StopOrder.Label = "Precio toca Filter SMA";
                //                this.ModifyOrder(StopOrder);

                //Precio sube X%, stoplossinicial a BE
//                if (porcentajeMovimientoPrecio(buyOrder.FillPrice) > (double)GetInputParameter("Breakeven Ticks") && !breakevenFlag)
//                {
//                    StopOrder.Price = buyOrder.FillPrice + (GetMainChart().Symbol.TickSize * 100);
//                    StopOrder.Label = "Breakeven triggered ******************";
//                    this.ModifyOrder(StopOrder);
//                    breakevenFlag = true;
//                }
//                if (indStochasticRSI.GetD()[1] > (int)GetInputParameter("RSI UpperLine") && indStochasticRSI.GetD()[0] <= (int)GetInputParameter("RSI UpperLine"))
//                {
//                    this.CancelOrder(StopOrder);
//                    sellOrder = new MarketOrder(OrderSide.Sell, 1, "Trend ended, close long");
//                    this.InsertOrder(sellOrder);
//                }
                takeprofitlevel = precioValido(buyOrder.FillPrice + ((buyOrder.FillPrice - StopOrder.Price) * (double)GetInputParameter("Ratio"))); // RR 1:3 (por ejemplo)
                if (Bars.Close[0] >= takeprofitlevel)
                {
                    this.CancelOrder(StopOrder);
                    sellOrder = new MarketOrder(OrderSide.Sell, 1, "TakeProfit triggered RR 1:3");
                    this.InsertOrder(sellOrder);

                }
//                else if(indFilterADX.GetADX()[0] < (int)GetInputParameter("ADX Level"))
//                {
//                    this.CancelOrder(StopOrder);
//                    sellOrder = new MarketOrder(OrderSide.Sell, 1, "Poca volatilidad, se termina tendencia");
//                    this.InsertOrder(sellOrder);
//                }
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
        
        // Convierte el precio dado para que sea valido para el Symbol.
        protected double precioValido(double precio)
        {
            double resto = precio % GetMainChart().Symbol.TickSize;
            double precioValido = precio;
            if (resto != 0)
            {
                double ajuste = GetMainChart().Symbol.TickSize - resto;
                precioValido += ajuste;
            }
            return precioValido;
        }
    }
}
