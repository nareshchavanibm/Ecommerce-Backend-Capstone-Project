using OrderService.Application.DTOs.Orders;
using OrderService.Application.Interfaces.Repositories;
using OrderService.Application.Interfaces.Services;
using OrderService.Domain.Entities;
using OrderService.Domain.Enums;

namespace OrderService.Application.Services
{
    public class OrderService : IOrderService
    {
        private readonly IOrderRepository _orderRepository;
        private readonly IOrderItemRepository _orderItemRepository;

        public OrderService(
            IOrderRepository orderRepository,
            IOrderItemRepository orderItemRepository)
        {
            _orderRepository = orderRepository;
            _orderItemRepository = orderItemRepository;
        }

        public async Task<OrderResponseDto> CreateOrderAsync(OrderCreateDto dto)
        {
            if (dto.Items == null || dto.Items.Count == 0)
            {
                throw new ArgumentException("Order must contain at least one item.");
            }

            var order = new Order
            {
                UserId = dto.UserId,
                Status = OrderStatus.Pending,
                TotalAmount = dto.Items.Sum(x => x.UnitPrice * x.Quantity)
            };

            var createdOrder = await _orderRepository.AddAsync(order);

            foreach (var item in dto.Items)
            {
                var orderItem = new OrderItem
                {
                    OrderId = createdOrder.Id,
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice
                };

                await _orderItemRepository.AddAsync(orderItem);
            }

            return await GetOrderByIdAsync(createdOrder.Id)
                   ?? throw new InvalidOperationException("Failed to retrieve created order.");
        }

        public async Task<OrderResponseDto?> GetOrderByIdAsync(int id)
        {
            var order = await _orderRepository.GetByIdAsync(id);

            if (order == null)
            {
                return null;
            }

            var items = await _orderItemRepository.GetByOrderIdAsync(id);

            return new OrderResponseDto
            {
                Id = order.Id,
                UserId = order.UserId,
                TotalAmount = order.TotalAmount,
                Status = order.Status,
                CreatedAt = order.CreatedAt,
                Items = items.Select(item => new OrderItemResponseDto
                {
                    Id = item.Id,
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice
                }).ToList()
            };
        }

        public async Task<IEnumerable<OrderResponseDto>> GetOrdersByUserIdAsync(int userId)
        {
            var orders = await _orderRepository.GetByUserIdAsync(userId);

            var result = new List<OrderResponseDto>();

            foreach (var order in orders)
            {
                var items = await _orderItemRepository.GetByOrderIdAsync(order.Id);

                result.Add(new OrderResponseDto
                {
                    Id = order.Id,
                    UserId = order.UserId,
                    TotalAmount = order.TotalAmount,
                    Status = order.Status,
                    CreatedAt = order.CreatedAt,
                    Items = items.Select(item => new OrderItemResponseDto
                    {
                        Id = item.Id,
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice
                    }).ToList()
                });
            }

            return result;
        }

        public async Task UpdateOrderStatusAsync(int orderId, int status)
        {
            var order = await _orderRepository.GetByIdAsync(orderId);

            if (order == null)
            {
                throw new KeyNotFoundException("Order not found.");
            }

            if (!Enum.IsDefined(typeof(OrderStatus), status))
            {
                throw new ArgumentException("Invalid order status.");
            }

            order.Status = (OrderStatus)status;

            await _orderRepository.UpdateAsync(order);
        }
    }
}