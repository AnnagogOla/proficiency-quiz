using Microsoft.Data.Sqlite;

var builder = WebApplication.CreateBuilder(args);

// Render sets PORT. Make sure Kestrel listens correctly.
builder.WebHost.UseUrls($"http://0.0.0.0:{Environment.GetEnvironmentVariable("PORT") ?? "8080"}");

var app = builder.Build();

// ---------- CORS (allow your Vercel frontend) ----------
var allowedOrigin = Environment.GetEnvironmentVariable("FRONTEND_ORIGIN") ?? "*";
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["Access-Control-Allow-Origin"] = allowedOrigin == "*" ? "*" : allowedOrigin;
    ctx.Response.Headers["Access-Control-Allow-Methods"] = "GET,POST,PUT,DELETE,OPTIONS";
    ctx.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type, Authorization";
    if (ctx.Request.Method == "OPTIONS")
    {
        ctx.Response.StatusCode = 204;
        return;
    }
    await next();
});

// ---------- Database (SQLite) ----------
var dbPath = Path.Combine(app.Environment.ContentRootPath, "data.db");
var connString = new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString();

SqliteConnection Open()
{
    var c = new SqliteConnection(connString);
    c.Open();
    return c;
}

void InitDb()
{
    using var con = Open();
    using var cmd = con.CreateCommand();
    cmd.CommandText = """
    CREATE TABLE IF NOT EXISTS Questions (
        Id INTEGER PRIMARY KEY AUTOINCREMENT,
        Text TEXT NOT NULL,
        A TEXT NOT NULL,
        B TEXT NOT NULL,
        C TEXT NOT NULL,
        D TEXT NOT NULL,
        CorrectIndex INTEGER NOT NULL CHECK(CorrectIndex BETWEEN 0 AND 3)
    );

    CREATE TABLE IF NOT EXISTS Results (
        Id INTEGER PRIMARY KEY AUTOINCREMENT,
        Score INTEGER NOT NULL,
        Total INTEGER NOT NULL,
        Level TEXT NOT NULL,
        CreatedAtUtc TEXT NOT NULL
    );
    """;
    cmd.ExecuteNonQuery();
}

int CountQuestions()
{
    using var con = Open();
    using var cmd = con.CreateCommand();
    cmd.CommandText = "SELECT COUNT(*) FROM Questions;";
    return Convert.ToInt32(cmd.ExecuteScalar());
}

void SeedFromCsvIfEmpty()
{
    // Seed from /questions.csv at repo root (Render deploy includes it)
    var csvPath = Path.Combine(app.Environment.ContentRootPath, "..", "questions.csv");
    if (!File.Exists(csvPath)) return;
    if (CountQuestions() > 0) return;

    var lines = File.ReadAllLines(csvPath);
    if (lines.Length <= 1) return;

    using var con = Open();
    using var tx = con.BeginTransaction();

    for (int i = 1; i < lines.Length; i++)
    {
        if (string.IsNullOrWhiteSpace(lines[i])) continue;
        var parts = lines[i].Split(',');
        if (parts.Length < 6) continue;

        var text = parts[0].Trim();
        var a = parts[1].Trim();
        var b = parts[2].Trim();
        var c = parts[3].Trim();
        var d = parts[4].Trim();

        if (!int.TryParse(parts[5].Trim(), out var correctFromFile)) continue;
        var correctIndex = correctFromFile - 1; // CSV is 1-4; backend uses 0-3
        if (correctIndex < 0 || correctIndex > 3) continue;

        using var cmd = con.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
          INSERT INTO Questions(Text, A, B, C, D, CorrectIndex)
          VALUES ($t,$a,$b,$c,$d,$ci);
        """;
        cmd.Parameters.AddWithValue("$t", text);
        cmd.Parameters.AddWithValue("$a", a);
        cmd.Parameters.AddWithValue("$b", b);
        cmd.Parameters.AddWithValue("$c", c);
        cmd.Parameters.AddWithValue("$d", d);
        cmd.Parameters.AddWithValue("$ci", correctIndex);
        cmd.ExecuteNonQuery();
    }

    tx.Commit();
}

string GetLevel(int score, int total)
{
    double pct = (double)score / total * 100;
    if (pct < 30) return "A1";
    if (pct < 50) return "A2";
    if (pct < 70) return "B1";
    if (pct < 85) return "B2";
    if (pct < 95) return "C1";
    return "C2";
}

InitDb();
SeedFromCsvIfEmpty();

// ---------- DTOs ----------
record QuestionDto(long Id, string Text, string[] Options, int CorrectIndex);
record CreateQuestion(string Text, string[] Options, int CorrectIndex);
record GradeRequest(int Score, int Total);

// ---------- API ----------
app.MapGet("/health", () => Results.Ok(new { ok = true }));

app.MapGet("/api/questions", () =>
{
    var list = new List<QuestionDto>();
    using var con = Open();
    using var cmd = con.CreateCommand();
    cmd.CommandText = "SELECT Id, Text, A, B, C, D, CorrectIndex FROM Questions ORDER BY Id;";
    using var r = cmd.ExecuteReader();
    while (r.Read())
    {
        list.Add(new QuestionDto(
            r.GetInt64(0),
            r.GetString(1),
            new[] { r.GetString(2), r.GetString(3), r.GetString(4), r.GetString(5) },
            r.GetInt32(6)
        ));
    }
    return Results.Json(list);
});

// Admin: create question
app.MapPost("/api/questions", async (HttpRequest req) =>
{
    var body = await req.ReadFromJsonAsync<CreateQuestion>();
    if (body is null) return Results.BadRequest("Invalid body.");
    if (body.Options is null || body.Options.Length != 4) return Results.BadRequest("Options must be length 4.");
    if (body.CorrectIndex < 0 || body.CorrectIndex > 3) return Results.BadRequest("CorrectIndex must be 0-3.");
    if (string.IsNullOrWhiteSpace(body.Text)) return Results.BadRequest("Text required.");

    using var con = Open();
    using var cmd = con.CreateCommand();
    cmd.CommandText = """
      INSERT INTO Questions(Text, A, B, C, D, CorrectIndex)
      VALUES ($t,$a,$b,$c,$d,$ci);
      SELECT last_insert_rowid();
    """;
    cmd.Parameters.AddWithValue("$t", body.Text.Trim());
    cmd.Parameters.AddWithValue("$a", body.Options[0].Trim());
    cmd.Parameters.AddWithValue("$b", body.Options[1].Trim());
    cmd.Parameters.AddWithValue("$c", body.Options[2].Trim());
    cmd.Parameters.AddWithValue("$d", body.Options[3].Trim());
    cmd.Parameters.AddWithValue("$ci", body.CorrectIndex);

    var id = (long)(cmd.ExecuteScalar() ?? 0L);
    return Results.Json(new { id });
});

// Admin: delete question
app.MapDelete("/api/questions/{id:long}", (long id) =>
{
    using var con = Open();
    using var cmd = con.CreateCommand();
    cmd.CommandText = "DELETE FROM Questions WHERE Id = $id;";
    cmd.Parameters.AddWithValue("$id", id);
    var rows = cmd.ExecuteNonQuery();
    return rows > 0 ? Results.Ok() : Results.NotFound();
});

// Grade + save result
app.MapPost("/api/grade", async (HttpRequest req) =>
{
    var body = await req.ReadFromJsonAsync<GradeRequest>();
    if (body is null || body.Total <= 0) return Results.BadRequest();

    var level = GetLevel(body.Score, body.Total);

    using var con = Open();
    using var cmd = con.CreateCommand();
    cmd.CommandText = """
      INSERT INTO Results(Score, Total, Level, CreatedAtUtc)
      VALUES ($s,$t,$l,$c);
    """;
    cmd.Parameters.AddWithValue("$s", body.Score);
    cmd.Parameters.AddWithValue("$t", body.Total);
    cmd.Parameters.AddWithValue("$l", level);
    cmd.Parameters.AddWithValue("$c", DateTime.UtcNow.ToString("O"));
    cmd.ExecuteNonQuery();

    return Results.Json(new { level });
});

app.Run();
