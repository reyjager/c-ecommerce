using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using MyMvcProject.Data;
using MyMvcProject.Models;

namespace MyMvcProject.Controllers
{
    public class ProductController : Controller
    {
        private readonly ILogger<ProductController> _logger;
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public ProductController(ILogger<ProductController> logger, ApplicationDbContext context, IConfiguration configuration)
        {
            _logger = logger;
            _context = context;
            _configuration = configuration;
        }

        public IActionResult Index()
        {
            try
            {
                // Use direct SQL to get products
                List<Product> products = new List<Product>();
                string connectionString = _configuration.GetConnectionString("DefaultConnection") ?? "";

                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();

                    // Ensure Products table exists
                    EnsureProductsTableExists(connection);

                    string sql = @"SELECT ""Id"", ""ProductId"", ""Name"", ""SkuNumber"", ""ModelNumber"", ""BarcodeNumber"", ""Description"", ""Price"", ""StockQuantity"", ""ImageUrl"", ""IsActive"", ""DateCreated"", ""DateUpdated"" 
                              FROM ""Products"" ORDER BY ""Id""";
                    using (var cmd = new NpgsqlCommand(sql, connection))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                products.Add(new Product
                                {
                                    Id = reader.GetInt32(0),
                                    ProductId = reader.GetInt32(1),
                                    Name = reader.GetString(2),
                                    SkuNumber = reader.IsDBNull(3) ? null : reader.GetString(3),
                                    ModelNumber = reader.IsDBNull(4) ? null : reader.GetString(4),
                                    BarcodeNumber = reader.IsDBNull(5) ? null : reader.GetString(5),
                                    Description = reader.IsDBNull(6) ? null : reader.GetString(6),
                                    Price = reader.GetDecimal(7),
                                    StockQuantity = reader.GetInt32(8),
                                    ImageUrl = reader.IsDBNull(9) ? null : reader.GetString(9),
                                    IsActive = reader.GetBoolean(10),
                                    DateCreated = reader.GetDateTime(11),
                                    DateUpdated = reader.IsDBNull(12) ? null : reader.GetDateTime(12)
                                });
                            }
                        }
                    }

                    connection.Close();
                }

                return View(products);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving products");
                TempData["ErrorMessage"] = "Error retrieving products: " + ex.Message;
                return View(new List<Product>());
            }
        }

        [HttpGet]
        public IActionResult Create()
        {
            var product = new Product();

            // Generate a new ProductId starting from 600000
            string connectionString = _configuration.GetConnectionString("DefaultConnection") ?? "";
            using (var connection = new NpgsqlConnection(connectionString))
            {
                connection.Open();

                // Get the last ProductId from the database
                int nextProductId = 600000; // Default starting value
                using (var maxIdCmd = new NpgsqlCommand("SELECT COALESCE(MAX(\"ProductId\"), 0) FROM \"Products\"", connection))
                {
                    int maxProductId = Convert.ToInt32(maxIdCmd.ExecuteScalar());

                    // If there are existing products, increment by 1, otherwise start at 600000
                    nextProductId = maxProductId >= 600000 ? maxProductId + 1 : 600000;
                }

                product.ProductId = nextProductId;

                // Generate barcode and SKU
                product.BarcodeNumber = GenerateBarcodeNumber();
                product.SkuNumber = GenerateSkuNumber();

                connection.Close();
            }

            return View(product);
        }

        [HttpPost]
        public IActionResult Create(Product product)
        {
            // Debug logging
            _logger.LogInformation("Create POST method called");
            _logger.LogInformation("Product data: Name={Name}, ProductId={ProductId}", product.Name, product.ProductId);

            // Ensure SKU and barcode are assigned if not provided
            if (string.IsNullOrEmpty(product.SkuNumber))
            {
                product.SkuNumber = GenerateSkuNumber();
                _logger.LogInformation("Generated SKU number: {SkuNumber}", product.SkuNumber);
            }

            if (string.IsNullOrEmpty(product.BarcodeNumber))
            {
                product.BarcodeNumber = GenerateBarcodeNumber();
                _logger.LogInformation("Generated barcode number: {BarcodeNumber}", product.BarcodeNumber);
            }

            // Validate model
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Model state is invalid");
                foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                {
                    _logger.LogWarning(error.ErrorMessage);
                }
                return View(product);
            }

            try
            {
                string connectionString = _configuration.GetConnectionString("DefaultConnection") ?? "";
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    _logger.LogInformation("Database connection opened successfully");

                    // Check if table exists, if not create it
                    EnsureProductsTableExists(connection);

                    // Check if ProductId already exists
                    using (var checkCmd = new NpgsqlCommand("SELECT COUNT(*) FROM \"Products\" WHERE \"ProductId\" = @productId", connection))
                    {
                        checkCmd.Parameters.AddWithValue("productId", product.ProductId);
                        int count = Convert.ToInt32(checkCmd.ExecuteScalar());

                        if (count > 0)
                        {
                            ModelState.AddModelError("ProductId", "Product ID already exists.");
                            return View(product);
                        }
                    }

                    // Check if SKU already exists (if provided)
                    if (!string.IsNullOrEmpty(product.SkuNumber))
                    {
                        using (var checkCmd = new NpgsqlCommand("SELECT COUNT(*) FROM \"Products\" WHERE \"SkuNumber\" = @skuNumber", connection))
                        {
                            checkCmd.Parameters.AddWithValue("skuNumber", product.SkuNumber);
                            int count = Convert.ToInt32(checkCmd.ExecuteScalar());

                            if (count > 0)
                            {
                                ModelState.AddModelError("SkuNumber", "SKU Number already exists.");
                                return View(product);
                            }
                        }
                    }

                    // Insert the product directly with SQL
                    string insertSql = @"
                    INSERT INTO ""Products"" (""ProductId"", ""Name"", ""SkuNumber"", ""ModelNumber"", ""BarcodeNumber"", ""Description"", ""Price"", ""StockQuantity"", ""ImageUrl"", ""IsActive"", ""DateCreated"", ""DateUpdated"")
                    VALUES (@productId, @name, @skuNumber, @modelNumber, @barcodeNumber, @description, @price, @stockQuantity, @imageUrl, @isActive, @dateCreated, @dateUpdated)
                    RETURNING ""Id""";

                    using (var cmd = new NpgsqlCommand(insertSql, connection))
                    {
                        DateTime now = DateTime.UtcNow;

                        cmd.Parameters.AddWithValue("productId", product.ProductId);
                        cmd.Parameters.AddWithValue("name", product.Name);
                        cmd.Parameters.AddWithValue("skuNumber", product.SkuNumber ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("modelNumber", product.ModelNumber ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("barcodeNumber", product.BarcodeNumber ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("description", product.Description ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("price", 0); // Default price to 0 since field was removed
                        cmd.Parameters.AddWithValue("stockQuantity", 0); // Default stock to 0 since field was removed
                        cmd.Parameters.AddWithValue("imageUrl", (object)DBNull.Value); // No image URL since field was removed
                        cmd.Parameters.AddWithValue("isActive", product.IsActive);
                        cmd.Parameters.AddWithValue("dateCreated", now);
                        cmd.Parameters.AddWithValue("dateUpdated", (object)DBNull.Value);

                        // Execute the command and get the Id
                        int newId = Convert.ToInt32(cmd.ExecuteScalar());
                        product.Id = newId;
                        product.DateCreated = now;

                        _logger.LogInformation("Product saved successfully with ID: {Id} and ProductId: {ProductId}", product.Id, product.ProductId);
                    }

                    connection.Close();
                }

                // Add success message
                TempData["SuccessMessage"] = "Product created successfully!";

                // Redirect to Products page
                return RedirectToAction("Index");
            }
            catch (NpgsqlException npgEx)
            {
                // Log the specific PostgreSQL error
                _logger.LogError(npgEx, "PostgreSQL error occurred: {Message}", npgEx.Message);

                if (npgEx.Message.Contains("relation") && npgEx.Message.Contains("does not exist"))
                {
                    // Table doesn't exist, create it
                    try
                    {
                        using (var connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                        {
                            connection.Open();
                            EnsureProductsTableExists(connection);
                            connection.Close();
                        }

                        // Try again with the same product
                        return Create(product);
                    }
                    catch (Exception tableEx)
                    {
                        _logger.LogError(tableEx, "Error creating Products table");
                        ModelState.AddModelError("", "Database table does not exist. Error creating table.");
                    }
                }
                else if (npgEx.Message.Contains("duplicate key"))
                {
                    ModelState.AddModelError("", "A product with this information already exists.");
                }
                else
                {
                    ModelState.AddModelError("", "A database error occurred: " + npgEx.Message);
                }

                return View(product);
            }
            catch (Exception ex)
            {
                // Log the general error
                _logger.LogError(ex, "Error occurred while saving product: {Message}", ex.Message);

                // Add error message
                ModelState.AddModelError("", "An error occurred while saving the product: " + ex.Message);
                return View(product);
            }
        }

        [HttpGet]
        public IActionResult Edit(int id)
        {
            try
            {
                // Get product by ID
                Product? product = null;
                string connectionString = _configuration.GetConnectionString("DefaultConnection") ?? "";

                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();

                    string sql = @"SELECT ""Id"", ""ProductId"", ""Name"", ""SkuNumber"", ""ModelNumber"", ""BarcodeNumber"", ""Description"", ""Price"", ""StockQuantity"", ""ImageUrl"", ""IsActive"", ""DateCreated"", ""DateUpdated"" 
                              FROM ""Products"" WHERE ""Id"" = @id";
                    using (var cmd = new NpgsqlCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("id", id);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                product = new Product
                                {
                                    Id = reader.GetInt32(0),
                                    ProductId = reader.GetInt32(1),
                                    Name = reader.GetString(2),
                                    SkuNumber = reader.IsDBNull(3) ? null : reader.GetString(3),
                                    ModelNumber = reader.IsDBNull(4) ? null : reader.GetString(4),
                                    BarcodeNumber = reader.IsDBNull(5) ? null : reader.GetString(5),
                                    Description = reader.IsDBNull(6) ? null : reader.GetString(6),
                                    Price = reader.GetDecimal(7),
                                    StockQuantity = reader.GetInt32(8),
                                    ImageUrl = reader.IsDBNull(9) ? null : reader.GetString(9),
                                    IsActive = reader.GetBoolean(10),
                                    DateCreated = reader.GetDateTime(11),
                                    DateUpdated = reader.IsDBNull(12) ? null : reader.GetDateTime(12)
                                };
                            }
                        }
                    }

                    connection.Close();
                }

                if (product == null)
                {
                    TempData["ErrorMessage"] = "Product not found.";
                    return RedirectToAction("Index");
                }

                return View(product);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving product for edit");
                TempData["ErrorMessage"] = "Error retrieving product.";
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        public IActionResult Edit(Product product)
        {
            // Debug logging
            _logger.LogInformation("Edit POST method called for product ID: {Id}", product.Id);

            // Validate model
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Model state is invalid");
                foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                {
                    _logger.LogWarning(error.ErrorMessage);
                }
                return View(product);
            }

            try
            {
                string connectionString = _configuration.GetConnectionString("DefaultConnection") ?? "";
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();

                    // Check if SKU already exists (if provided) but exclude current product
                    if (!string.IsNullOrEmpty(product.SkuNumber))
                    {
                        using (var checkCmd = new NpgsqlCommand("SELECT COUNT(*) FROM \"Products\" WHERE \"SkuNumber\" = @skuNumber AND \"Id\" != @id", connection))
                        {
                            checkCmd.Parameters.AddWithValue("skuNumber", product.SkuNumber);
                            checkCmd.Parameters.AddWithValue("id", product.Id);
                            int count = Convert.ToInt32(checkCmd.ExecuteScalar());

                            if (count > 0)
                            {
                                ModelState.AddModelError("SkuNumber", "SKU Number already exists on another product.");
                                return View(product);
                            }
                        }
                    }

                    // Update the product
                    string updateSql = @"
                    UPDATE ""Products"" 
                    SET ""Name"" = @name, 
                        ""SkuNumber"" = @skuNumber,
                        ""ModelNumber"" = @modelNumber,
                        ""BarcodeNumber"" = @barcodeNumber,
                        ""Description"" = @description,
                        ""Price"" = @price,
                        ""StockQuantity"" = @stockQuantity,
                        ""IsActive"" = @isActive,
                        ""DateUpdated"" = @dateUpdated
                    WHERE ""Id"" = @id";

                    using (var cmd = new NpgsqlCommand(updateSql, connection))
                    {
                        DateTime now = DateTime.UtcNow;

                        cmd.Parameters.AddWithValue("name", product.Name);
                        cmd.Parameters.AddWithValue("skuNumber", product.SkuNumber ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("modelNumber", product.ModelNumber ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("barcodeNumber", product.BarcodeNumber ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("description", product.Description ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("price", product.Price);
                        cmd.Parameters.AddWithValue("stockQuantity", product.StockQuantity);
                        cmd.Parameters.AddWithValue("isActive", product.IsActive);
                        cmd.Parameters.AddWithValue("dateUpdated", now);
                        cmd.Parameters.AddWithValue("id", product.Id);

                        int rowsAffected = cmd.ExecuteNonQuery();
                        _logger.LogInformation("Product updated successfully. Rows affected: {Rows}", rowsAffected);

                        product.DateUpdated = now;
                    }

                    connection.Close();
                }

                // Add success message
                TempData["SuccessMessage"] = "Product updated successfully!";

                // Redirect to Products page
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                // Log the error
                _logger.LogError(ex, "Error occurred while updating product: {Message}", ex.Message);

                // Add error message
                ModelState.AddModelError("", "An error occurred while updating the product: " + ex.Message);
                return View(product);
            }
        }

        [HttpGet]
        public IActionResult Details(int id)
        {
            try
            {
                // Get product by ID
                Product? product = null;
                string connectionString = _configuration.GetConnectionString("DefaultConnection") ?? "";

                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();

                    string sql = @"SELECT ""Id"", ""ProductId"", ""Name"", ""SkuNumber"", ""ModelNumber"", ""BarcodeNumber"", ""Description"", ""Price"", ""StockQuantity"", ""ImageUrl"", ""IsActive"", ""DateCreated"", ""DateUpdated"" 
                              FROM ""Products"" WHERE ""Id"" = @id";
                    using (var cmd = new NpgsqlCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("id", id);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                product = new Product
                                {
                                    Id = reader.GetInt32(0),
                                    ProductId = reader.GetInt32(1),
                                    Name = reader.GetString(2),
                                    SkuNumber = reader.IsDBNull(3) ? null : reader.GetString(3),
                                    ModelNumber = reader.IsDBNull(4) ? null : reader.GetString(4),
                                    BarcodeNumber = reader.IsDBNull(5) ? null : reader.GetString(5),
                                    Description = reader.IsDBNull(6) ? null : reader.GetString(6),
                                    Price = reader.GetDecimal(7),
                                    StockQuantity = reader.GetInt32(8),
                                    ImageUrl = reader.IsDBNull(9) ? null : reader.GetString(9),
                                    IsActive = reader.GetBoolean(10),
                                    DateCreated = reader.GetDateTime(11),
                                    DateUpdated = reader.IsDBNull(12) ? null : reader.GetDateTime(12)
                                };
                            }
                        }
                    }

                    connection.Close();
                }

                if (product == null)
                {
                    TempData["ErrorMessage"] = "Product not found.";
                    return RedirectToAction("Index");
                }

                return View(product);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving product details");
                TempData["ErrorMessage"] = "Error retrieving product details.";
                return RedirectToAction("Index");
            }
        }

        [HttpGet]
        public IActionResult Delete(int id)
        {
            try
            {
                // Get product by ID
                Product? product = null;
                string connectionString = _configuration.GetConnectionString("DefaultConnection") ?? "";

                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();

                    string sql = @"SELECT ""Id"", ""ProductId"", ""Name"", ""SkuNumber"", ""ModelNumber"", ""BarcodeNumber"", ""Description"", ""Price"", ""StockQuantity"", ""ImageUrl"", ""IsActive"", ""DateCreated"", ""DateUpdated"" 
                              FROM ""Products"" WHERE ""Id"" = @id";
                    using (var cmd = new NpgsqlCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("id", id);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                product = new Product
                                {
                                    Id = reader.GetInt32(0),
                                    ProductId = reader.GetInt32(1),
                                    Name = reader.GetString(2),
                                    SkuNumber = reader.IsDBNull(3) ? null : reader.GetString(3),
                                    ModelNumber = reader.IsDBNull(4) ? null : reader.GetString(4),
                                    BarcodeNumber = reader.IsDBNull(5) ? null : reader.GetString(5),
                                    Description = reader.IsDBNull(6) ? null : reader.GetString(6),
                                    Price = reader.GetDecimal(7),
                                    StockQuantity = reader.GetInt32(8),
                                    ImageUrl = reader.IsDBNull(9) ? null : reader.GetString(9),
                                    IsActive = reader.GetBoolean(10),
                                    DateCreated = reader.GetDateTime(11),
                                    DateUpdated = reader.IsDBNull(12) ? null : reader.GetDateTime(12)
                                };
                            }
                        }
                    }

                    connection.Close();
                }

                if (product == null)
                {
                    TempData["ErrorMessage"] = "Product not found.";
                    return RedirectToAction("Index");
                }

                return View(product);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving product for delete");
                TempData["ErrorMessage"] = "Error retrieving product: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        [HttpPost, ActionName("Delete")]
        public IActionResult DeleteConfirmed(int id)
        {
            try
            {
                // Delete the product
                string connectionString = _configuration.GetConnectionString("DefaultConnection") ?? "";
                int rowsAffected = 0;

                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();

                    string deleteSql = @"DELETE FROM ""Products"" WHERE ""Id"" = @id";
                    using (var cmd = new NpgsqlCommand(deleteSql, connection))
                    {
                        cmd.Parameters.AddWithValue("id", id);
                        rowsAffected = cmd.ExecuteNonQuery();
                        _logger.LogInformation("Product deleted successfully. Rows affected: {Rows}", rowsAffected);
                    }

                    connection.Close();
                }

                if (rowsAffected > 0)
                {
                    TempData["SuccessMessage"] = "Product deleted successfully!";
                }
                else
                {
                    TempData["ErrorMessage"] = "Product not found or could not be deleted.";
                }

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting product");
                TempData["ErrorMessage"] = "Error deleting product: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        public IActionResult ToggleActive(int id)
        {
            try
            {
                // Toggle the product's active status
                string connectionString = _configuration.GetConnectionString("DefaultConnection") ?? "";
                int rowsAffected = 0;
                bool newStatus = false;

                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();

                    // First, get the current status
                    string selectSql = @"SELECT ""IsActive"" FROM ""Products"" WHERE ""Id"" = @id";
                    using (var cmd = new NpgsqlCommand(selectSql, connection))
                    {
                        cmd.Parameters.AddWithValue("id", id);
                        var currentStatus = cmd.ExecuteScalar();
                        if (currentStatus != null)
                        {
                            newStatus = !(bool)currentStatus;
                        }
                    }

                    // Then update with the opposite status
                    string updateSql = @"UPDATE ""Products"" SET ""IsActive"" = @isActive, ""DateUpdated"" = @dateUpdated WHERE ""Id"" = @id";
                    using (var cmd = new NpgsqlCommand(updateSql, connection))
                    {
                        DateTime now = DateTime.UtcNow;
                        cmd.Parameters.AddWithValue("isActive", newStatus);
                        cmd.Parameters.AddWithValue("dateUpdated", now);
                        cmd.Parameters.AddWithValue("id", id);

                        rowsAffected = cmd.ExecuteNonQuery();
                        _logger.LogInformation("Product active status updated successfully. Rows affected: {Rows}", rowsAffected);
                    }

                    connection.Close();
                }

                if (rowsAffected > 0)
                {
                    TempData["SuccessMessage"] = newStatus ?
                        "Product activated successfully!" :
                        "Product deactivated successfully!";
                }
                else
                {
                    TempData["ErrorMessage"] = "Product not found or could not be updated.";
                }

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating product active status");
                TempData["ErrorMessage"] = "Error updating product status: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // Generates a unique 10-digit barcode number
        private string GenerateBarcodeNumber()
        {
            // Generate a random 10-digit number for barcode
            Random random = new Random();
            string barcode = "";

            for (int i = 0; i < 10; i++)
            {
                barcode += random.Next(0, 10).ToString();
            }

            // Check if barcode already exists in database
            string connectionString = _configuration.GetConnectionString("DefaultConnection") ?? "";
            using (var connection = new NpgsqlConnection(connectionString))
            {
                connection.Open();

                using (var checkCmd = new NpgsqlCommand("SELECT COUNT(*) FROM \"Products\" WHERE \"BarcodeNumber\" = @barcodeNumber", connection))
                {
                    checkCmd.Parameters.AddWithValue("barcodeNumber", barcode);
                    int count = Convert.ToInt32(checkCmd.ExecuteScalar());

                    // If barcode exists, generate a new one recursively
                    if (count > 0)
                    {
                        connection.Close();
                        return GenerateBarcodeNumber();
                    }
                }

                connection.Close();
            }

            return barcode;
        }

        // Generates a unique 12-digit SKU number
        private string GenerateSkuNumber()
        {
            // Generate a random 12-digit number for SKU
            Random random = new Random();
            string sku = "";

            for (int i = 0; i < 12; i++)
            {
                sku += random.Next(0, 10).ToString();
            }

            // Check if SKU already exists in database
            string connectionString = _configuration.GetConnectionString("DefaultConnection") ?? "";
            using (var connection = new NpgsqlConnection(connectionString))
            {
                connection.Open();

                using (var checkCmd = new NpgsqlCommand("SELECT COUNT(*) FROM \"Products\" WHERE \"SkuNumber\" = @skuNumber", connection))
                {
                    checkCmd.Parameters.AddWithValue("skuNumber", sku);
                    int count = Convert.ToInt32(checkCmd.ExecuteScalar());

                    // If SKU exists, generate a new one recursively
                    if (count > 0)
                    {
                        connection.Close();
                        return GenerateSkuNumber();
                    }
                }

                connection.Close();
            }

            return sku;
        }

        private void EnsureProductsTableExists(NpgsqlConnection connection)
        {
            // Check if the table exists
            bool tableExists = false;
            using (var cmd = new NpgsqlCommand(
                "SELECT EXISTS (SELECT FROM information_schema.tables WHERE table_name = 'Products')",
                connection))
            {
                tableExists = (bool)cmd.ExecuteScalar();
            }

            if (!tableExists)
            {
                // Create the table with all columns
                string createTableSql = @"
        CREATE TABLE IF NOT EXISTS ""Products"" (
            ""Id"" SERIAL PRIMARY KEY,
            ""ProductId"" INTEGER NOT NULL,
            ""Name"" VARCHAR(100) NOT NULL,
            ""SkuNumber"" VARCHAR(50),
            ""ModelNumber"" VARCHAR(50),
            ""BarcodeNumber"" VARCHAR(50),
            ""Description"" VARCHAR(500),
            ""Price"" DECIMAL(18,2) NOT NULL,
            ""StockQuantity"" INTEGER NOT NULL,
            ""branch""  VARCHAR(50),
            ""location""  VARCHAR(50),
            ""ImageUrl"" VARCHAR(200),
            ""IsActive"" BOOLEAN NOT NULL DEFAULT TRUE,
            ""DateCreated"" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            ""DateUpdated"" TIMESTAMP NULL
        )";

                using (var cmd = new NpgsqlCommand(createTableSql, connection))
                {
                    cmd.ExecuteNonQuery();
                    _logger.LogInformation("Products table created successfully");
                }

                // Add unique constraint on ProductId
                string addConstraintSql = @"ALTER TABLE ""Products"" ADD CONSTRAINT ""Products_ProductId_Unique"" UNIQUE (""ProductId"")";
                using (var cmd = new NpgsqlCommand(addConstraintSql, connection))
                {
                    cmd.ExecuteNonQuery();
                    _logger.LogInformation("Added unique constraint on ProductId");
                }
            }
            else
            {
                // Check if ProductId column exists, if not add it
                bool productIdColumnExists = false;
                using (var cmd = new NpgsqlCommand(
                    "SELECT EXISTS (SELECT FROM information_schema.columns WHERE table_name = 'Products' AND column_name = 'ProductId')",
                    connection))
                {
                    productIdColumnExists = (bool)cmd.ExecuteScalar();
                }

                if (!productIdColumnExists)
                {
                    // Add ProductId column with default value
                    using (var cmd = new NpgsqlCommand(
                        "ALTER TABLE \"Products\" ADD COLUMN \"ProductId\" INTEGER NOT NULL DEFAULT 600000",
                        connection))
                    {
                        cmd.ExecuteNonQuery();
                        _logger.LogInformation("Added ProductId column to Products table");
                    }

                    // Add unique constraint on ProductId
                    string addConstraintSql = @"ALTER TABLE ""Products"" ADD CONSTRAINT ""Products_ProductId_Unique"" UNIQUE (""ProductId"")";
                    using (var cmd = new NpgsqlCommand(addConstraintSql, connection))
                    {
                        cmd.ExecuteNonQuery();
                        _logger.LogInformation("Added unique constraint on ProductId");
                    }
                }

                // Check if other columns exist, if not add them
                string[] columns = { "SkuNumber", "ModelNumber", "BarcodeNumber" };
                foreach (var column in columns)
                {
                    bool columnExists = false;
                    using (var cmd = new NpgsqlCommand(
                        $"SELECT EXISTS (SELECT FROM information_schema.columns WHERE table_name = 'Products' AND column_name = '{column}')",
                        connection))
                    {
                        columnExists = (bool)cmd.ExecuteScalar();
                    }

                    if (!columnExists)
                    {
                        // Add column
                        using (var cmd = new NpgsqlCommand(
                            $"ALTER TABLE \"Products\" ADD COLUMN \"{column}\" VARCHAR(50)",
                            connection))
                        {
                            cmd.ExecuteNonQuery();
                            _logger.LogInformation($"Added {column} column to Products table");
                        }
                    }
                }
            }
        }
    }
}