using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using MyMvcProject.Data;
using MyMvcProject.Models;
using BCrypt.Net;

namespace MyMvcProject.Controllers
{
    public class UserController : Controller
    {
        private readonly ILogger<UserController> _logger;
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public UserController(ILogger<UserController> logger, ApplicationDbContext context, IConfiguration configuration)
        {
            _logger = logger;
            _context = context;
            _configuration = configuration;
        }

        public IActionResult Index()
        {
            return RedirectToAction("List");
        }

        public IActionResult List()
        {
            try
            {
                // Use direct SQL to get users
                List<User> users = new List<User>();
                string connectionString = _configuration.GetConnectionString("DefaultConnection") ?? "";

                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();

                    string sql = @"SELECT ""Id"", ""UserId"",""UserName"", ""Password"", ""Email"", ""Mobile"", ""Roles"", ""DateCreated"", ""DateUpdated"" 
                              FROM ""Users"" ORDER BY ""Id""";
                    using (var cmd = new NpgsqlCommand(sql, connection))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                users.Add(new User
                                {
                                    Id = reader.GetInt32(0),
                                    UserId = reader.GetInt32(1),
                                    UserName = reader.GetString(2),
                                    Password = reader.GetString(3),
                                    Email = reader.GetString(4),
                                    Mobile = reader.GetString(5),
                                    Roles = reader.IsDBNull(6) ? null : reader.GetString(6),
                                    DateCreated = reader.IsDBNull(7) ? DateTime.UtcNow : reader.GetDateTime(7),
                                    DateUpdated = reader.IsDBNull(8) ? null : reader.GetDateTime(8)
                                });
                            }
                        }
                    }

                    connection.Close();
                }

                return View(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving users");
                return View(new List<User>());
            }
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new User());
        }

        [HttpPost]
        public IActionResult Create(User user)
        {
            // Debug logging
            _logger.LogInformation("Create POST method called");
            _logger.LogInformation("User data: Username={Username}, Email={Email}", user.UserName, user.Email);

            // Validate model
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Model state is invalid");
                foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                {
                    _logger.LogWarning(error.ErrorMessage);
                }
                return View(user);
            }

            // Set default role if not provided
            if (string.IsNullOrEmpty(user.Roles))
            {
                user.Roles = "User";
            }

            // Use direct SQL approach instead of Entity Framework
            try
            {
                string connectionString = _configuration.GetConnectionString("DefaultConnection") ?? "";
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    _logger.LogInformation("Database connection opened successfully");

                    // Check if user with same email already exists
                    using (var checkCmd = new NpgsqlCommand("SELECT COUNT(*) FROM \"Users\" WHERE \"Email\" = @email", connection))
                    {
                        checkCmd.Parameters.AddWithValue("email", user.Email);
                        int count = Convert.ToInt32(checkCmd.ExecuteScalar());

                        if (count > 0)
                        {
                            ModelState.AddModelError("Email", "A user with this email already exists.");
                            return View(user);
                        }
                    }

                    // Check if table exists, if not create it with updated schema
                    EnsureUsersTableExists(connection);

                    // Get the last UserId from the database
                    int nextUserId = 300000; // Default starting value
                    using (var maxIdCmd = new NpgsqlCommand("SELECT COALESCE(MAX(\"UserId\"), 0) FROM \"Users\"", connection))
                    {
                        int maxUserId = Convert.ToInt32(maxIdCmd.ExecuteScalar());
                        _logger.LogInformation("Current maximum UserId in database: {MaxUserId}", maxUserId);
                        Console.WriteLine($"Current maximum UserId in database: {maxUserId}");

                        // If there are existing users, increment by 1, otherwise start at 300000
                        nextUserId = maxUserId >= 300000 ? maxUserId + 1 : 300000;
                        _logger.LogInformation("Next UserId to be used: {NextUserId}", nextUserId);
                        Console.WriteLine($"Next UserId to be used: {nextUserId}");
                    }

                    // Hash the password
                    string hashedPassword = BCrypt.Net.BCrypt.HashPassword(user.Password);

                    // Insert the user directly with SQL - now including UserId
                    string insertSql = @"
INSERT INTO ""Users"" (""UserId"", ""UserName"", ""Password"", ""Email"", ""Mobile"", ""Roles"", ""DateCreated"", ""DateUpdated"")
VALUES (@userId, @userName, @password, @email, @mobile, @roles, @dateCreated, @dateUpdated)
RETURNING ""Id""";

                    using (var cmd = new NpgsqlCommand(insertSql, connection))
                    {
                        DateTime now = DateTime.UtcNow;

                        cmd.Parameters.AddWithValue("userId", nextUserId);
                        cmd.Parameters.AddWithValue("userName", user.UserName);
                        cmd.Parameters.AddWithValue("password", hashedPassword);
                        cmd.Parameters.AddWithValue("email", user.Email);
                        cmd.Parameters.AddWithValue("mobile", user.Mobile);
                        cmd.Parameters.AddWithValue("roles", user.Roles ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("dateCreated", now);
                        cmd.Parameters.AddWithValue("dateUpdated", DBNull.Value);

                        // Execute the command and get the Id
                        int newId = Convert.ToInt32(cmd.ExecuteScalar());
                        user.Id = newId;
                        user.UserId = nextUserId;
                        user.DateCreated = now;

                        _logger.LogInformation("User saved successfully with ID: {Id} and UserId: {UserId}", user.Id, user.UserId);
                        Console.WriteLine($"User saved successfully with ID: {user.Id} and UserId: {user.UserId}");
                    }

                    connection.Close();
                }

                // Add success message
                TempData["SuccessMessage"] = "User registered successfully!";

                // Redirect to Users page
                return RedirectToAction("List");
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
                            EnsureUsersTableExists(connection);
                            connection.Close();
                        }

                        // Try again with the same user
                        return Create(user);
                    }
                    catch (Exception tableEx)
                    {
                        _logger.LogError(tableEx, "Error creating Users table");
                        ModelState.AddModelError("", "Database table does not exist. Error creating table.");
                    }
                }
                else if (npgEx.Message.Contains("duplicate key"))
                {
                    ModelState.AddModelError("", "A user with this information already exists.");
                }
                else
                {
                    ModelState.AddModelError("", "A database error occurred: " + npgEx.Message);
                }

                return View(user);
            }
            catch (Exception ex)
            {
                // Log the general error
                _logger.LogError(ex, "Error occurred while saving user: {Message}", ex.Message);

                // Add error message
                ModelState.AddModelError("", "An error occurred while saving the user: " + ex.Message);
                return View(user);
            }
        }

        [HttpGet]
        public IActionResult Edit(int id)
        {
            try
            {
                // Get user by ID
                User? user = null;
                string connectionString = _configuration.GetConnectionString("DefaultConnection") ?? "";

                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();

                    string sql = @"SELECT ""Id"", ""UserId"",""UserName"", ""Password"", ""Email"", ""Mobile"", ""Roles"", ""DateCreated"", ""DateUpdated"" 
                              FROM ""Users"" WHERE ""Id"" = @id";
                    using (var cmd = new NpgsqlCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("id", id);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                user = new User
                                {
                                    Id = reader.GetInt32(0),
                                    UserId = reader.GetInt32(1),
                                    UserName = reader.GetString(2),
                                    Password = reader.GetString(3),
                                    Email = reader.GetString(4),
                                    Mobile = reader.GetString(5),
                                    Roles = reader.IsDBNull(6) ? null : reader.GetString(6),
                                    DateCreated = reader.IsDBNull(7) ? DateTime.UtcNow : reader.GetDateTime(7),
                                    DateUpdated = reader.IsDBNull(8) ? null : reader.GetDateTime(8)
                                };
                            }
                        }
                    }

                    connection.Close();
                }

                if (user == null)
                {
                    TempData["ErrorMessage"] = "User not found.";
                    return RedirectToAction("List");
                }

                return View(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user for edit");
                TempData["ErrorMessage"] = "Error retrieving user.";
                return RedirectToAction("List");
            }
        }

        [HttpPost]
        public IActionResult Edit(User user, string? NewPassword)
        {
            // Debug logging
            _logger.LogInformation("Edit POST method called for user ID: {Id}", user.Id);

            // Validate model
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Model state is invalid");
                foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                {
                    _logger.LogWarning(error.ErrorMessage);
                }
                return View(user);
            }

            try
            {
                string connectionString = _configuration.GetConnectionString("DefaultConnection") ?? "";
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();

                    // Check if another user with the same email exists (excluding current user)
                    using (var checkCmd = new NpgsqlCommand(
                        "SELECT COUNT(*) FROM \"Users\" WHERE \"Email\" = @email AND \"Id\" != @id",
                        connection))
                    {
                        checkCmd.Parameters.AddWithValue("email", user.Email);
                        checkCmd.Parameters.AddWithValue("id", user.Id);
                        int count = Convert.ToInt32(checkCmd.ExecuteScalar());

                        if (count > 0)
                        {
                            ModelState.AddModelError("Email", "Another user with this email already exists.");
                            return View(user);
                        }
                    }

                    // Update the user
                    string updateSql;

                    if (!string.IsNullOrEmpty(NewPassword))
                    {
                        // Hash the new password
                        string hashedPassword = BCrypt.Net.BCrypt.HashPassword(NewPassword);

                        updateSql = @"
                        UPDATE ""Users"" 
                        SET ""UserId"" = @userId,
                            ""UserName"" = @userName, 
                            ""Password"" = @password,
                            ""Email"" = @email, 
                            ""Mobile"" = @mobile, 
                            ""Roles"" = @roles,
                            ""DateUpdated"" = @dateUpdated
                        WHERE ""Id"" = @id";

                        using (var cmd = new NpgsqlCommand(updateSql, connection))
                        {
                            DateTime now = DateTime.UtcNow;

                            cmd.Parameters.AddWithValue("userId", user.UserId);
                            cmd.Parameters.AddWithValue("userName", user.UserName);
                            cmd.Parameters.AddWithValue("password", hashedPassword);
                            cmd.Parameters.AddWithValue("email", user.Email);
                            cmd.Parameters.AddWithValue("mobile", user.Mobile);
                            cmd.Parameters.AddWithValue("roles", user.Roles ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("dateUpdated", now);
                            cmd.Parameters.AddWithValue("id", user.Id);

                            int rowsAffected = cmd.ExecuteNonQuery();
                            _logger.LogInformation("User updated successfully. Rows affected: {Rows}", rowsAffected);

                            user.DateUpdated = now;
                        }
                    }
                    else
                    {
                        // Update without changing password
                        updateSql = @"
                        UPDATE ""Users"" 
                        SET ""UserId"" = @userId, 
                            ""UserName"" = @userName, 
                            ""Email"" = @email, 
                            ""Mobile"" = @mobile, 
                            ""Roles"" = @roles,
                            ""DateUpdated"" = @dateUpdated
                        WHERE ""Id"" = @id";

                        using (var cmd = new NpgsqlCommand(updateSql, connection))
                        {
                            DateTime now = DateTime.UtcNow;

                            cmd.Parameters.AddWithValue("userId", user.UserId);
                            cmd.Parameters.AddWithValue("userName", user.UserName);
                            cmd.Parameters.AddWithValue("email", user.Email);
                            cmd.Parameters.AddWithValue("mobile", user.Mobile);
                            cmd.Parameters.AddWithValue("roles", user.Roles ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("dateUpdated", now);
                            cmd.Parameters.AddWithValue("id", user.Id);

                            int rowsAffected = cmd.ExecuteNonQuery();
                            _logger.LogInformation("User updated successfully. Rows affected: {Rows}", rowsAffected);

                            user.DateUpdated = now;
                        }
                    }

                    connection.Close();
                }

                // Add success message
                TempData["SuccessMessage"] = "User updated successfully!";

                // Redirect to Users page
                return RedirectToAction("List");
            }
            catch (Exception ex)
            {
                // Log the error
                _logger.LogError(ex, "Error occurred while updating user: {Message}", ex.Message);

                // Add error message
                ModelState.AddModelError("", "An error occurred while updating the user: " + ex.Message);
                return View(user);
            }
        }

        [HttpGet]
        public IActionResult Delete(int id)
        {
            try
            {
                // Get user by ID
                User? user = null;
                string connectionString = _configuration.GetConnectionString("DefaultConnection") ?? "";

                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();

                    string sql = @"SELECT ""Id"",""UserId"", ""UserName"", ""Email"", ""Mobile"", ""Roles"", ""DateCreated"", ""DateUpdated"" 
                              FROM ""Users"" WHERE ""Id"" = @id";
                    using (var cmd = new NpgsqlCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("id", id);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                user = new User
                                {
                                    Id = reader.GetInt32(0),
                                    UserId = reader.GetInt32(1),
                                    UserName = reader.GetString(2),
                                    Email = reader.GetString(3),
                                    Mobile = reader.GetString(4),
                                    Roles = reader.IsDBNull(5) ? null : reader.GetString(5),
                                    DateCreated = reader.IsDBNull(6) ? DateTime.UtcNow : reader.GetDateTime(6),
                                    DateUpdated = reader.IsDBNull(7) ? null : reader.GetDateTime(7)
                                };
                            }
                        }
                    }

                    connection.Close();
                }

                if (user == null)
                {
                    TempData["ErrorMessage"] = "User not found.";
                    return RedirectToAction("List");
                }

                return View(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user for delete");
                TempData["ErrorMessage"] = "Error retrieving user: " + ex.Message;
                return RedirectToAction("List");
            }
        }
        
        [HttpPost]
        public IActionResult Delete(int id, bool confirmed = true)
        {
            try
            {
                // Delete the user
                string connectionString = _configuration.GetConnectionString("DefaultConnection") ?? "";
                int rowsAffected = 0;
                
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    
                    string deleteSql = @"DELETE FROM ""Users"" WHERE ""Id"" = @id";
                    using (var cmd = new NpgsqlCommand(deleteSql, connection))
                    {
                        cmd.Parameters.AddWithValue("id", id);
                        rowsAffected = cmd.ExecuteNonQuery();
                        _logger.LogInformation("User deleted successfully. Rows affected: {Rows}", rowsAffected);
                    }
                    
                    connection.Close();
                }

                if (rowsAffected > 0)
                {
                    TempData["SuccessMessage"] = "User deleted successfully!";
                }
                else
                {
                    TempData["ErrorMessage"] = "User not found or could not be deleted.";
                }

                return RedirectToAction("List");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user");
                TempData["ErrorMessage"] = "Error deleting user: " + ex.Message;
                return RedirectToAction("List");
            }
        }

        public IActionResult GetAllUsersJson()
        {
            List<object> users = new List<object>();
            string connectionString = _configuration.GetConnectionString("DefaultConnection") ?? "";

            using (var connection = new NpgsqlConnection(connectionString))
            {
                connection.Open();

                string sql = @"SELECT ""Id"",""UserId"", ""UserName"", ""Email"", ""Mobile"", ""Roles"", ""DateCreated"", ""DateUpdated"" 
                          FROM ""Users"" ORDER BY ""Id""";
                using (var cmd = new NpgsqlCommand(sql, connection))
                {
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            users.Add(new
                            {
                                Id = reader.GetInt32(0),
                                UserId = reader.GetInt32(1),
                                UserName = reader.GetString(2),
                                Email = reader.GetString(3),
                                Mobile = reader.GetString(4),
                                Roles = reader.IsDBNull(5) ? null : reader.GetString(5),
                                DateCreated = reader.IsDBNull(6) ? null : reader.GetDateTime(6).ToString("yyyy-MM-dd HH:mm:ss"),
                                DateUpdated = reader.IsDBNull(7) ? null : reader.GetDateTime(7).ToString("yyyy-MM-dd HH:mm:ss")
                            });
                        }
                    }
                }

                connection.Close();
            }

            return Json(users);
        }

        private void EnsureUsersTableExists(NpgsqlConnection connection)
        {
            // Check if the table exists
            bool tableExists = false;
            using (var cmd = new NpgsqlCommand(
                "SELECT EXISTS (SELECT FROM information_schema.tables WHERE table_name = 'Users')",
                connection))
            {
                tableExists = (bool)cmd.ExecuteScalar();
            }

            if (tableExists)
            {
                // Always reset the sequence to ensure it starts at 300000
                using (var cmd = new NpgsqlCommand(
                    "SELECT setval('\"Users_UserId_seq\"', 300000, false)",
                    connection))
                {
                    cmd.ExecuteNonQuery();
                    _logger.LogInformation("UserId sequence reset to start at 300000");
                }
            }
            else
            {
                // Create the table with all columns
                string createTableSql = @"
        CREATE TABLE IF NOT EXISTS ""Users"" (
            ""Id"" SERIAL PRIMARY KEY,
            ""UserId"" SERIAL NOT NULL,
            ""UserName"" TEXT NOT NULL,
            ""Password"" TEXT NOT NULL,
            ""Email"" TEXT NOT NULL,
            ""Mobile"" TEXT NOT NULL,
            ""Roles"" TEXT,
            ""DateCreated"" TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
            ""DateUpdated"" TIMESTAMP NULL
        )";

                using (var cmd = new NpgsqlCommand(createTableSql, connection))
                {
                    cmd.ExecuteNonQuery();
                    _logger.LogInformation("Users table created successfully");
                }

                // Set the starting value for the UserId sequence to 300000
                string alterSequenceSql = @"
            SELECT setval('""Users_UserId_seq""', 300000, false)";

                using (var cmd = new NpgsqlCommand(alterSequenceSql, connection))
                {
                    cmd.ExecuteNonQuery();
                    _logger.LogInformation("UserId sequence set to start at 300000");
                }
            }
        }
    }
}