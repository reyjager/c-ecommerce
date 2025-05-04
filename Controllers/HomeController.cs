using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using MyMvcProject.Data;
using MyMvcProject.Models;
using BCrypt.Net;

namespace MyMvcProject.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;

    public HomeController(ILogger<HomeController> logger, ApplicationDbContext context, IConfiguration configuration)
    {
        _logger = logger;
        _context = context;
        _configuration = configuration;
    }

    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    public IActionResult Users()
    {
        try
        {
            // Use direct SQL to get users
            List<User> users = new List<User>();
            string connectionString = _configuration.GetConnectionString("DefaultConnection") ?? "";
            
            using (var connection = new NpgsqlConnection(connectionString))
            {
                connection.Open();
                
                string sql = @"SELECT ""Id"", ""UserName"", ""Password"", ""Email"", ""Mobile"", ""Roles"", ""DateCreated"", ""DateUpdated"" 
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
                                UserName = reader.GetString(1),
                                Password = reader.GetString(2),
                                Email = reader.GetString(3),
                                Mobile = reader.GetString(4),
                                Roles = reader.IsDBNull(5) ? null : reader.GetString(5),
                                DateCreated = reader.IsDBNull(6) ? DateTime.UtcNow : reader.GetDateTime(6),
                                DateUpdated = reader.IsDBNull(7) ? null : reader.GetDateTime(7)
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

    public IActionResult Add()
    {
        return View("AddUser", new User());
    }

    [HttpGet]
    public IActionResult AddUser()
    {
        return View(new User());
    }

    [HttpPost]
    public IActionResult AddUser(User user)
    {
        // Debug logging
        _logger.LogInformation("AddUser POST method called");
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
                
                // Hash the password
                string hashedPassword = BCrypt.Net.BCrypt.HashPassword(user.Password);
                
                // Insert the user directly with SQL
                string insertSql = @"
                    INSERT INTO ""Users"" (""UserName"", ""Password"", ""Email"", ""Mobile"", ""Roles"", ""DateCreated"", ""DateUpdated"")
                    VALUES (@userName, @password, @email, @mobile, @roles, @dateCreated, @dateUpdated)
                    RETURNING ""Id""";
                
                using (var cmd = new NpgsqlCommand(insertSql, connection))
                {
                    DateTime now = DateTime.UtcNow;
                    
                    cmd.Parameters.AddWithValue("userName", user.UserName);
                    cmd.Parameters.AddWithValue("password", hashedPassword);
                    cmd.Parameters.AddWithValue("email", user.Email);
                    cmd.Parameters.AddWithValue("mobile", user.Mobile);
                    cmd.Parameters.AddWithValue("roles", user.Roles ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("dateCreated", now);
                    cmd.Parameters.AddWithValue("dateUpdated", DBNull.Value);
                    
                    // Execute the command and get the new ID
                    int newId = Convert.ToInt32(cmd.ExecuteScalar());
                    user.Id = newId;
                    user.DateCreated = now;
                    
                    _logger.LogInformation("User saved successfully with ID: {Id}", newId);
                }
                
                connection.Close();
            }
            
            // Add success message
            TempData["SuccessMessage"] = "User registered successfully!";
            
            // Redirect to Users page
            return RedirectToAction("Users");
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
                    return AddUser(user);
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
                
                string sql = @"SELECT ""Id"", ""UserName"", ""Password"", ""Email"", ""Mobile"", ""Roles"", ""DateCreated"", ""DateUpdated"" 
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
                                UserName = reader.GetString(1),
                                Password = reader.GetString(2),
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
                return RedirectToAction("Users");
            }
            
            return View(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user for edit");
            TempData["ErrorMessage"] = "Error retrieving user.";
            return RedirectToAction("Users");
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
                        SET ""UserName"" = @userName, 
                            ""Password"" = @password,
                            ""Email"" = @email, 
                            ""Mobile"" = @mobile, 
                            ""Roles"" = @roles,
                            ""DateUpdated"" = @dateUpdated
                        WHERE ""Id"" = @id";
                        
                    using (var cmd = new NpgsqlCommand(updateSql, connection))
                    {
                        DateTime now = DateTime.UtcNow;
                        
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
                        SET ""UserName"" = @userName, 
                            ""Email"" = @email, 
                            ""Mobile"" = @mobile, 
                            ""Roles"" = @roles,
                            ""DateUpdated"" = @dateUpdated
                        WHERE ""Id"" = @id";
                        
                    using (var cmd = new NpgsqlCommand(updateSql, connection))
                    {
                        DateTime now = DateTime.UtcNow;
                        
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
            return RedirectToAction("Users");
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
                
                string sql = @"SELECT ""Id"", ""UserName"", ""Email"", ""Mobile"", ""Roles"", ""DateCreated"", ""DateUpdated"" 
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
                                UserName = reader.GetString(1),
                                Email = reader.GetString(2),
                                Mobile = reader.GetString(3),
                                Roles = reader.IsDBNull(4) ? null : reader.GetString(4),
                                DateCreated = reader.IsDBNull(5) ? DateTime.UtcNow : reader.GetDateTime(5),
                                DateUpdated = reader.IsDBNull(6) ? null : reader.GetDateTime(6)
                            };
                        }
                    }
                }
                
                if (user != null)
                {
                    // Delete the user
                    string deleteSql = @"DELETE FROM ""Users"" WHERE ""Id"" = @id";
                    using (var cmd = new NpgsqlCommand(deleteSql, connection))
                    {
                        cmd.Parameters.AddWithValue("id", id);
                        
                        int rowsAffected = cmd.ExecuteNonQuery();
                        _logger.LogInformation("User deleted successfully. Rows affected: {Rows}", rowsAffected);
                    }
                }
                
                connection.Close();
            }
            
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
            }
            else
            {
                TempData["SuccessMessage"] = "User deleted successfully!";
            }
            
            return RedirectToAction("Users");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user");
            TempData["ErrorMessage"] = "Error deleting user: " + ex.Message;
            return RedirectToAction("Users");
        }
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
            // Check if the table has the DateCreated and DateUpdated columns
            bool hasDateColumns = false;
            using (var cmd = new NpgsqlCommand(
                "SELECT COUNT(*) FROM information_schema.columns " +
                "WHERE table_name = 'Users' AND column_name = 'DateCreated'", 
                connection))
            {
                hasDateColumns = ((long)cmd.ExecuteScalar() > 0);
            }
            
            if (!hasDateColumns)
            {
                // Alter the table to add the new columns
                using (var cmd = new NpgsqlCommand(
                    "ALTER TABLE \"Users\" " +
                    "ADD COLUMN \"DateCreated\" TIMESTAMP DEFAULT CURRENT_TIMESTAMP, " +
                    "ADD COLUMN \"DateUpdated\" TIMESTAMP NULL", 
                    connection))
                {
                    cmd.ExecuteNonQuery();
                    _logger.LogInformation("Users table altered to add date columns");
                }
            }
        }
        else
        {
            // Create the table with all columns
            string createTableSql = @"
                CREATE TABLE IF NOT EXISTS ""Users"" (
                    ""Id"" SERIAL PRIMARY KEY,
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
        }
    }
    
    public IActionResult GetAllUsersJson()
    {
        List<object> users = new List<object>();
        string connectionString = _configuration.GetConnectionString("DefaultConnection") ?? "";
        
        using (var connection = new NpgsqlConnection(connectionString))
        {
            connection.Open();
            
            string sql = @"SELECT ""Id"", ""UserName"", ""Email"", ""Mobile"", ""Roles"", ""DateCreated"", ""DateUpdated"" 
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
                            UserName = reader.GetString(1),
                            Email = reader.GetString(2),
                            Mobile = reader.GetString(3),
                            Roles = reader.IsDBNull(4) ? null : reader.GetString(4),
                            DateCreated = reader.IsDBNull(5) ? null : reader.GetDateTime(5).ToString("yyyy-MM-dd HH:mm:ss"),
                            DateUpdated = reader.IsDBNull(6) ? null : reader.GetDateTime(6).ToString("yyyy-MM-dd HH:mm:ss")
                        });
                    }
                }
            }
            
            connection.Close();
        }
        
        return Json(users);
    }
}