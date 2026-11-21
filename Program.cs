using MySql.Data.MySqlClient;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var connectionString =
    builder.Configuration.GetConnectionString("ExpenseDb");

using (var connection = new MySqlConnection(connectionString))
{
    connection.Open();
    Console.WriteLine("MySQL connected successfully.");
}
 

//数据库检查

 

app.MapPost("/transactions", (CreateTransactionRequest request) =>
{
    if (request.Type != "income" && request.Type != "expense")
    {
        return Results.BadRequest("Type must be income or expense.");
    }

    if (request.Amount <= 0 ||
      request.Amount > 9999999999.99m ||
      decimal.Round(request.Amount, 2) != request.Amount)
    {
        return Results.BadRequest(
            "Amount must be positive, at most 9999999999.99, " +
            "and have at most two decimal places.");
    }

    // 3. 在这里加入类别检查
    if (string.IsNullOrWhiteSpace(request.Category) ||
        request.Category.Length > 100)
    {
        return Results.BadRequest(
            "Category must contain 1-100 characters.");
    }

    // 4. 接着加入日期检查
    if (request.Date.Year < 1000)
    {
        return Results.BadRequest("Date year must be at least 1000.");
    }


    using var connection = new MySqlConnection(connectionString);
    connection.Open();

    const string sql = """
        INSERT INTO transactions (`type`, amount, category, `date`)
        VALUES (@type, @amount, @category, @date);
        """;

    using var command = new MySqlCommand(sql, connection);

    command.Parameters.AddWithValue("@type", request.Type);
    command.Parameters.AddWithValue("@amount", request.Amount);
    command.Parameters.AddWithValue("@category", request.Category);
    command.Parameters.AddWithValue(
        "@date",
        request.Date.ToDateTime(TimeOnly.MinValue));

    command.ExecuteNonQuery();

    var transaction = new Transaction(
        checked((int)command.LastInsertedId),
        request.Type,
        request.Amount,
        request.Category,
        request.Date);

    return Results.Ok(transaction);
});

 

 
  

app.MapGet("/transactions", (int? year, int? month) =>
{
    if (year.HasValue != month.HasValue)
    {
        return Results.BadRequest("Provide both year and month.");
    }

    DateTime? startDate = null;
    DateTime? endDate = null;

    if (year.HasValue && month.HasValue)
    {
        if (year.Value < 1000 || year.Value > 9998 ||
            month.Value < 1 || month.Value > 12)
        {
            return Results.BadRequest(
                "Year must be 1000-9998 and month must be 1-12.");
        }

        startDate = new DateTime(year.Value, month.Value, 1);
        endDate = startDate.Value.AddMonths(1);
    }

    var results = new List<Transaction>();

    using var connection = new MySqlConnection(connectionString);
    connection.Open();

    var sql = """
        SELECT id, `type`, amount, category, `date`
        FROM transactions
        """;

    if (startDate.HasValue)
    {
        sql += " WHERE `date` >= @startDate AND `date` < @endDate";
    }

    sql += " ORDER BY id;";

    using var command = new MySqlCommand(sql, connection);

    if (startDate.HasValue)
    {
        command.Parameters.AddWithValue("@startDate", startDate.Value);
        command.Parameters.AddWithValue("@endDate", endDate!.Value);
    }

    using var reader = command.ExecuteReader();

    while (reader.Read())
    {
        results.Add(new Transaction(
            reader.GetInt32("id"),
            reader.GetString("type"),
            reader.GetDecimal("amount"),
            reader.GetString("category"),
            DateOnly.FromDateTime(reader.GetDateTime("date"))));
    }

    return Results.Ok(results);
});

app.MapGet("/transactions/summary", (int year, int month) =>
{
    if (year < 1000 || year > 9998 || month < 1 || month > 12)
    {
        return Results.BadRequest(
            "Year must be 1000-9998 and month must be 1-12.");
    }

    var startDate = new DateTime(year, month, 1);
    var endDate = startDate.AddMonths(1);

    using var connection = new MySqlConnection(connectionString);
    connection.Open();

    const string sql = """
        SELECT
            COALESCE(SUM(CASE WHEN `type` = 'income'
                THEN amount ELSE 0 END), 0) AS income,
            COALESCE(SUM(CASE WHEN `type` = 'expense'
                THEN amount ELSE 0 END), 0) AS expense
        FROM transactions
        WHERE `date` >= @startDate
          AND `date` < @endDate;
        """;

    using var command = new MySqlCommand(sql, connection);

    command.Parameters.AddWithValue("@startDate", startDate);
    command.Parameters.AddWithValue("@endDate", endDate);

    using var reader = command.ExecuteReader();
    reader.Read();

    var income = reader.GetDecimal("income");
    var expense = reader.GetDecimal("expense");
    var balance = income - expense;

    return Results.Ok(new
    {
        year,
        month,
        income,
        expense,
        balance
    });
});

app.MapDelete("/transactions/{id:int}", (int id) =>
{
    if (id <= 0)
    {
        return Results.BadRequest("Id must be greater than zero.");
    }

    using var connection = new MySqlConnection(connectionString);
    connection.Open();

    const string sql = """
        DELETE FROM transactions
        WHERE id = @id;
        """;

    using var command = new MySqlCommand(sql, connection);
    command.Parameters.AddWithValue("@id", id);

    var affectedRows = command.ExecuteNonQuery();

    if (affectedRows == 0)
    {
        return Results.NotFound();
    }

    return Results.NoContent();
});

app.MapPut("/transactions/{id:int}",
    (int id, CreateTransactionRequest request) =>
    {
        if (id <= 0)
        {
            return Results.BadRequest("Id must be greater than zero.");
        }

        if (request.Type != "income" && request.Type != "expense")
        {
            return Results.BadRequest("Type must be income or expense.");
        }

        if (request.Amount <= 0 ||
            request.Amount > 9999999999.99m ||
            decimal.Round(request.Amount, 2) != request.Amount)
        {
            return Results.BadRequest(
                "Amount must be positive, at most 9999999999.99, " +
                "and have at most two decimal places.");
        }

        if (string.IsNullOrWhiteSpace(request.Category) ||
            request.Category.Length > 100)
        {
            return Results.BadRequest(
                "Category must contain 1-100 characters.");
        }

        if (request.Date.Year < 1000)
        {
            return Results.BadRequest("Date year must be at least 1000.");
        }

        var connectionBuilder =
            new MySqlConnectionStringBuilder(connectionString)
            {
                UseAffectedRows = false
            };

        using var connection =
            new MySqlConnection(connectionBuilder.ConnectionString);
        connection.Open();

        const string sql = """
        UPDATE transactions
        SET `type` = @type,
            amount = @amount,
            category = @category,
            `date` = @date
        WHERE id = @id;
        """;

        using var command = new MySqlCommand(sql, connection);

        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@type", request.Type);
        command.Parameters.AddWithValue("@amount", request.Amount);
        command.Parameters.AddWithValue("@category", request.Category);
        command.Parameters.AddWithValue(
            "@date",
            request.Date.ToDateTime(TimeOnly.MinValue));

        var matchedRows = command.ExecuteNonQuery();

        if (matchedRows == 0)
        {
            return Results.NotFound();
        }

        return Results.Ok(new Transaction(
            id,
            request.Type,
            request.Amount,
            request.Category,
            request.Date));
    });

app.Run();
record Transaction(
    int Id,
    string Type,
    decimal Amount,
    string Category,
    DateOnly Date);

record CreateTransactionRequest(
    string Type,
    decimal Amount,
    string Category,
    DateOnly Date);