using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

namespace OrderMatchineEngine
{
    public enum Operation
    {
        BUY,
        SELL,
        CANCEL,
        MODIFY,
        PRINT  
    }

    public enum OrderType
    {
        GFD,  // Good For Day
        IOC   // Insert Or Cancel 
    }

    public class Order
    {
        public string OrderId { get;  } // unique 
        public Operation Operation { get; set; }
        public OrderType OrderType { get; }
        public int Price { get; set; } // not a floating type based on spec
        public int Quantity { get; set; } // not a floating type based on spec

        public Order(string orderId, Operation operation, OrderType orderType, int price, int quantity)
        {
            OrderId = orderId;
            Operation = operation;
            OrderType = orderType;
            Price = price;
            Quantity = quantity;
        }
    }

    class Solution
    {
        private static OrderedDictionary BuyOrders = new OrderedDictionary();
        private static OrderedDictionary SellOrders = new OrderedDictionary();

        static void Main(string[] args)
        {
            string input;
            var inputList = new List<string>();

            while (!string.IsNullOrEmpty(input = Console.ReadLine()))
                inputList.Add(input.Trim());

            inputList.ForEach(t => ProcessOrder(new Queue<string>(t.Split())));
            Console.ReadLine();
        }

        private static void ProcessOrder(Queue<string> inputQueue)
        {
            var operation = GetOperation(inputQueue.Dequeue().ToUpper());

            switch (operation)
            {
                case Operation.BUY:
                case Operation.SELL:
                    {
                        var inputs = GetBuySellInputs(inputQueue);
                        CreateOrder(operation, inputs.Item1, inputs.Item2, inputs.Item3, inputs.Item4);
                        break;
                    }
                case Operation.MODIFY:
                    {
                        var inputs = GetModifyInputs(inputQueue);
                        ModifyOrder(inputs.Item1, inputs.Item2, inputs.Item3, inputs.Item4);
                        break;
                    }
                case Operation.CANCEL:
                    DeleteOrder(inputQueue.Dequeue());
                    break;
                case Operation.PRINT:
                    PrintOrders();
                    break;
            }
        }

        private static (OrderType, int, int, string) GetBuySellInputs(Queue<string> inputQueue)
        {
            if (inputQueue.Count < 3) // since we need to ignore orderId
                throw new ArgumentException("invalid number of arguments, Usage: Operation OrderType Price Quantity Id");

            var orderType = GetOrderType(inputQueue.Dequeue().ToUpper());

            if (!int.TryParse(inputQueue.Dequeue(), out int price))
                throw new ArgumentException("invalid price " + price);

            if (!int.TryParse(inputQueue.Dequeue(), out int quantity))
                throw new ArgumentException("invalid quantity " + quantity);
            
            var orderId = inputQueue.Count == 0 ? string.Empty : inputQueue.Dequeue();

            return (orderType, price, quantity, orderId);
        }

        private static (string, Operation, int, int) GetModifyInputs(Queue<string> inputQueue)
        {
            if (inputQueue.Count != 4)
                throw new ArgumentException("invalid number of arguments, Usage: MODIFY Id Operation Price Quantity");

            var orderId = inputQueue.Dequeue();

            var operation = GetOperation(inputQueue.Dequeue().ToUpper());

            if (!int.TryParse(inputQueue.Dequeue(), out int price))
                throw new ArgumentException("invalid price " + price);

            if (!int.TryParse(inputQueue.Dequeue(), out int quantity))
                throw new ArgumentException("invalid quantity " + quantity);

            return (orderId, operation, price, quantity);
        }

        private static Operation GetOperation(string operationString)
        {
            if (!Enum.IsDefined(typeof(Operation), operationString))
                throw new ArgumentException("invalid operation " + operationString);
            return (Operation)Enum.Parse(typeof(Operation), operationString);
        }

        private static OrderType GetOrderType(string orderTypeString)
        {
            if (!Enum.IsDefined(typeof(OrderType), orderTypeString))
                throw new ArgumentException("invalid ordertype " + orderTypeString);
            return (OrderType)Enum.Parse(typeof(OrderType), orderTypeString);
        }

        public static void CreateOrder(Operation operation, OrderType orderType, int price, int quantity, string orderId)
        {
            if (price <= 0 || quantity <= 0 || string.IsNullOrEmpty(orderId))
                return; // as per spec

            var isBuy = operation == Operation.BUY ? true : false;
            var order = new Order(orderId, operation, orderType, price, quantity);
            var offset = 0;
            var fillQuantity = 0;
            var matches = (isBuy ? SellOrders : BuyOrders)
                            .Values.Cast<Order>()
                            .Where(t => isBuy ? t.Price <= order.Price : t.Price >= order.Price)
                            .OrderByDescending(t => t.Price).ToList();

            if (matches.Any()) // found matching orders in the order book 
            {
                while (order.Quantity > 0)
                {
                    var match = matches.Skip(offset).FirstOrDefault();
                    if (match == null) break;

                    var remainder = order.Quantity - match.Quantity;

                    if (remainder > 0) // partial fill, modify incoming order with reduced qty 
                    {
                        fillQuantity = match.Quantity;
                        PrintMatch(order, match, fillQuantity);
                        DeleteOrder(match.OrderId); // because its fully filled
                        CreateOrder(order.Operation, order.OrderType, order.Price, remainder, order.OrderId); // create partial order
                    }
                    else if (remainder < 0) // full fill, but modify matched order with partial qty 
                    {
                        fillQuantity = order.Quantity;
                        PrintMatch(order, match, fillQuantity);
                        DeleteOrder(order.OrderId); // incoming order is fully matched, delete it
                        ModifyOrder(match.OrderId, match.Operation, match.Price, Math.Abs(remainder)); // modify qty to remainder
                    }
                    else // remainder == 0 i.e. perfect match, remove both orders
                    {
                        fillQuantity = order.Quantity;
                        PrintMatch(order, match, fillQuantity);
                        DeleteOrder(match.OrderId);
                        DeleteOrder(order.OrderId);
                    }

                    order.Quantity = 0;
                    offset++;
                }
            }
            else // no matching orders exit in the order book 
            {
                if (order.OrderType == OrderType.IOC)
                    return; // as per spec, fill or kill 

                (isBuy ? BuyOrders : SellOrders).Add(order.OrderId, order); // just add order to order book 
            }

        }

        private static void PrintMatch(Order order, Order match, int fillQuantity)
        {
            Console.WriteLine("Trade {0} {1} {2} {3} {4} {5}",
                            match.OrderId, match.Price, fillQuantity, order.OrderId, order.Price, fillQuantity);
        }

        public static void ModifyOrder(string orderId, Operation operation, int price, int quantity)
        {
            if (price <= 0 || quantity <= 0 || string.IsNullOrEmpty(orderId))
                return; // as per spec

            Order foundOrder;

            if (BuyOrders.Contains(orderId))
                foundOrder = BuyOrders[orderId] as Order;
            else if (SellOrders.Contains(orderId))
                foundOrder = SellOrders[orderId] as Order;
            else
                return; // not found

            if (foundOrder.OrderType == OrderType.IOC) // can't modify IOC orders
                return;

            DeleteOrder(orderId);
            CreateOrder(operation, foundOrder.OrderType, price, quantity, orderId);
        }

        public static void DeleteOrder(string orderId)
        {
            if (string.IsNullOrEmpty(orderId))
                return; // as per spec

            if (BuyOrders.Contains(orderId))
                BuyOrders.Remove(orderId);
            else if (SellOrders.Contains(orderId))
                SellOrders.Remove(orderId);
        }

        public static void PrintOrders()
        {
            Console.WriteLine("SELL:");

            var sellGroup = SellOrders.Values.Cast<Order>().GroupBy(t => t.Price)
                            .Select(t => new { Price = t.Key, Quantity = t.Sum(x => x.Quantity) })
                            .OrderByDescending(t => t.Price);

            foreach (var sells in sellGroup)
                Console.WriteLine("{0} {1}", sells.Price, sells.Quantity);

            Console.WriteLine("BUY:");

            var buyGroup = BuyOrders.Values.Cast<Order>().GroupBy(t => t.Price)
                            .Select(t => new { Price = t.Key, Quantity = t.Sum(x => x.Quantity) })
                            .OrderByDescending(t => t.Price);

            foreach (var buys in buyGroup)
                Console.WriteLine("{0} {1}", buys.Price, buys.Quantity);

            Console.WriteLine();
        }
    }
}
