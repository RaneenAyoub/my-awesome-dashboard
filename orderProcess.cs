using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Net.Mail;

namespace ShopEngine.BusinessLogics
{
    /// <summary>
    /// Represents custom exceptions thrown when a customer account is blocked or not found.
    /// </summary>
    public class CustomerException : Exception { public CustomerException(string message) : base(message) { } }

    /// <summary>
    /// Represents custom exceptions thrown when a product is unavailable or out of stock.
    /// </summary>
    public class ProductUnavailableException : Exception { public ProductUnavailableException(string message) : base(message) { } }

    /// <summary>
    /// Handles the verification, calculation, database updates, and notification delivery for customer orders.
    /// </summary>
    public class OrderProcessor
    {
        private readonly string _connectionString;
        private readonly string _smtpHost;

        /// <summary>
        /// Initializes a new instance of the <see cref="OrderProcessor"/> class.
        /// </summary>
        /// <param name="connectionString">The database connection string.</param>
        /// <param name="smtpHost">The SMTP host address for email notifications.</param>
        public OrderProcessor(string connectionString, string smtpHost)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _smtpHost = smtpHost ?? throw new ArgumentNullException(nameof(smtpHost));
        }

        /// <summary>
        /// Processes a customer order by validating the customer, updating product inventory, applying discount coupons, and sending receipts.
        /// </summary>
        /// <param name="customerId">The unique identifier of the customer.</param>
        /// <param name="productIds">The list of product identifiers to be purchased.</param>
        /// <param name="couponCode">The optional coupon code for applying discounts.</param>
        /// <param name="shouldSendEmail">If set to <c>true</c>, an email receipt will be sent to the customer.</param>
        /// <returns>The final calculated order total after discounts.</returns>
        /// <exception cref="CustomerException">Thrown when the customer is missing or blocked.</exception>
        /// <exception cref="ProductUnavailableException">Thrown when any requested product is out of stock or missing.</exception>
        public decimal ProcessOrder(int customerId, List<int> productIds, string couponCode, bool shouldSendEmail)
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            string customerEmail = ValidateAndGetCustomerEmail(customerId, connection);
            decimal subTotal = ReserveInventoryAndCalculateSubtotal(productIds, connection);
            decimal finalTotal = ApplyCouponDiscount(subTotal, couponCode, connection);

            SaveOrder(customerId, finalTotal, connection);

            if (shouldSendEmail)
            {
                SendOrderConfirmationEmail(customerEmail, finalTotal);
            }

            return finalTotal;
        }

        private string ValidateAndGetCustomerEmail(int customerId, SqlConnection connection)
        {
            const string query = "SELECT email, is_blocked FROM customers WHERE id = @CustomerId";
            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@CustomerId", customerId);

            using var reader = command.ExecuteReader();
            if (!reader.Read())
                throw new CustomerException($"Customer with ID {customerId} was not found.");

            if ((bool)reader["is_blocked"])
                throw new CustomerException($"Customer with ID {customerId} is blocked.");

            return reader["email"].ToString();
        }

        private decimal ReserveInventoryAndCalculateSubtotal(List<int> productIds, SqlConnection connection)
        {
            decimal total = 0;
            foreach (int productId in productIds)
            {
                total += ProcessProductSelection(productId, connection);
            }
            return total;
        }

        private decimal ProcessProductSelection(int productId, SqlConnection connection)
        {
            const string selectQuery = "SELECT stock, price FROM products WHERE id = @ProductId";
            using var selectCommand = new SqlCommand(selectQuery, connection);
            selectCommand.Parameters.AddWithValue("@ProductId", productId);

            using var reader = selectCommand.ExecuteReader();
            if (!reader.Read() || (int)reader["stock"] <= 0)
                throw new ProductUnavailableException($"Product ID {productId} is unavailable or out of stock.");

            decimal price = (decimal)reader["price"];
            reader.Close(); 

            DeductProductStock(productId, connection);
            return price;
        }

        private void DeductProductStock(int productId, SqlConnection connection)
        {
            const string updateQuery = "UPDATE products SET stock = stock - 1 WHERE id = @ProductId";
            using var updateCommand = new SqlCommand(updateQuery, connection);
            updateCommand.Parameters.AddWithValue("@ProductId", productId);
            updateCommand.ExecuteNonQuery();
        }

        private decimal ApplyCouponDiscount(decimal currentTotal, string couponCode, SqlConnection connection)
        {
            if (string.IsNullOrWhiteSpace(couponCode)) return currentTotal;

            const string query = "SELECT expiry, discount_pct FROM coupons WHERE code = @CouponCode";
            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@CouponCode", couponCode);

            using var reader = command.ExecuteReader();
            if (!reader.Read()) return currentTotal;

            DateTime expiryDate = (DateTime)reader["expiry"];
            decimal discountPercentage = (decimal)reader["discount_pct"];

            if (expiryDate > DateTime.Now)
            {
                currentTotal -= (currentTotal * discountPercentage / 100);
            }

            return currentTotal;
        }

        private void SaveOrder(int customerId, decimal totalAmount, SqlConnection connection)
        {
            const string query = "INSERT INTO orders (customer_id, total, created_at) VALUES (@CustomerId, @Total, @CreatedAt)";
            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@CustomerId", customerId);
            command.Parameters.AddWithValue("@Total", totalAmount);
            command.Parameters.AddWithValue("@CreatedAt", DateTime.Now);

            command.ExecuteNonQuery();
        }

        private void SendOrderConfirmationEmail(string recipientEmail, decimal orderTotal)
        {
            using var smtpClient = new SmtpClient(_smtpHost);
            using var mailMessage = new MailMessage();
            
            mailMessage.From = new MailAddress("orders@shop.com");
            mailMessage.To.Add(recipientEmail);
            mailMessage.Subject = "Order Confirmation";
            mailMessage.Body = $"Total: {orderTotal}";

            smtpClient.Send(mailMessage);
        }
    }
}