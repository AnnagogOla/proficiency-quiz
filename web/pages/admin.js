import { useEffect, useState } from "react";

const API = process.env.NEXT_PUBLIC_API_BASE;

export default function Admin() {
  const [questions, setQuestions] = useState([]);
  const [form, setForm] = useState({ text: "", a: "", b: "", c: "", d: "", correctIndex: 0 });

  useEffect(() => {
    load();
  }, []);

  async function load() {
    const res = await fetch(`${API}/api/questions`);
    const data = await res.json();
    setQuestions(data);
  }

  async function add(e) {
    e.preventDefault();

    await fetch(`${API}/api/questions`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        text: form.text,
        options: [form.a, form.b, form.c, form.d],
        correctIndex: Number(form.correctIndex)
      })
    });

    setForm({ text: "", a: "", b: "", c: "", d: "", correctIndex: 0 });
    load();
  }

  async function remove(id) {
    await fetch(`${API}/api/questions/${id}`, { method: "DELETE" });
    load();
  }

  return (
    <div style={styles.wrap}>
      <div style={styles.card}>
        <h1>Admin Panel</h1>
        <a href="/">Back to quiz</a>

        <h3>Add Question</h3>
        <form onSubmit={add} style={{ display: "grid", gap: 10 }}>
          <input placeholder="Question text" value={form.text}
            onChange={e => setForm({ ...form, text: e.target.value })} required />

          <input placeholder="Option 1" value={form.a}
            onChange={e => setForm({ ...form, a: e.target.value })} required />

          <input placeholder="Option 2" value={form.b}
            onChange={e => setForm({ ...form, b: e.target.value })} required />

          <input placeholder="Option 3" value={form.c}
            onChange={e => setForm({ ...form, c: e.target.value })} required />

          <input placeholder="Option 4" value={form.d}
            onChange={e => setForm({ ...form, d: e.target.value })} required />

          <select value={form.correctIndex}
            onChange={e => setForm({ ...form, correctIndex: e.target.value })}>
            <option value={0}>Correct: 1</option>
            <option value={1}>Correct: 2</option>
            <option value={2}>Correct: 3</option>
            <option value={3}>Correct: 4</option>
          </select>

          <button type="submit">Add Question</button>
        </form>

        <h3>Existing Questions</h3>
        {questions.map(q => (
          <div key={q.id} style={styles.qCard}>
            <strong>{q.text}</strong>
            <button onClick={() => remove(q.id)}>Delete</button>
          </div>
        ))}
      </div>
    </div>
  );
}

const styles = {
  wrap: { minHeight: "100vh", background: "#f6f7fb", padding: 20, fontFamily: "system-ui" },
  card: { maxWidth: 800, margin: "0 auto", background: "#fff", padding: 20, borderRadius: 12 },
  qCard: { marginTop: 10, padding: 10, border: "1px solid #eee", borderRadius: 8 }
};
