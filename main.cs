using System.IO;

static List<Question> LoadQuestionsFromCsv(string path)
{
    var questions = new List<Question>();

    if (!File.Exists(path))
        throw new FileNotFoundException($"Question file not found: {path}");

    var lines = File.ReadAllLines(path);

    // Skip header
    for (int i = 1; i < lines.Length; i++)
    {
        if (string.IsNullOrWhiteSpace(lines[i])) continue;

        // Simple CSV split (OK if you don't use commas inside text)
        string[] parts = lines[i].Split(',');

        if (parts.Length < 6)
            throw new FormatException($"Invalid CSV line {i + 1}: {lines[i]}");

        string text = parts[0];
        string[] options = new[] { parts[1], parts[2], parts[3], parts[4] };

        if (!int.TryParse(parts[5], out int correctIndex))
            throw new FormatException($"Invalid CorrectIndex at line {i + 1}");

        questions.Add(new Question(text, options, correctIndex));
    }

    return questions;
}
