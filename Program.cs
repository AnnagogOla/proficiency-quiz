using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.FileProviders;
using System.Text;
using System.Text.Encodings.Web;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// --------------------
// SQLite setup
// --------------------
var dbPath = Path.Combine(app.Environment.ContentRootPath, "data.db");
var connString = new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString();

static SqliteConnection Open(string connString)
{
    var c = new SqliteConnection(connString);
    c.Open();
    return c;
}

static void InitDb(string connString)
{
    using var con = Open(connString);
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
    """;
    cmd.ExecuteNonQuery();
}

static int CountQuestions(string connString)
{
    using var con = Open(connString);
    using var cmd = con.CreateCommand();
    cmd.CommandText = "SELECT COUNT(*) FROM Questions;";
    return Convert.ToInt32(cmd.ExecuteScalar());
}

static void SeedFromCsvIfEmpty(string connString, string csvPath)
{
    if (!File.Exists(csvPath)) return;
    if (CountQuestions(connString) > 0) return;

    var lines = File.ReadAllLines(csvPath);
    if (lines.Length <= 1) return;

    using var con = Open(connString);
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
        var correctIndex = correctFromFile - 1; // CSV uses 1-4; DB uses 0-3
        if (correctIndex < 0 || correctIndex > 3) continue;

        using var cmd = con.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
        INSERT INTO Questions(Text, A, B, C, D, CorrectIndex)
        VALUES ($t, $a, $b, $c, $d, $ci);
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

InitDb(connString);
// seed from your existing questions.csv at repo root
SeedFromCsvIfEmpty(connString, Path.Combine(app.Environment.ContentRootPath, "questions.csv"));

// --------------------
// Helpers
// --------------------
static string GetLevel(int score, int total)
{
    double pct = (double)score / total * 100;
    if (pct < 30) return "A1";
    if (pct < 50) return "A2";
    if (pct < 70) return "B1";
    if (pct < 85) return "B2";
    if (pct < 95) return "C1";
    return "C2";
}

record QuestionRow(long Id, string Text, string A, string B, string C, string D, int CorrectIndex);

static List<QuestionRow> LoadAll(string connString)
{
    var list = new List<QuestionRow>();
    using var con = Open(connString);
    using var cmd = con.CreateCommand();
    cmd.CommandText = "SELECT Id, Text, A, B, C, D, CorrectIndex FROM Questions ORDER BY Id;";
    using var r = cmd.ExecuteReader();
    while (r.Read())
    {
        list.Add(new QuestionRow(
            r.GetInt64(0),
            r.GetString(1),
            r.GetString(2),
            r.GetString(3),
            r.GetString(4),
            r.GetString(5),
            r.GetInt32(6)
        ));
    }
    return list;
}

static string Html(string s) => HtmlEncoder.Default.Encode(s);

// --------------------
// Routes
// --------------------

// Home quiz page
app.MapGet("/", () =>
{
    // Simple responsive HTML; quiz loads questions from /api/questions
    var page = """
<!doctype html>
<html>
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width,initial-scale=1" />
  <title>Proficiency Quiz</title>
  <style>
    body { font-family: system-ui, -apple-system, Segoe UI, Roboto, Arial; margin: 0; background:#f6f7fb; }
    .wrap { max-width: 820px; margin: 0 auto; padding: 18px; }
    .card { background:#fff; border-radius: 14px; padding: 18px; box-shadow: 0 6px 18px rgba(0,0,0,.06); }
    h1 { font-size: 22px; margin: 0 0 10px; }
    .muted { color:#666; font-size: 14px; margin-bottom: 16px; }
    .q { font-size: 18px; margin: 14px 0; }
    .grid { display: grid; grid-template-columns: 1fr; gap: 10px; }
    @media (min-width: 700px) { .grid { grid-template-columns: 1fr 1fr; } }
    button { padding: 12px 12px; border-radius: 12px; border: 1px solid #ddd; background: #fff; text-align:left; cursor:pointer; }
    button:hover { border-color: #bbb; }
    .top { display:flex; justify-content:space-between; align-items:center; gap: 12px; }
    a { color:#0b57d0; text-decoration:none; font-size: 14px; }
    .pill { display:inline-block; padding: 6px 10px; border-radius: 999px; background:#eef2ff; font-size: 13px; }
    .result { font-size: 18px; }
  </style>
</head>
<body>
  <div class="wrap">
    <div class="card">
      <div class="top">
        <h1>English Placement Quiz</h1>
        <a href="/admin">Admin</a>
      </div>
      <div class="muted">Answer the questions. You’ll get a CEFR level (A1–C2).</div>

      <div id="app"></div>
    </div>
  </div>

<script>
let questions = [];
let i = 0;
let score = 0;

async function load() {
  const res = await fetch('/api/questions');
  questions = await res.json();
  if (!questions.length) {
    document.getElementById('app').innerHTML = '<p>No questions found. Ask admin to add some.</p>';
    return;
  }
  render();
}

function render() {
  const q = questions[i];
  const total = questions.length;

  document.getElementById('app').innerHTML = `
    <div class="pill">Question ${i+1} / ${total}</div>
    <div class="q">${escapeHtml(q.text)}</div>
    <div class="grid">
      ${q.options.map((opt, idx) => `<button onclick="answer(${idx})">${idx+1}. ${escapeHtml(opt)}</button>`).join('')}
    </div>
  `;
}

async function answer(choice) {
  const q = questions[i];
  if (choice === q.correctIndex) score++;

  i++;
  if (i >= questions.length) {
    const res = await fetch('/api/grade', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ score, total: questions.length })
    });
    const data = await res.json();
    document.getElementById('app').innerHTML = `
      <div class="result"><b>Score:</b> ${score} / ${questions.length}</div>
      <div class="result"><b>Level:</b> ${data.level}</div>
      <p><button onclick="restart()">Restart</button></p>
    `;
    return;
  }
  render();
}

function restart() { i = 0; score = 0; render(); }

function escapeHtml(s) {
  return s.replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('>','&gt;').replaceAll('"','&quot;').replaceAll("'","&#039;");
}

load();
</script>
</body>
</html>
""";
    return Results.Content(page, "text/html; charset=utf-8");
});

// Admin page to add/delete questions
app.MapGet("/admin", () =>
{
    var rows = LoadAll(connString);

    var sb = new StringBuilder();
    sb.Append("""
<!doctype html><html><head>
<meta charset="utf-8" />
<meta name="viewport" content="width=device-width,initial-scale=1" />
<title>Admin - Questions</title>
<style>
 body{font-family:system-ui;margin:0;background:#f6f7fb;}
 .wrap{max-width:980px;margin:0 auto;padding:18px;}
 .card{background:#fff;border-radius:14px;padding:18px;box-shadow:0 6px 18px rgba(0,0,0,.06);}
 input,select{padding:10px;border-radius:10px;border:1px solid #ddd;width:100%;}
 .grid{display:grid;grid-template-columns:1fr;gap:10px;}
 @media(min-width:900px){.grid{grid-template-columns:2fr 1fr 1fr;}}
 .row{display:grid;grid-template-columns:1fr;gap:10px;margin:10px 0;}
 @media(min-width:900px){.row{grid-template-columns:2fr 1fr 1fr 1fr 1fr 120px;}}
 button{padding:10px 12px;border-radius:10px;border:1px solid #ddd;background:#fff;cursor:pointer;}
 button:hover{border-color:#bbb;}
 a{color:#0b57d0;text-decoration:none;}
 .muted{color:#666;font-size:14px;}
</style></head><body>
<div class="wrap">
 <div class="card">
  <div style="display:flex;justify-content:space-between;align-items:center;gap:12px;">
    <h2 style="margin:0;">Admin: Edit Questions</h2>
    <a href="/">Back to quiz</a>
  </div>
  <p class="muted">Add questions and they update instantly for users.</p>

  <h3>Add new</h3>
  <form method="post" action="/admin/add">
    <div class="row">
      <input name="text" placeholder="Question text" required />
      <input name="a" placeholder="Option 1" required />
      <input name="b" placeholder="Option 2" required />
      <input name="c" placeholder="Option 3" required />
      <input name="d" placeholder="Option 4" required />
      <select name="correctIndex" required>
        <option value="0">Correct: 1</option>
        <option value="1">Correct: 2</option>
        <option value="2">Correct: 3</option>
        <option value="3">Correct: 4</option>
      </select>
    </div>
    <button type="submit">Add</button>
  </form>

  <h3 style="margin-top:22px;">Existing</h3>
""");

    foreach (var q in rows)
    {
        sb.Append($"""
  <form method="post" action="/admin/delete" style="margin:10px 0;">
    <input type="hidden" name="id" value="{q.Id}" />
    <div class="row">
      <input value="{Html(q.Text)}" disabled />
      <input value="{Html(q.A)}" disabled />
      <input value="{Html(q.B)}" disabled />
      <input value="{Html(q.C)}" disabled />
      <input value="{Html(q.D)}" disabled />
      <button type="submit">Delete</button>
    </div>
  </form>
""");
    }

    sb.Append("""
 </div>
</div>
</body></html>
""");
    return Results.Content(sb.ToString(), "text/html; charset=utf-8");
});

app.MapPost("/admin/add", async (HttpRequest req) =>
{
    var form = await req.ReadFormAsync();
    var text = form["text"].ToString();
    var a = form["a"].ToString();
    var b = form["b"].ToString();
    var c = form["c"].ToString();
    var d = form["d"].ToString();
    var ciStr = form["correctIndex"].ToString();
