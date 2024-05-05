using System.Collections.Generic;
using TradingMotion.SDKv2.Markets.Charts;
using TradingMotion.SDKv2.Markets.Orders;
using TradingMotion.SDKv2.Markets.Indicators.OverlapStudies;
using TradingMotion.SDKv2.Algorithms;
using TradingMotion.SDKv2.Algorithms.InputParameters;
using System;
using TradingMotion.SDKv2.Markets.Indicators.Momentum;

namespace macd_shorts
{
    /// <summary> 
    /// TradingMotion SDK Golden Cross Strategy
    /// </summary> 
    /// <remarks> 
    /// The Golden Cross Strategy uses two moving averages, one with short period (called Fast) and the other with a longer period (called Slow).
    /// When the fast avg crosses the slow avg from below it is called the "Golden Cross" and it is considered as a signal for a following bullish trend.
    /// The strategy will open a Long position right after a "Golden Cross", and will go flat when the fast average crosses below the slow one.
    /// </remarks> 
    public class macd_shorts : Strategy
    {
        Order buyOrder, sellOrder, StopOrder;
        double stoplossInicial, dineroPerdido, dineroGanado;

        /// <summary>
        /// Strategy required constructor
        /// </summary>
        /// <param Name="mainChart">The Chart over the Strategy will run</param>
        /// <param Name="secondaryCharts">Secondary charts that the Strategy can use</param>
        public macd_shorts(Chart mainChart, List<Chart> secondaryCharts)
            : base(mainChart, secondaryCharts)
        {

        }

        /// <summary>
        /// Strategy Name
        /// </summary>
        /// <returns>The complete name of the strategy</returns>
        public override string Name
        {
            get { return "MACD Shorts Strategy"; }
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
                new InputParameter("fastPeriod", 10),
                new InputParameter("slowPeriod", 15),
                new InputParameter("signalPeriod", 25),

                new InputParameter("Filter MA Period", 100),

                new InputParameter("Quantity SL", 3000),
                //new InputParameter("Quantity TP", 3000),
            };
        }

        /// <summary>
        /// Callback executed when the strategy starts executing. This is the right place
        /// to create the Indicators that the strategy will use.
        /// </summary>
        public override void OnInitialize()
        {
            log.Debug("MACDShortsStrategy onInitialize()");

            var indMACD = new MACDExtIndicator(
                Bars.Close,
                (int)GetInputParameter("fastPeriod"),
                TradingMotion.SDKv2.Markets.Indicators.MovingAverageType.Sma,
                (int)GetInputParameter("slowPeriod"),
                TradingMotion.SDKv2.Markets.Indicators.MovingAverageType.Sma,
                (int)GetInputParameter("signalPeriod"),
                TradingMotion.SDKv2.Markets.Indicators.MovingAverageType.Sma
                );
            ;

            var indFilterSMA = new SMAIndicator(Bars.Close, (int)GetInputParameter("Filter MA Period"));

            AddIndicator("Filter SMA", indFilterSMA);
            AddIndicator("MACD", indMACD);

            dineroGanado = 0;
            dineroPerdido = 0;
        }

        /// <summary>
        /// Callback executed for every new Bar. This is the right place
        /// to check your Indicators/trading rules and place the orders accordingly.
        /// </summary>
        public override void OnNewBar()
        {
            var indMACD = (MACDExtIndicator)GetIndicator("MACD");
            var indFilterSMA = (SMAIndicator)GetIndicator("Filter SMA");

            //imprimirOrdenStop();
            //imprimirOrdenShort();

            if (GetOpenPosition() == 0)
            {
                // Filtro de SMA
                if (indFilterSMA.GetAvSimple()[0] > Bars.Close[0])
                {
                    /* Cruce de MACD con Signal hacia abajo && que esté encima de 0 */
                    if (indMACD.GetSignalAverage()[0] > 0 && indMACD.GetMACD()[1] > indMACD.GetSignalAverage()[1] && indMACD.GetMACD()[0] <= indMACD.GetSignalAverage()[0])
                    {
                        sellOrder = new MarketOrder(OrderSide.Sell, 1, "Trend confirmed, open short");
                        InsertOrder(sellOrder);

                        stoplossInicial = precioValido(calcularNivelPrecioParaStopLoss(cantidadDinero: (int)GetInputParameter("Quantity SL")));
                        StopOrder = new StopOrder(OrderSide.Buy, 1, stoplossInicial, "StopLoss triggered");
                        InsertOrder(StopOrder);
                    }
                }
            }
            else if (GetOpenPosition() != 0)
            {
                if (indFilterSMA.GetAvSimple()[0] <= Bars.Close[0])
                {
                    CancelOrder(StopOrder);
                    buyOrder = new MarketOrder(OrderSide.Buy, 1, "Filter signal cancelled the short.");
                    InsertOrder(buyOrder);
                }
                //else if (activarTakeProfit())
                //{
                //    CancelOrder(StopOrder);
                //    buyOrder = new MarketOrder(OrderSide.Buy, 1, "TakeProfit reached, profit: " + precioValido((sellOrder.FillPrice - Bars.Close[0]) * 20).ToString());
                //    InsertOrder(buyOrder);
                //}
                else if (indMACD.GetSignalAverage()[0] < 0 && indMACD.GetMACD()[1] < indMACD.GetSignalAverage()[1] && indMACD.GetMACD()[0] >= indMACD.GetSignalAverage()[0])
                {
                        CancelOrder(StopOrder);
                        buyOrder = new MarketOrder(OrderSide.Buy, 1, "MACD Signal to close short: " + precioValido((sellOrder.FillPrice - Bars.Close[0]) * 20).ToString());
                        InsertOrder(buyOrder);
                }
            }
        }

        //*******************************************************************************************************************************************************//

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

        //Devuelve el nivel de precio al que se debe ejecutar una orden para que se pierda la cantidadDinero pasada como parámetro.
        protected double calcularNivelPrecioParaStopLoss(int cantidadDinero)
        {
            //TODO HACER ESTA FUNCIÓN QUE DEVUELVA EL NIVEL DEL PRECIO AL QUE SE DEBE COLOCAR LA ORDEN DE STOPLOSS PARA PERDER LA cantidadDinero PASA COMO PARÁMETRO (SERÁ UN INPUT PARAMETER LUEGO)
            // El argumento cantidadDinero debe ser en términos absolutos, es decir si digo de perder 1000, es 1000 no -1000.
            // Renombrar la 'i' y cambiar lo de devolver 0.
            for (double i = Bars.Close[0]; i > 0; i += 5)
            {
                if ((i - sellOrder.FillPrice) * 20 >= cantidadDinero)
                {
                    return i;
                }
            }
            return 0;
        }

        //TODO Devuelve el nivel de precio al que se debe ejecutar una orden para que se gane la cantidadDinero pasada como parámetro.
        protected bool activarTakeProfit()
        {
            if ((sellOrder.FillPrice - Bars.Close[0]) * 20 >= (int)GetInputParameter("Quantity TP"))
            {
                return true;
            }
            return false;
        }

        //Loggear si se ha ejecutado una orden Stop.
        protected void imprimirOrdenStop()
        {
            if (GetFilledOrders()[0] != null && GetFilledOrders()[0].Type == OrderType.Stop)
            {
                if (dineroPerdido != (GetFilledOrders()[0].FillPrice - sellOrder.FillPrice) * Symbol.PointValue)
                {
                    dineroPerdido = (GetFilledOrders()[0].FillPrice - sellOrder.FillPrice) * Symbol.PointValue;
                    log.Error("StopLoss  Ejecutado! Pierdo: " + Math.Truncate(dineroPerdido));
                }
            }
        }

        //Loggear si se ha ejectuado una orden Short
        protected void imprimirOrdenShort()
        {
            if (GetFilledOrders()[0] != null && GetFilledOrders()[0].Type == OrderType.Market && GetFilledOrders()[0].Side == OrderSide.Buy)
            {
                if (dineroGanado != (sellOrder.FillPrice - buyOrder.FillPrice) * Symbol.PointValue)
                {
                    dineroGanado = (sellOrder.FillPrice - buyOrder.FillPrice) * Symbol.PointValue;
                    if (buyOrder.Label == "Filter signal cancelled the short.")
                    {
                        if (dineroGanado > 0)
                        {
                            log.Warn("Filtro cancela short! Gano: " + Math.Truncate(dineroGanado));
                        }
                        else
                        {
                            log.Warn("Filtro cancela short!            Pierdo: " + Math.Truncate(dineroGanado));
                        }

                    }
                    else
                    {
                        log.Info("ShortOrder Ejecutada! Gano: " + Math.Truncate(dineroGanado));
                    }
                }
            }
        }
    }
}
