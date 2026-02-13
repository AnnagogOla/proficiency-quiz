import { useEffect, useState } from "react";

const API = process.env.NEXT_PUBLIC_API_BASE;

export default function Home() {
  const [questions, setQuestions] = useState([]);
  const [index, setIndex] = useState(0);
  const [score, setScore] = useState(0);
  const [finished, setFinished] = useState(false);
  const [level, setLevel] = useState("");

  useEffect(() => {
    fetch(`${API}/api/questions`)
      .then(res => res.json())
      .then(setQuestions)
      .catch(err => console.error(err));
  }, []);

  async function answer(choice) {
    const q = questions[index];
    const newScore = score + (choice === q.correctIndex ? 1 : 0);
    const nextIndex = index + 1;

    setScore(newScore);

    if (nextIndex >= questions.length) {
      const res = await fetch(`${API}/api/grade`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ score: newScore, total: questions.length })
      });

      const data = await res.json();
      setLevel(data.level);
      setFinished(true);
      return;
    }

    setIndex(nextIndex);
  }

  function restart() {
    setIndex(0);
    setScore(0);
    setFinished(false);
    setLevel("");
  }

  if (!questions.length)
    return <div style={styles.wrap}><div style={styles.card}>Loading...</div></div>;

  if (finished) {
    return (
      <div style={styles.wrap}>
        <div style={styles.card}>
          <h1>Quiz Finished</h1>
          <p><b>Score:</b> {score} / {questions.length}</p>
          <p><b>Level:</b> {level}</p>
          <button style={styles.button} onClick={restart}>Restart</button>
        </div>
      </div>
    );
  }

  const q = questions[index];

  return (
    <div style={styles.wrap}>
      <div style={styles.card}>
        <h1>English Placement Quiz</h1>
        <p>Question {index + 1} of {questions.length}</p>
        <h3>{q.text}</h3>

        <div style={styles.grid}>
          {q.options.map((opt, i) => (
            <button key={i} style={styles.button} onClick={() => answer(i)}>
              {i + 1}. {opt}
            </button>
          ))}
        </div>

        <a href="/admin" style={styles.link}>Admin</a>
      </div>
    </div>
  );
}

const styles = {
  wrap: { minHeight: "100vh", background: "#f6f7fb", padding: 20, fontFamily: "system-ui" },
  card: { maxWidth: 700, margin: "0 auto", background: "#fff", padding: 20, borderRadius: 12, boxShadow: "0 6px 16px rgba(0,0,0,.06)" },
  grid: { display: "grid", gap: 10 },
  button: { padding: 12, borderRadius: 10, border: "1px solid #ddd", background: "#fff", cursor: "pointer" },
  link: { display: "block", marginTop: 20, color: "#0b57d0" }
};
