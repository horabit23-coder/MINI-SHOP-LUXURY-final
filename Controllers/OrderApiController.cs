
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Text.RegularExpressions;
using System.Web.Mvc;
using MINI_SHOP_LUXURY.Models;

namespace MINI_SHOP_LUXURY.Controllers
{
    public class OrderApiController : Controller
    {
        public class OrderItemInput
        {
            public int ProductId { get; set; }
            public int Quantity { get; set; }
        }

        public class OrderInput
        {
            public string CustomerName { get; set; }
            public string Phone { get; set; }
            public string Address { get; set; }
            public string City { get; set; }
            public string PaymentMethod { get; set; }
            public List<OrderItemInput> Items { get; set; }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(OrderInput order)
        {
            if (order == null ||
                string.IsNullOrWhiteSpace(order.CustomerName) ||
                string.IsNullOrWhiteSpace(order.Phone) ||
                string.IsNullOrWhiteSpace(order.Address) ||
                string.IsNullOrWhiteSpace(order.City) ||
                order.Items == null ||
                order.Items.Count == 0)
            {
                return Json(new
                {
                    success = false,
                    message = "Thông tin đơn hàng không hợp lệ."
                });
            }

            string customerName = order.CustomerName.Trim();
            string phone = order.Phone.Trim();
            string address = order.Address.Trim();
            string city = order.City.Trim();

            string fullAddress = address + ", " + city;

            string paymentMethod =
                string.IsNullOrWhiteSpace(order.PaymentMethod)
                ? "COD"
                : order.PaymentMethod.Trim().ToUpperInvariant();

            if (paymentMethod != "COD" &&
                paymentMethod != "BANK")
            {
                return Json(new
                {
                    success = false,
                    message = "Phương thức thanh toán không hợp lệ."
                });
            }

            if (!Regex.IsMatch(phone, @"^[0-9]{10}$"))
            {
                return Json(new
                {
                    success = false,
                    message = "Số điện thoại phải có đúng 10 chữ số."
                });
            }

            if (customerName.Length > 150 ||
                fullAddress.Length > 500 ||
                order.Items.Count > 100)
            {
                return Json(new
                {
                    success = false,
                    message = "Thông tin đơn hàng vượt quá giới hạn."
                });
            }

            var quantities = new Dictionary<int, int>();

            foreach (OrderItemInput item in order.Items)
            {
                if (item == null ||
                    item.ProductId <= 0 ||
                    item.Quantity <= 0 ||
                    item.Quantity > 100)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Số lượng sản phẩm không hợp lệ."
                    });
                }

                if (!quantities.ContainsKey(item.ProductId))
                {
                    quantities[item.ProductId] = 0;
                }

                quantities[item.ProductId] += item.Quantity;

                if (quantities[item.ProductId] > 100)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Số lượng sản phẩm vượt giới hạn."
                    });
                }
            }

            try
            {
                using (SqlConnection connection = Db.GetConnection())
                {
                    connection.Open();

                    using (SqlTransaction transaction =
                        connection.BeginTransaction())
                    {
                        try
                        {
                            decimal totalAmount = 0;

                            var orderLines =
                                new List<Tuple<int, int, decimal>>();

                            // KIEM TRA SAN PHAM VA TON KHO
                            var productIds =
                                new List<int>(quantities.Keys);

                            productIds.Sort();

                            foreach (int productId in productIds)
                            {
                                int quantity = quantities[productId];

                                const string productSql = @"
                                    SELECT
                                        p.Price,
                                        p.StockQuantity
                                    FROM dbo.Products p
                                        WITH (UPDLOCK, HOLDLOCK)
                                    INNER JOIN dbo.Categories c
                                        ON p.CategoryId = c.CategoryId
                                    WHERE p.ProductId = @ProductId
                                      AND p.IsActive = 1
                                      AND c.IsActive = 1";

                                using (SqlCommand command =
                                    new SqlCommand(
                                        productSql,
                                        connection,
                                        transaction))
                                {
                                    command.Parameters.Add(
                                        "@ProductId",
                                        SqlDbType.Int
                                    ).Value = productId;

                                    using (SqlDataReader reader =
                                        command.ExecuteReader())
                                    {
                                        if (!reader.Read())
                                        {
                                            throw new InvalidOperationException(
                                                "Có sản phẩm không còn được bán."
                                            );
                                        }

                                        decimal price =
                                            Convert.ToDecimal(
                                                reader["Price"]
                                            );

                                        int stock =
                                            Convert.ToInt32(
                                                reader["StockQuantity"]
                                            );

                                        if (stock < quantity)
                                        {
                                            throw new InvalidOperationException(
                                                "Sản phẩm không đủ số lượng tồn kho."
                                            );
                                        }

                                        totalAmount += price * quantity;

                                        orderLines.Add(
                                            Tuple.Create(
                                                productId,
                                                quantity,
                                                price
                                            )
                                        );
                                    }
                                }
                            }

                            // XAC DINH KHACH HANG DANG NHAP
                            int? customerId = null;
                            int parsedId;

                            if (Session["CustomerId"] != null &&
                                int.TryParse(
                                    Convert.ToString(
                                        Session["CustomerId"]
                                    ),
                                    out parsedId))
                            {
                                const string customerSql = @"
                                    SELECT COUNT(*)
                                    FROM dbo.Customers
                                    WHERE CustomerId = @Id
                                      AND IsActive = 1";

                                using (SqlCommand command =
                                    new SqlCommand(
                                        customerSql,
                                        connection,
                                        transaction))
                                {
                                    command.Parameters.Add(
                                        "@Id",
                                        SqlDbType.Int
                                    ).Value = parsedId;

                                    if (Convert.ToInt32(
                                        command.ExecuteScalar()) > 0)
                                    {
                                        customerId = parsedId;
                                    }
                                }
                            }

                            // TAO DON HANG
                            const string orderSql = @"
                                INSERT INTO dbo.Orders
                                (
                                    CustomerId,
                                    CustomerName,
                                    Phone,
                                    Address,
                                    TotalAmount,
                                    OrderStatus,
                                    PaymentMethod,
                                    PaymentStatus,
                                    PaidAt
                                )
                                OUTPUT INSERTED.OrderId
                                VALUES
                                (
                                    @CustomerId,
                                    @CustomerName,
                                    @Phone,
                                    @Address,
                                    @TotalAmount,
                                    N'Chờ xác nhận',
                                    @PaymentMethod,
                                    N'Chưa thanh toán',
                                    NULL
                                )";

                            int orderId;

                            using (SqlCommand command =
                                new SqlCommand(
                                    orderSql,
                                    connection,
                                    transaction))
                            {
                                command.Parameters.Add(
                                    "@CustomerId",
                                    SqlDbType.Int
                                ).Value = customerId.HasValue
                                    ? (object)customerId.Value
                                    : DBNull.Value;

                                command.Parameters.Add(
                                    "@CustomerName",
                                    SqlDbType.NVarChar,
                                    150
                                ).Value = customerName;

                                command.Parameters.Add(
                                    "@Phone",
                                    SqlDbType.NVarChar,
                                    20
                                ).Value = phone;

                                command.Parameters.Add(
                                    "@Address",
                                    SqlDbType.NVarChar,
                                    500
                                ).Value = fullAddress;

                                SqlParameter amount =
                                    command.Parameters.Add(
                                        "@TotalAmount",
                                        SqlDbType.Decimal
                                    );

                                amount.Precision = 18;
                                amount.Scale = 2;
                                amount.Value = totalAmount;

                                command.Parameters.Add(
                                    "@PaymentMethod",
                                    SqlDbType.NVarChar,
                                    30
                                ).Value = paymentMethod;

                                orderId = Convert.ToInt32(
                                    command.ExecuteScalar()
                                );
                            }

                            // LUU CHI TIET DON HANG VA TRU TON KHO
                            foreach (var line in orderLines)
                            {
                                const string detailSql = @"
                                    INSERT INTO dbo.OrderDetails
                                    (
                                        OrderId,
                                        ProductId,
                                        Quantity,
                                        UnitPrice
                                    )
                                    VALUES
                                    (
                                        @OrderId,
                                        @ProductId,
                                        @Quantity,
                                        @UnitPrice
                                    )";

                                using (SqlCommand command =
                                    new SqlCommand(
                                        detailSql,
                                        connection,
                                        transaction))
                                {
                                    command.Parameters.Add(
                                        "@OrderId",
                                        SqlDbType.Int
                                    ).Value = orderId;

                                    command.Parameters.Add(
                                        "@ProductId",
                                        SqlDbType.Int
                                    ).Value = line.Item1;

                                    command.Parameters.Add(
                                        "@Quantity",
                                        SqlDbType.Int
                                    ).Value = line.Item2;

                                    SqlParameter price =
                                        command.Parameters.Add(
                                            "@UnitPrice",
                                            SqlDbType.Decimal
                                        );

                                    price.Precision = 18;
                                    price.Scale = 2;
                                    price.Value = line.Item3;

                                    command.ExecuteNonQuery();
                                }

                                const string stockSql = @"
                                    UPDATE dbo.Products
                                    SET StockQuantity =
                                        StockQuantity - @Quantity
                                    WHERE ProductId = @ProductId
                                      AND StockQuantity >= @Quantity";

                                using (SqlCommand command =
                                    new SqlCommand(
                                        stockSql,
                                        connection,
                                        transaction))
                                {
                                    command.Parameters.Add(
                                        "@ProductId",
                                        SqlDbType.Int
                                    ).Value = line.Item1;

                                    command.Parameters.Add(
                                        "@Quantity",
                                        SqlDbType.Int
                                    ).Value = line.Item2;

                                    if (command.ExecuteNonQuery() != 1)
                                    {
                                        throw new InvalidOperationException(
                                            "Sản phẩm không đủ hàng."
                                        );
                                    }
                                }
                            }

                            // LUU DON HANG THANH CONG
                            transaction.Commit();

                            // LUU MA DON VUA TAO CHO TRANG COD
                            Session["LastCreatedOrderId"] = orderId;

                            bool isBank = paymentMethod == "BANK";

                            // CHUYEN TRANG THEO PHUONG THUC THANH TOAN
                            string redirectUrl = isBank
                                ? Url.Action(
                                    "Index",
                                    "Payment",
                                    new { id = orderId }
                                )
                                : Url.Action(
                                    "Index",
                                    "OrderSuccess"
                                );

                            return Json(new
                            {
                                success = true,
                                orderId = orderId,
                                total = totalAmount,
                                paymentMethod = paymentMethod,
                                paymentStatus = "Chưa thanh toán",
                                redirectUrl = redirectUrl,
                                paymentUrl = isBank ? redirectUrl : "",
                                message = "Đặt hàng thành công."
                            });
                        }
                        catch (InvalidOperationException ex)
                        {
                            transaction.Rollback();

                            return Json(new
                            {
                                success = false,
                                message = ex.Message
                            });
                        }
                        catch (Exception)
                        {
                            transaction.Rollback();

                            Response.StatusCode = 500;

                            return Json(new
                            {
                                success = false,
                                message =
                                    "Không thể lưu đơn hàng. Vui lòng thử lại."
                            });
                        }
                    }
                }
            }
            catch (SqlException)
            {
                Response.StatusCode = 500;

                return Json(new
                {
                    success = false,
                    message =
                        "Không thể kết nối SQL Server. Vui lòng thử lại."
                });
            }
        }
    }
}
