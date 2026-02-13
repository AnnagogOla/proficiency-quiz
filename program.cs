using System;
using System.Collections.Generic;
using System.IO;

namespace english_learning_center
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // 1) Load questions from CSV in the same folder as the executable
            string csvPath = Path.Combine(AppContext.BaseDirectory, "questions.csv");

            List<Question> quiz;
            try
            {
                quiz = LoadQuestionsFromCsv(csvPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to load questions.");
                Console.WriteLine(ex.Message);
                Console.ReadLine();
                return;
            }

            if (quiz.Count == 0)
            {
                Console.WriteLine("No questions found in questions.csv.");
                Console.ReadLine();
                return;
            }

            int score = 0;

            Console.WriteLine("=== English Placement Quiz ===");
            Console.WriteLine("Answer by typing 1, 2, 3 or 4.\n");

            // Run quiz
            for (int i = 0; i < quiz.Count; i++)
            {
                Console.WriteLine($"Question {i + 1}: {quiz[i].Text}");

                for (int j = 0; j < quiz[i].Options.Length; j++)
                {
                    Console.WriteLine($"{j + 1}. {quiz[i].Options[j]}");
                }

                int answer = ReadAnswer(1, 4) - 1;

                if (answer == quiz[i].CorrectIndex)
                {
                    score++;
                    Console.WriteLine("Correct!\n");
                }
                else
                {
                    Console.WriteLine("Incorrect.\n");
                }
            }

            string level = GetLevel(score, quiz.Count);

            Console.WriteLine("=== Quiz Finished ===");
            Console.WriteLine($"Score: {score} / {quiz.Count}");
            Console.WriteLine($"Assigned level: {level}");
            Console.ReadLine();
        }

        static List<Question> LoadQuestionsFromCsv(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"Could not find questions.csv at: {path}");

            var questions = new List<Question>();
            var lines = File.ReadAllLines(path);

            if (lines.Length <= 1) return questions; // only header or empty

            // Start from 1 to skip header
            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;

                // Simple CSV split (OK as long as you don't use commas inside fields)
                string[] parts = lines[i].Split(',');

                if (parts.Length < 6)
                    throw new FormatException($"Invalid CSV line {i + 1}: expected 6 columns.");

                string text = parts[0].Trim();
                string[] options = new[]
                {
                    parts[1].Trim(),
                    parts[2].Trim(),
                    parts[3].Trim(),
                    parts[4].Trim()
                };

                if (!int.TryParse(parts[5].Trim(), out int correctIndexFromFile))
                    throw new FormatException($"Invalid CorrectIndex at line {i + 1}.");

                // Convert file's 1-4 into code's 0-3
                int correctIndex = correctIndexFromFile - 1;

                if (correctIndex < 0 || correctIndex > 3)
                    throw new FormatException($"CorrectIndex out of range at line {i + 1} (must be 1-4).");

                questions.Add(new Question(text, options, correctIndex));
            }

            return questions;
        }

        static int ReadAnswer(int min, int max)
        {
            while (true)
            {
                Console.Write("Your answer: ");
                string input = Console.ReadLine();

                if (int.TryParse(input, out int value))
                {
                    if (value >= min && value <= max)
                        return value;
                }

                Console.WriteLine("Invalid input. Please enter a number between 1 and 4.");
            }
        }

        static string GetLevel(int score, int totalQuestions)
        {
            double percentage = (double)score / totalQuestions * 100;

            if (percentage < 30) return "A1";
            if (percentage < 50) return "A2";
            if (percentage < 70) return "B1";
            if (percentage < 85) return "B2";
            if (percentage < 95) return "C1";
            return "C2";
        }
    }

    class Question
    {
        public string Text { get; }
        public string[] Options { get; }
        public int CorrectIndex { get; }

        public Question(string text, string[] options, int correctIndex)
        {
            Text = text;
            Options = options;
            CorrectIndex = correctIndex;
        }
    }
}
